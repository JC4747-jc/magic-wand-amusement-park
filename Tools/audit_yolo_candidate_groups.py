"""Bounded provenance/sequence/perceptual grouping audit. Never trains or assigns splits."""
from collections import Counter, defaultdict
import hashlib
import json
import math
import os
from pathlib import Path
import re
import time

os.environ.setdefault('OMP_NUM_THREADS', '1')
os.environ.setdefault('OPENBLAS_NUM_THREADS', '1')
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '.codex-tmp/yolo-training/candidate-group-audit.json'
Image.MAX_IMAGE_PIXELS = 16_000_000
CAP_BYTES = 256 * 1024**2
CAP_IMAGES = 1000


def sha(data):
    return hashlib.sha256(data).hexdigest()


def bit_hash(values):
    return int.from_bytes(np.packbits(values.reshape(-1)).tobytes(), 'big')


def main():
    started = time.monotonic()
    records = {}
    manifests = []
    for name in ('candidate-p20.json', 'potted-p40.json'):
        path = ROOT / '.codex-tmp/yolo-training/openimages-review' / name
        raw = path.read_bytes()
        manifests.append({'path': path.relative_to(ROOT).as_posix(), 'sha256': sha(raw)})
        for r in json.loads(raw)['records']:
            if r['downloaded']:
                metadata = r['official_metadata']
                records['oi-' + r['image_id']] = {'key': 'oi-' + r['image_id'], 'path': r['local_path'], 'sha256': r['sha256'],
                    'classes': sorted({a['canonical_candidate'] for a in r['annotations']}),
                    'provenance_groups': ['flickr-photographer-' + metadata['AuthorProfileURL']], 'source': 'OpenImages'}
    path = ROOT / 'TrainingData/public-gap-annotations-v1.json'
    raw = path.read_bytes()
    manifests.append({'path': path.relative_to(ROOT).as_posix(), 'sha256': sha(raw)})
    gap_metadata = json.loads((ROOT / '.codex-tmp/yolo-training/gap-sources/candidate-index.json').read_text(encoding='utf8'))
    commons = {r['page_id']: r for r in gap_metadata['bronze_candidates']}
    for r in json.loads(raw)['records']:
        groups = [r['capture_group']]
        if r['source_key'].startswith('commons-'):
            metadata = commons[int(r['source_key'].split('-')[1])]['image_info']['extmetadata']
            author = re.sub('<[^>]+>', '', metadata.get('Artist', {}).get('value', '')).strip().casefold()
            day = metadata.get('DateTimeOriginal', {}).get('value', '').split(' ')[0]
            if author and day:
                groups.append('commons-photographer-day-' + author + ':' + day)
        records[r['source_key']] = {'key': r['source_key'], 'path': r['local_path'], 'sha256': r['sha256'],
            'classes': [r['canonical_candidate']], 'provenance_groups': groups,
            'source': 'iNaturalist' if r['source_key'].startswith('inat-') else 'Commons', 'annotation_status': r['status']}
    for split in ('train', 'valid', 'test'):
        for path in sorted((ROOT / 'Find lighter/detect' / split / 'images').glob('*.jpg')):
            stem = path.name.split('.rf.')[0]
            if re.match(r'^pico_frame_\d+(?:[_-]|$)', stem):
                # No original session IDs: numbering alone cannot establish separate captures.
                group = 'lighter-pico-frame-sequence-UNRESOLVED_SOURCE_SESSION'
            else:
                group = 'lighter-source-' + stem
            key = 'lighter-' + split + '-' + path.name
            records[key] = {'key': key, 'path': path.relative_to(ROOT).as_posix(), 'classes': ['Lighter'],
                'source': 'ExistingLighter', 'existing_split': split, 'provenance_groups': [group]}
    assert 0 < len(records) <= CAP_IMAGES
    parent = {key: key for key in records}
    def find(key):
        while parent[key] != key:
            parent[key] = parent[parent[key]]
            key = parent[key]
        return key
    def union(a, b):
        a, b = find(a), find(b)
        if a != b:
            parent[max(a, b)] = min(a, b)
    owners = {}
    for r in records.values():
        for group in r['provenance_groups']:
            if group in owners:
                union(r['key'], owners[group])
            else:
                owners[group] = r['key']
    size = 0
    k = np.arange(32)[:, None]
    n = np.arange(32)[None, :]
    basis = np.cos(np.pi / 32 * (n + .5) * k) * math.sqrt(2 / 32)
    basis[0] /= math.sqrt(2)
    pixels = defaultdict(list)
    for r in records.values():
        assert time.monotonic() - started < 180
        path = ROOT / r['path']
        raw = path.read_bytes()
        size += len(raw)
        assert size <= CAP_BYTES
        actual = sha(raw)
        if r.get('sha256'):
            assert actual == r['sha256'], 'Photograph changed: ' + r['key']
        r['sha256'] = actual
        with Image.open(path) as image:
            image.load()
            assert image.width * image.height <= Image.MAX_IMAGE_PIXELS
            assert image.getexif().get(274, 1) == 1
            rgb = image.convert('RGB')
            r['size'] = list(image.size)
            r['pixel_sha256'] = sha(str(rgb.size).encode() + rgb.tobytes())
            gray = rgb.convert('L')
            small = np.asarray(gray.resize((9, 8), Image.Resampling.LANCZOS), dtype=np.int16)
            r['dhash64'] = format(bit_hash(small[:, 1:] > small[:, :-1]), '016x')
            small = np.asarray(gray.resize((32, 32), Image.Resampling.LANCZOS), dtype=np.float64)
            coefficients = (basis @ small @ basis.T)[:8, :8]
            bits = coefficients > np.median(coefficients.reshape(-1)[1:])
            bits[0, 0] = False
            r['phash64'] = format(bit_hash(bits), '016x')
        pixels[r['pixel_sha256']].append(r['key'])
    exact = [keys for keys in pixels.values() if len(keys) > 1]
    for keys in exact:
        for key in keys[1:]:
            union(keys[0], key)
    near = []
    items = list(records.values())
    for i, a in enumerate(items):
        for b in items[i+1:]:
            if a['pixel_sha256'] == b['pixel_sha256']:
                continue
            ar = a['size'][0] / a['size'][1]
            br = b['size'][0] / b['size'][1]
            if abs(math.log(ar/br)) > .15:
                continue
            dh = (int(a['dhash64'], 16) ^ int(b['dhash64'], 16)).bit_count()
            ph = (int(a['phash64'], 16) ^ int(b['phash64'], 16)).bit_count()
            if dh <= 10 and ph <= 8:
                near.append({'a': a['key'], 'b': b['key'], 'dhash_distance': dh, 'phash_distance': ph,
                             'decision': 'CONSERVATIVELY_GROUPED_NOT_CONFIRMED_DUPLICATE'})
                union(a['key'], b['key'])
    components = defaultdict(list)
    for r in items:
        r['component_id'] = find(r['key'])
        components[r['component_id']].append(r['key'])
    cross = []
    for component, keys in components.items():
        splits = sorted({records[key]['existing_split'] for key in keys if 'existing_split' in records[key]})
        if len(splits) > 1:
            cross.append({'component_id': component, 'existing_splits': splits, 'images': len(keys)})
    groups_per_class = defaultdict(set)
    for r in items:
        for category in r['classes']:
            groups_per_class[category].add(r['component_id'])
    report = {'purpose': 'CANDIDATE_GROUP_AUDIT_NOT_TRAINING_OR_SPLIT_ACCEPTANCE', 'source_manifests': manifests,
        'image_count': len(items), 'image_bytes': size, 'sources': dict(Counter(r['source'] for r in items)),
        'exact_pixel_duplicate_groups': exact, 'perceptual_candidate_pairs': near, 'components': dict(components),
        'conservative_groups_per_candidate_class': {k: len(v) for k,v in groups_per_class.items()},
        'existing_split_cross_group_risks': cross, 'split_assigned': False,
        'limits': {'images': CAP_IMAGES, 'image_bytes': CAP_BYTES, 'decoded_pixels': Image.MAX_IMAGE_PIXELS, 'seconds': 180},
        'limitations': ['pHash/dHash are screening heuristics, not proof of different physical objects or capture independence.',
            'OpenImages is grouped by photographer conservatively; Commons combines artifact and photographer/day; iNaturalist uses observer/day.',
            'All existing pico_frame sequences lack independent session IDs; keep in one unresolved group rather than accept their original train/valid/test split.',
            'Counts include quarantined/unreviewed candidates and are not eligible training coverage.'], 'records': items}
    OUT.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf8')
    print(json.dumps({k:v for k,v in report.items() if k in ('image_count','image_bytes','sources','conservative_groups_per_candidate_class','existing_split_cross_group_risks','split_assigned')}))
    print('perceptual_pairs=' + str(len(near)) + ' exact_duplicate_groups=' + str(len(exact)))


if __name__ == '__main__':
    main()
