"""Compare a source manifest against current local dataset pixels; never release data."""
import argparse
import hashlib
import itertools
import json
from pathlib import Path
from PIL import Image


def scan(source, datasets, out):
    source, datasets, out = Path(source).resolve(), Path(datasets).resolve(), Path(out).resolve()
    if out.exists():
        raise ValueError('Preserve existing reports; choose a new output')
    manifest = source / 'manifest.json'
    rows = json.loads(manifest.read_text(encoding='utf-8-sig'))['samples']
    cache = {}
    def fingerprint(path):
        raw = path.read_bytes()
        encoded = hashlib.sha256(raw).hexdigest()
        if encoded not in cache:
            with Image.open(path) as im:
                rgb = im.convert('RGB')
                pixel = hashlib.sha256(f'RGB:{rgb.width}:{rgb.height}:'.encode()+rgb.tobytes()).hexdigest()
                gray = rgb.convert('L').resize((9,8), Image.Resampling.LANCZOS).tobytes()
                dh = 0
                for y in range(8):
                    for x in range(8):
                        dh = (dh << 1) | int(gray[y*9+x] > gray[y*9+x+1])
            cache[encoded] = pixel, dh
        return encoded, *cache[encoded]
    candidates = []
    for row in rows:
        path = (source / row['image']).resolve()
        if not path.is_relative_to(source):
            raise ValueError('Source image escapes manifest directory')
        fp = fingerprint(path)
        if fp[0] != row['sha256']:
            raise ValueError('Source manifest hash mismatch')
        candidates.append((row.get('page_id', row['image']), fp))
    def match(a, b):
        distance = (a[2] ^ b[2]).bit_count()
        if a[0] == b[0] or a[1] == b[1] or distance <= 4:
            return dict(encoded_exact=a[0] == b[0], decoded_exact=a[1] == b[1], dhash_distance=distance)
        return None
    internal = []
    for (aid, a), (bid, b) in itertools.combinations(candidates, 2):
        result = match(a,b)
        if result is not None:
            internal.append(dict(first=aid, second=bid, **result))
    paths = sorted(p for p in datasets.rglob('*') if p.is_file() and not p.is_relative_to(source)
                   and p.suffix.lower() in {'.jpg','.jpeg','.png','.webp','.bmp','.tif','.tiff'})
    errors, matches, scanned = [], [], 0
    print('Historical paths', len(paths), flush=True)
    for path in paths:
        try:
            fp = fingerprint(path)
        except (OSError, ValueError) as exc:
            errors.append(dict(path=str(path), error=type(exc).__name__))
            continue
        scanned += 1
        for candidate_id, candidate in candidates:
            result = match(candidate, fp)
            if result is not None:
                matches.append(dict(candidate_id=candidate_id, historical_image=str(path.relative_to(datasets)), historical_sha256=fp[0], **result))
        if scanned % 3000 == 0:
            print('Scanned', scanned, 'unique encoded', len(cache), flush=True)
    report = dict(source=str(source), datasets=str(datasets), source_manifest_sha256=hashlib.sha256(manifest.read_bytes()).hexdigest(),
                  tool_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), candidates=len(candidates),
                  enumerated_paths=len(paths), scanned_paths=scanned, unique_encoded=len(cache), errors=errors,
                  matches=matches, internal_pair_comparisons=len(candidates)*(len(candidates)-1)//2, internal_matches=internal,
                  training_approved=False, method='Current files: encoded SHA256, decoded RGB SHA256, horizontal64bit dHash distance<=4',
                  limitations=['Near matches need visual review; crops/viewpoint/physical-instance aliases may escape detection.',
                               'Missing historical files and Reports-only reservations require separate metadata audit.'])
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(dict(scanned=scanned, errors=len(errors), matches=len(matches), internal_matches=len(internal))), flush=True)
    return report

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True)
    parser.add_argument('--datasets', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    scan(args.source, args.datasets, args.out)
