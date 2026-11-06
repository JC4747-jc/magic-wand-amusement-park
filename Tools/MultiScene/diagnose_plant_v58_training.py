"""Compare fixed v58 training fit with validation; never select on TRAIN."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    trainpath = ROOT / 'Reports/plant-v58-training-diagnostic/report.json'
    valpath = ROOT / 'Reports/plant-v58-runtime-selected/final-val/report.json'
    data = ROOT / 'Datasets/plant-label-paths-v52/audit.json'
    train, val, audit = [json.loads(p.read_text(encoding='utf-8')) for p in (trainpath, valpath, data)]
    assert train['images'] == 127 and val['images'] == 30
    assert train['sha256'] == val['sha256'] == 'b2de50026bd92b62a6f3335f6e8a1ab5cf7342d88e8d06dc7ba34562d8f0ebee'
    assert train['dataset_version'] == val['dataset_version'] == sha(data)
    assert train['split'] == 'train' and val['split'] == 'val'
    for key in ('imgsz', 'rect', 'max_det', 'nms_iou', 'allowed_classes', 'operating_confidence'):
        assert train[key] == val[key], key
    lookup = {r['image'].replace('\\', '/'): r for r in train['per_image']}
    sources = []
    for row in audit['samples']:
        if row.get('page_id') in {129746819, 87404883, 134319541, 63438809}:
            assert row['split'] == 'train'
            sources.append(dict(page_id=row['page_id'], **lookup[row['image'].replace('\\', '/')]))
    assert len(sources) == 4
    result = dict(status='FIXED_WEIGHT_TRAIN_FIT_DIAGNOSTIC',
                  training_metrics=train['operating_per_class'], validation_metrics=val['operating_per_class'],
                  dedicated_hanging_source_training_photos=sources,
                  evidence={p.relative_to(ROOT).as_posix(): sha(p) for p in (trainpath, valpath, data)},
                  note='Source63438809 has a hanging basket plus ground urn. TRAIN fit is not held-out accuracy. Counts support diagnosis, not proof of a unique causal mechanism.',
                  labels_changed=False, checkpoint_reselected=False, deployment_accepted=False,
                  test_used=False, pico_validated=False)
    out = ROOT / 'Reports/plant-v58-train-val-diagnosis.json'
    assert not out.exists()
    out.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
