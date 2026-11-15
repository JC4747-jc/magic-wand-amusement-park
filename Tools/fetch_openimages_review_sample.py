"""Download a bounded, attributed review sample; never creates training labels/splits."""
import argparse
from collections import Counter, defaultdict
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time
import urllib.error
import urllib.request

from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.codex-tmp/yolo-training/source-metadata/openimages-candidates.json'
OUT = ROOT / '.codex-tmp/yolo-training/openimages-review'
PER_IMAGE = 2 * 1024**2
TOTAL = 128 * 1024**2
Image.MAX_IMAGE_PIXELS = 16_000_000


def digest(data):
    return hashlib.sha256(data).hexdigest()


def inspect(data):
    with Image.open(io.BytesIO(data)) as image:
        assert image.format == 'JPEG' and image.width * image.height <= 16_000_000
        image.load()
        assert image.getexif().get(274, 1) == 1, 'EXIF rotation needs annotation-coordinate review'
        oriented = ImageOps.exif_transpose(image)
        assert oriented.size == image.size, 'EXIF rotation needs annotation-coordinate review'
        rgb = image.convert('RGB')
        return {'size': list(image.size), 'pixel_sha256': digest(rgb.tobytes())}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--per-class', type=int, default=3)
    parser.add_argument('--classes', help='Comma-separated candidate classes; omitted means all candidates')
    parser.add_argument('--manifest-name', default='manifest.json')
    args = parser.parse_args()
    assert 1 <= args.per_class <= 40
    assert re.fullmatch(r'[a-z0-9][a-z0-9-]*\.json', args.manifest_name)
    started = time.monotonic()
    source_bytes = SOURCE.read_bytes()
    source = json.loads(source_bytes)
    by_class = defaultdict(list)
    for record in source['records']:
        for category in {a['canonical_candidate'] for a in record['annotations']}:
            by_class[category].append(record['image_id'])
    wanted_classes = set(args.classes.split(',')) if args.classes else set(by_class)
    assert wanted_classes and wanted_classes <= set(by_class), 'Unknown candidate class'
    chosen = {key for category, ids in by_class.items() if category in wanted_classes for key in sorted(ids)[:args.per_class]}
    OUT.mkdir(parents=True, exist_ok=True)
    images = OUT / 'images'
    images.mkdir(exist_ok=True)
    used = sum(p.stat().st_size for p in images.glob('*.jpg'))
    assert used <= TOTAL
    results = []
    for record in source['records']:
        key = record['image_id']
        if key not in chosen:
            continue
        assert re.fullmatch('[0-9a-f]{16}', key)
        assert time.monotonic() - started < 900, 'Review fetch exceeded 15-minute budget'
        assert shutil.disk_usage(ROOT).free >= 20 * 1024**3, 'Host disk reserve below 20 GiB'
        meta = record['official_metadata']
        license_url = meta['License'].replace('http://', 'https://').rstrip('/')
        assert re.fullmatch(r'https://creativecommons.org/licenses/(by|by-sa)/(2\.0|2\.5|3\.0|4\.0)', license_url), license_url
        assert meta['Author'] and meta['OriginalLandingURL']
        url = f'https://open-images-dataset.s3.amazonaws.com/validation/{key}.jpg'
        entry = dict(record, download_url=url, review_status='PENDING_SEMANTICS_BOXES_PROVENANCE', accepted_for_training=False)
        path = images / (key + '.jpg')
        try:
            if path.exists():
                assert path.stat().st_size <= PER_IMAGE
                data = path.read_bytes()
            else:
                assert TOTAL - used > 0, 'Owned image budget exhausted; no further network request'
                if os.name == 'nt':
                    # Windows Schannel trusts the system roots; never disable TLS verification.
                    response = subprocess.run(['curl.exe', '--silent', '--show-error', '--fail', '--location',
                        '--proto', '=https', '--proto-redir', '=https', '--max-redirs', '3', '--retry', '0',
                        '--connect-timeout', '10', '--max-time', '25', '--max-filesize', str(min(PER_IMAGE, TOTAL-used)), url],
                        capture_output=True, timeout=30, creationflags=subprocess.CREATE_NO_WINDOW)
                    if response.returncode:
                        raise OSError('System TLS downloader: ' + response.stderr.decode(errors='replace')[:300])
                    data = response.stdout
                else:
                    request = urllib.request.Request(url, headers={'User-Agent': 'MagicMR-bounded-data-review/1.0'})
                    with urllib.request.urlopen(request, timeout=20) as response:
                        assert int(response.headers.get('Content-Length', '0')) <= PER_IMAGE
                        data = response.read(min(PER_IMAGE, TOTAL-used) + 1)
                assert len(data) <= PER_IMAGE and used + len(data) <= TOTAL, 'Image download cap reached'
                info = inspect(data)
                path.write_bytes(data)
                used += len(data)
            assert len(data) <= PER_IMAGE
            info = inspect(data)
            entry.update(info, bytes=len(data), sha256=digest(data), local_path=str(path.relative_to(ROOT)), downloaded=True)
        except (urllib.error.URLError, TimeoutError, OSError, AssertionError, subprocess.TimeoutExpired) as error:
            entry.update(downloaded=False, error=f'{type(error).__name__}: {error}')
        results.append(entry)
        print(json.dumps({'image_id': key, 'downloaded': entry['downloaded'], 'classes': sorted({a['canonical_candidate'] for a in record['annotations']})}), flush=True)
    coverage = Counter(a['canonical_candidate'] for r in results if r['downloaded'] for a in r['annotations'])
    report = {'purpose': 'MANUAL_REVIEW_ONLY_NOT_TRAINING_ACCEPTED', 'source_index_sha256': digest(source_bytes),
              'per_class_requested': args.per_class, 'requested_classes': sorted(wanted_classes), 'unique_requested': len(chosen), 'image_downloads': sum(r['downloaded'] for r in results),
              'owned_jpeg_bytes': used, 'caps': {'per_image_bytes': PER_IMAGE, 'total_image_bytes': TOTAL, 'elapsed_seconds': 900, 'parallelism': 1},
              'annotation_counts': dict(coverage), 'training_split_assigned': False,
              'notes': ['Official validation is a source partition, not this project holdout.',
                        'Retained official image metadata does not replace per-image provenance/semantic/box review.',
                        'Other target classes may be unlabeled; do not treat missing annotations as verified negatives.',
                        'Statue/Sculpture and PottedPlant/Houseplant mappings remain provisional.'], 'records': results}
    (OUT / args.manifest_name).write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k != 'records'}, ensure_ascii=False), flush=True)
    assert report['image_downloads'] > 0, 'No images downloaded; review errors in manifest'


if __name__ == '__main__':
    main()
