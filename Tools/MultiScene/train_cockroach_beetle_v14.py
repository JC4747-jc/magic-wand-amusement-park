"""Bounded two-class native detector experiment, no TEST model selection."""
import hashlib
import json
from pathlib import Path
import torch
import ultralytics
from ultralytics import YOLO

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    data = ROOT / 'Datasets/cockroach-beetle-v14'
    out = ROOT / 'Reports/cockroach-beetle-v14-training'
    initializer = ROOT / 'Reports/pretrained-yolov8m/yolov8m.pt'
    assert not out.exists(), 'Inspect existing run before restarting'
    release = json.loads((data / 'training-release.json').read_text())
    assert release['training_approved'] and release['audit_sha256'] == sha(data / 'audit.json')
    assert release['background_review_sha256'] == sha(ROOT / 'Reports/beetle-confusers-v14-old-background-review.json')
    audit = json.loads((data / 'audit.json').read_text())
    assert audit['counts'] == dict(train=159, val=39, test=9)
    assert audit['names'] == ['Cockroach', 'Beetle']
    for row in audit['samples']:
        assert sha(data / row['image']) == row['sha256']
        assert sha(data / row['label']) == row['label_sha256']
    assert sha(initializer) == '5d4a90cdc7a21786cc59cd19778e9eafff836df9e2da32524737c7ee6efe4fe5'
    assert sha(ROOT / 'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt') == 'fd4a31001feb66127a8c527ffb1e0ba32bfe9f72566cfde4f2f9b5fc0c3e7ca7'
    assert torch.cuda.is_available()
    options = dict(data=str(data / 'data.yaml'), device=0, epochs=60, imgsz=640,
        batch=4, workers=0, seed=20261105, deterministic=True, freeze=0,
        optimizer='AdamW', lr0=.0005, lrf=.1, warmup_bias_lr=.0005,
        mosaic=.3, mixup=0., cutmix=0., scale=.3, translate=.05,
        hsv_h=.01, hsv_s=.3, hsv_v=.2, close_mosaic=10,
        project=str(out), name='train', plots=True)
    spec = dict(dataset_version=sha(data / 'audit.json'), release_sha256=sha(data / 'training-release.json'),
        initializer_sha256=sha(initializer), script_sha256=sha(Path(__file__)), parameters=options,
        torch=torch.__version__, ultralytics=ultralytics.__version__, cuda_device=torch.cuda.get_device_name(0),
        scope='Periplaneta americana target with auxiliary Beetle including reviewed larval VAL forms',
        unity_mapping={'Cockroach': 'ParkBugs', 'Beetle': None}, service_routed=False, pico_validated=False,
        selection='Ultralytics best validation fitness across both classes; separately gate Cockroach runtime metrics',
        evaluation_protocol=dict(target_class='Cockroach', confidence=.65, imgsz=640, rect=True, nms_iou=.7, max_det=8),
        gates=dict(precision_min=.9, recall_min=.8, negative_false_positives_max=0),
        test_policy='Historical TEST is regression only; no TEST threshold tuning; fresh independent acceptance required')
    out.mkdir()
    (out / 'run.json').write_text(json.dumps(spec, indent=2))
    YOLO(initializer).train(**options)


if __name__ == '__main__':
    main()
