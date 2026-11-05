"""Evaluate completed Tree candidates; no TEST access and no deployment mutation."""
import argparse
import csv
import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--variant', choices=['scale07', 'scale07-lr1e4', 'vegetation-v10-control', 'vegetation-v10', 'windswept-v11', 'oiv7n-preserved-v11', 'oiv7n-unfrozen-v12', 'oiv7n-low-scale-v13', 'oiv7n-small-views-v17'], default='scale07')
    args = parser.parse_args()
    import torch
    paired = args.variant.startswith(('vegetation-v10', 'windswept-v11', 'oiv7n-preserved-v11', 'oiv7n-unfrozen-v12', 'oiv7n-low-scale-v13', 'oiv7n-small-views-v17'))
    prefix = f'tree-{args.variant}' if paired else f'tree-qualified-public-pilot-v6-{args.variant}'
    run = ROOT / f'Reports/{prefix}-training'
    rows = list(csv.DictReader((run / 'train/results.csv').open(encoding='utf-8')))
    if len(rows) != 20 or int(rows[-1]['epoch']) != 20:
        raise RuntimeError('Training has not completed all 20 epochs')
    weight = run / 'train/weights/best.pt'
    for name in ['best.pt', 'last.pt']:
        checkpoint = torch.load(run / 'train/weights' / name, map_location='cpu', weights_only=False)
        if checkpoint.get('epoch') != -1 or checkpoint.get('optimizer') is not None:
            raise RuntimeError('Training finalization not confirmed; do not evaluate a changing checkpoint')
    digest = hashlib.sha256(weight.read_bytes()).hexdigest()
    results = {}
    evaluations = [
        ('runtime-val', 'tree-qualified-public-pilot-v6'),
        ('old-val', 'tree-qualified-public-pilot-v3'),
        ('runtime-regression', 'tree-only-instance-review-v8'),
        ('extension-val', 'tree-public-validation-extension-v1'),
    ]
    if paired:
        dataset = 'tree-qualified-public-pilot-v6' if args.variant.endswith('control') else 'tree-vegetation-negatives-v10'
        if args.variant in ('windswept-v11', 'oiv7n-preserved-v11', 'oiv7n-unfrozen-v12', 'oiv7n-low-scale-v13'):
            dataset = 'tree-windswept-positives-v11'
        if args.variant == 'oiv7n-small-views-v17':
            dataset = 'tree-small-views-v17'
        evaluations = [('runtime-val', dataset), ('train-diagnostic', dataset)]
    for suffix, dataset in evaluations:
        split = 'train' if suffix == 'train-diagnostic' else 'val'
        out = ROOT / f'Reports/{prefix}-{suffix}'
        if out.exists():
            raise FileExistsError(f'Preserve existing evaluation: {out}')
        command = [sys.executable, str(ROOT / 'Tools/MultiScene/evaluate_mapped.py'),
                   '--model', str(weight), '--data', str(ROOT / 'Datasets' / dataset),
                   '--out', str(out), '--split', split, '--imgsz', '640',
                   '--operating-conf', '0.65', '--allowed-classes', 'Tree',
                   '--max-det', '8', '--nms-iou', '0.7']
        subprocess.run(command, cwd=ROOT, check=True)
        report = json.loads((out / 'report.json').read_text(encoding='utf-8'))
        if report['sha256'] != digest or hashlib.sha256(weight.read_bytes()).hexdigest() != digest:
            raise RuntimeError('Model changed during evaluation')
        results[suffix] = report['operating_per_class']
    print(json.dumps({'model_sha256': digest, 'results': results,
                      'test_evaluated': False, 'service_routed': False}))


if __name__ == '__main__':
    main()
