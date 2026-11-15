"""Fetch a small licensed-photo review set; no automatic boxes/split acceptance."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import time
from urllib.parse import urlparse

from fetch_openimages_review_sample import inspect

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / '.codex-tmp/yolo-training/gap-sources'
OUT = ROOT / '.codex-tmp/yolo-training/gap-review'
BRONZE_IDS = [177625644, 101302261, 98011103, 97859136, 4300068, 101302262,
              177812865, 98006765, 97144425, 97144424, 175786493, 31216981, 20059983]
PER_IMAGE = 2 * 1024**2
TOTAL = 64 * 1024**2


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--per-species', type=int, default=8)
    args = parser.parse_args()
    assert 1 <= args.per_species <= 20
    source_bytes = (FOLDER / 'candidate-index.json').read_bytes()
    data = json.loads(source_bytes)
    selected = []
    counts = Counter()
    for r in data['cockroach_candidates']:
        if counts[r['taxon_name']] >= args.per_species:
            continue
        photo = r['photo']
        assert photo['license_code'] in ('cc0', 'cc-by', 'cc-by-sa')
        assert photo.get('attribution') and not photo.get('hidden') and not photo.get('flags')
        url = photo['url']
        assert url.endswith('/square.jpg')
        selected.append(dict(r, source_key='inat-' + str(r['observation_id']),
            image_url=url.removesuffix('/square.jpg') + '/large.jpg',
            capture_group='inat-observer-' + str(r['observer_id']) + '-day-' + str(r['observed_on'])))
        counts[r['taxon_name']] += 1
    for r in data['bronze_candidates']:
        if r['page_id'] in BRONZE_IDS:
            info = r['image_info']
            license_url = info['extmetadata'].get('LicenseUrl', {}).get('value', '').replace('http://', 'https://')
            assert re.fullmatch(r'https://creativecommons.org/(?:licenses/(?:by|by-sa)/(?:2\.0|2\.5|3\.0|4\.0)|publicdomain/zero/1\.0)(?:/deed\.en)?/?', license_url)
            selected.append(dict(r, source_key='commons-' + str(r['page_id']), image_url=info['thumburl'],
                                 capture_group='PENDING_ARTIFACT_ID_REVIEW'))
    OUT.mkdir(parents=True, exist_ok=True)
    images = OUT / 'images'
    images.mkdir(exist_ok=True)
    used = sum(p.stat().st_size for p in images.glob('*.jpg'))
    assert used <= TOTAL
    started = time.monotonic()
    results = []
    for r in selected:
        assert time.monotonic() - started < 600, 'Photo-review fetch exceeded 10-minute budget'
        assert shutil.disk_usage(ROOT).free >= 20 * 1024**3
        assert urlparse(r['image_url']).hostname in ('inaturalist-open-data.s3.amazonaws.com', 'static.inaturalist.org', 'thumb.wikimedia.org', 'upload.wikimedia.org')
        path = images / (r['source_key'] + '.jpg')
        entry = dict(r)
        try:
            if path.exists():
                assert path.stat().st_size <= PER_IMAGE
                raw = path.read_bytes()
            else:
                assert TOTAL - used > 0, 'Owned image budget exhausted; no further network request'
                response = subprocess.run(['curl.exe', '--silent', '--show-error', '--fail', '--location',
                    '--proto', '=https', '--proto-redir', '=https', '--max-redirs', '3', '--retry', '0',
                    '--connect-timeout', '10', '--max-time', '25', '--max-filesize', str(min(PER_IMAGE, TOTAL-used)),
                    '--user-agent', 'MagicMR-bounded-training-data-review/1.0', r['image_url']],
                    capture_output=True, timeout=30, creationflags=subprocess.CREATE_NO_WINDOW)
                if response.returncode:
                    raise OSError(response.stderr.decode(errors='replace')[:300])
                raw = response.stdout
                assert len(raw) <= PER_IMAGE and used + len(raw) <= TOTAL
                inspect(raw)
                path.write_bytes(raw)
                used += len(raw)
            assert len(raw) <= PER_IMAGE
            entry.update(inspect(raw), downloaded=True, bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest(),
                         local_path=path.relative_to(ROOT).as_posix())
        except (OSError, AssertionError, subprocess.TimeoutExpired) as error:
            entry.update(downloaded=False, error=type(error).__name__ + ': ' + str(error))
        results.append(entry)
        print(json.dumps({'key': entry['source_key'], 'downloaded': entry['downloaded']}), flush=True)
    report = {'purpose': 'MANUAL_REVIEW_NOT_TRAINING_ACCEPTANCE', 'source_index_sha256': hashlib.sha256(source_bytes).hexdigest(),
              'requested': len(selected), 'downloaded': sum(r['downloaded'] for r in results), 'owned_image_bytes': used,
              'caps': {'per_image_bytes': PER_IMAGE, 'total_image_bytes': TOTAL, 'seconds': 600, 'parallelism': 1},
              'training_split_assigned': False, 'records': results}
    (OUT / 'manifest.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf8')
    print(json.dumps({k: v for k, v in report.items() if k != 'records'}), flush=True)
    assert report['downloaded'] > 0, 'No review photos downloaded'


if __name__ == '__main__':
    main()
