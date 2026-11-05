"""Repeat the eight already-observed office negatives; never claim fresh TEST."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', required=True)
    parser.add_argument('--out', required=True)
    parser.add_argument('--imgsz', type=int, default=640)
    args = parser.parse_args()
    if args.imgsz < 32 or args.imgsz % 32:
        parser.error('imgsz must be a positive multiple of 32')
    weight, out = Path(args.model).resolve(), Path(args.out).resolve()
    if out.exists():
        raise FileExistsError(out)
    prior = ROOT / 'Reports/statue-reviewed-v7-decision.json'
    evidence = json.loads(prior.read_text(encoding='utf-8'))['negative_rows']
    if len(evidence) != 8 or len({r['sha256'] for r in evidence}) != 8:
        raise ValueError('Expected exactly eight previously observed images')
    data = ROOT / 'Datasets/pico-tree-negative-heldout-v1'
    for row in evidence:
        if sha(data / row['image']) != row['sha256']:
            raise ValueError('Observed image changed')
    from ultralytics import YOLO
    digest = sha(weight)
    model = YOLO(weight)
    ids = [i for i, name in model.names.items() if name.lower() in ('statue', 'sculpture')]
    if not ids:
        raise ValueError('Checkpoint has no Statue class')
    rows = []
    for row in evidence:
        result = model.predict(str(data / row['image']), imgsz=args.imgsz, rect=True,
                               conf=0.001, iou=0.7, max_det=32, classes=ids,
                               device='cpu', verbose=False)[0]
        scores = result.boxes.conf.tolist()
        rows.append(dict(image=row['image'], sha256=row['sha256'], confidence=scores,
                         false_positives=sum(s >= 0.65 for s in scores)))
    if sha(weight) != digest:
        raise RuntimeError('Weight changed during inference')
    report = dict(scope='Repeated observed office negative regression, not fresh independent test',
                  sha256=digest, prior_evidence_sha256=sha(prior), images=8,
                  operating_conf=0.65, imgsz=args.imgsz, rect=True, max_det=32, nms_iou=0.7,
                  rows=rows, false_positives=sum(r['false_positives'] for r in rows),
                  ground_truth_instances=0, native_target_classes={i: model.names[i] for i in ids},
                  independent_test_evaluated=False, service_routed=False)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({'images': 8, 'false_positives': report['false_positives'], 'sha256': digest}))


if __name__ == '__main__':
    main()
