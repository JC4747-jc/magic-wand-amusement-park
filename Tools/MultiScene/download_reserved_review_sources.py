"""Download immutable review candidates with an explicit, checked fold reservation."""
import argparse
import concurrent.futures
import hashlib
import io
import json
from pathlib import Path
import requests
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', required=True)
    parser.add_argument('--out', required=True)
    parser.add_argument('--reserved-split', choices=['train', 'val', 'test'], required=True)
    args = parser.parse_args()
    plan_path = (ROOT / args.plan).resolve()
    output = (ROOT / args.out).resolve()
    assert output.is_relative_to(ROOT / 'Datasets')
    plan = json.loads(plan_path.read_text())
    assert plan['samples'] and all(row['reserved_split'] == args.reserved_split for row in plan['samples'])
    output.mkdir(exist_ok=False)
    (output / 'images').mkdir()
    (output / 'selection.json').write_bytes(plan_path.read_bytes())

    def fetch(row):
        try:
            response = requests.get(row['download_url'], timeout=(10, 25))
            response.raise_for_status()
            raw = response.content
            with Image.open(io.BytesIO(raw)) as image:
                image.verify()
            name = row['original_id'] + '.jpg'
            assert Path(name).name == name
            (output / 'images' / name).write_bytes(raw)
            return dict(**row, image='images/' + name, sha256=hashlib.sha256(raw).hexdigest())
        except Exception as exc:
            return dict(original_id=row['original_id'], reserved_split=args.reserved_split,
                        error=type(exc).__name__, detail=str(exc)[:350])

    rows, failures = [], []
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        for result in pool.map(fetch, plan['samples']):
            (rows if 'image' in result else failures).append(result)
            print(result['original_id'], 'DOWNLOADED' if 'image' in result else 'FAILED', flush=True)
    manifest = dict(source='openimages-v7', upstream_split='test', project_reserved_split=args.reserved_split,
        selection_sha256=hashlib.sha256(plan_path.read_bytes()).hexdigest(),
        sources=[dict(id='openimages-v7', url='https://storage.googleapis.com/openimages/web/download_v7.html',
                      license='Per-image metadata CC BY2.0; attribution retained')],
        samples=rows, failures=failures, training_approved=False, evaluation_ready=False,
        scope='Unreviewed source photos only; immutable fold reservation. No labels or model predictions generated.')
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps(dict(downloaded=len(rows), failed=len(failures), reserved_split=args.reserved_split)))


if __name__ == '__main__':
    main()
