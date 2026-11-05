"""Bounded confuser-background fine tune; no TEST selection or service promotion."""
import hashlib
import json
from pathlib import Path
import torch
import ultralytics
from ultralytics import YOLO

ROOT = Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    data = ROOT / 'Datasets/cockroach-positive-train-v16'
    out = ROOT / 'Reports/cockroach-positive-train-v16-training'
    initializer = ROOT / 'object-detection-demo/weights/candidates/cockroach-diverse-v15/best.pt'
    assert not out.exists(), 'Inspect existing run instead of restarting'
    assert sha(data / 'audit.json') == 'ea5662758384ef064c530f0d1b0b1a4abdc48e1d3d458aa2d17d82ce9724795a'
    audit = json.loads((data / 'audit.json').read_text())
    assert audit['training_approved'] and audit['counts'] == dict(train=200, val=39, test=9)
    release = ROOT / 'Reports/cockroach-diverse-negative-v15-release.json'
    assert sha(release) == audit['background_release_sha256']
    for row in audit['samples']:
        assert sha(data / row['image']) == row['sha256']
        assert sha(data / row['label']) == row['label_sha256']
    assert sha(initializer) == '088e0fc7f65fb8e157f10178dde6af0fa9946be51b722886d9eaed12124ea901'
    assert sha(ROOT / 'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt') == 'fd4a31001feb66127a8c527ffb1e0ba32bfe9f72566cfde4f2f9b5fc0c3e7ca7'
    assert torch.cuda.is_available()
    model = YOLO(initializer)
    assert list(model.names.values()) == ['Cockroach', 'Beetle']
    options = dict(data=str(data / 'data.yaml'), device=0, epochs=30, imgsz=640,
        batch=4, workers=0, seed=20261105, deterministic=True, freeze=0,
        optimizer='AdamW', lr0=.0001, lrf=.1, warmup_bias_lr=.0001,
        mosaic=0., mixup=0., cutmix=0., scale=.3, translate=.05,
        hsv_h=.01, hsv_s=.3, hsv_v=.2, close_mosaic=0,
        project=str(out), name='train', plots=True)
    spec = dict(dataset_version=sha(data / 'audit.json'), initializer_sha256=sha(initializer),
        script_sha256=sha(Path(__file__)), background_release_sha256=sha(release), parameters=options,
        torch=torch.__version__, ultralytics=ultralytics.__version__, cuda_device=torch.cuda.get_device_name(0),
        scope='Two-class public-photo pilot; 23 new observer-separated reviewed TRAIN positives added',
        hypothesis='Additional real positive views may improve generalization; fixed runtime recall and negative regression must be measured',
        unity_mapping={'Cockroach': 'ParkBugs', 'Beetle': None}, service_routed=False, pico_validated=False,
        selection='Ultralytics validation fitness only; not an isolated data-only ablation',
        evaluation_protocol=dict(confidence=.65, imgsz=640, rect=True, nms_iou=.7, max_det=8, allowed_classes=['Cockroach']),
        gates=dict(precision_min=.9, recall_min=.8, negative_false_positives_max=0),
        test_policy='No observed TEST threshold tuning; fresh independent acceptance required after validation gates pass')
    out.mkdir()
    (out / 'run.json').write_text(json.dumps(spec, indent=2))
    model.train(**options)


if __name__ == '__main__':
    main()
