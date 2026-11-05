"""Bounded public-data fine-tune preserving the extracted official head topology."""
from copy import deepcopy
import argparse
import hashlib
import json
from pathlib import Path

import torch
import ultralytics
from ultralytics import YOLO
from ultralytics.models.yolo.detect import DetectionTrainer

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Reports/statue-oiv7n-preserved-v12-training'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class PreservedHeadTrainer(DetectionTrainer):
    target_name = 'Statue'
    def get_model(self, cfg=None, weights=None, verbose=True):
        if weights is None or type(weights).__name__ != 'DetectionModel':
            raise ValueError('Requires a serialized DetectionModel initializer')
        if self.data['nc'] != 1 or weights.names != {0: self.target_name} or self.data['names'] != {0: self.target_name}:
            raise ValueError('Requires matching reviewed single-class dataset and initializer')
        model = deepcopy(weights).float()
        head = model.model[-1]
        if type(head).__name__ != 'Detect' or head.nc != 1 or head.end2end:
            raise ValueError('Unexpected detection head')
        if any(b[-1].in_channels != 100 or b[-1].out_channels != 1 for b in head.cv3):
            raise ValueError('Classification topology was not preserved')
        for key, value in model.state_dict().items():
            if not torch.equal(value, weights.state_dict()[key].float()):
                raise ValueError(f'Initialization mismatch: {key}')
        report = dict(status='PASS', tensors=len(model.state_dict()),
                      class_hidden_width=100, scope='Actual trainer model construction before optimizer setup')
        (OUT / 'trainer-initializer-verification.json').write_text(json.dumps(report, indent=2))
        return model


def main():
    global OUT
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--target', choices=['statue', 'tree'], default='statue')
    parser.add_argument('--tree-unfrozen', action='store_true', help='Paired Tree experiment: freeze=0, all other training settings unchanged')
    parser.add_argument('--tree-low-scale', action='store_true', help='Paired Tree experiment: scale=.1 instead of .7')
    args = parser.parse_args()
    if args.tree_unfrozen and args.target != 'tree':
        parser.error('--tree-unfrozen requires --target tree')
    if args.tree_low_scale and (args.target != 'tree' or args.tree_unfrozen):
        parser.error('--tree-low-scale requires tree and cannot combine with unfreezing')
    target_name, dataset_name, expected_hash = {
        'statue':('Statue','statue-small-positives-v12','27e3cafc35397d1ba45bdc00f88f68cbed09daff0261c50505ba3a7763e46be8'),
        'tree':('Tree','tree-windswept-positives-v11','9196b6a2fa8c22d34d808a3e1a7f903f4275121950c837bcabeab794514782fb')}[args.target]
    if args.target == 'tree':
        OUT = ROOT / 'Reports/tree-oiv7n-preserved-v11-training'
        if args.tree_unfrozen:
            OUT = ROOT / 'Reports/tree-oiv7n-unfrozen-v12-training'
        if args.tree_low_scale:
            OUT = ROOT / 'Reports/tree-oiv7n-low-scale-v13-training'
    PreservedHeadTrainer.target_name = target_name
    if OUT.exists():
        raise FileExistsError('Run already exists; inspect authoritative process before any resume')
    init = ROOT / f'Reports/{args.target}-oiv7n-single-class-extraction/initializer.pt'
    extraction = ROOT / f'Reports/{args.target}-oiv7n-single-class-extraction/report.json'
    evidence = json.loads(extraction.read_text(encoding='utf-8'))
    if evidence['status'] != 'PASS' or evidence['initializer_sha256'] != sha(init):
        raise ValueError('Initializer extraction evidence mismatch')
    data = ROOT / 'Datasets' / dataset_name
    audit = json.loads((data / 'audit.json').read_text(encoding='utf-8'))
    if audit.get('training_approved') is False or (data / 'review_hold.json').exists():
        raise ValueError('Dataset not released or review hold present')
    if sha(data / 'audit.json') != expected_hash:
        raise ValueError('Unexpected dataset version')
    tree_baseline = None
    if args.target == 'tree':
        tree_baseline = ROOT / 'Reports/tree-oiv7n-preserved-v11-initial-runtime-val/report.json'
        baseline = json.loads(tree_baseline.read_text(encoding='utf-8'))
        expected = dict(sha256=sha(init), dataset_version=expected_hash, split='val',
                        imgsz=640, rect=True, operating_confidence=.65, nms_iou=.7, max_det=8)
        if any(baseline.get(k) != v for k,v in expected.items()):
            raise ValueError('Tree initializer baseline mismatch')
    OUT.mkdir()
    options = dict(data=str(data / 'data.yaml'), device='cpu', epochs=20, batch=4,
                   workers=0, imgsz=640, seed=4747, deterministic=True, freeze=10,
                   optimizer='AdamW', lr0=.0001, lrf=.1, warmup_bias_lr=.0001,
                   mosaic=0., mixup=0., cutmix=0., scale=.1, translate=.05,
                   hsv_h=.01, hsv_s=.3, hsv_v=.2, close_mosaic=0,
                   project=str(OUT), name='train', plots=True)
    if args.target == 'tree':
        options.update(seed=20261103, scale=.7)
        if args.tree_unfrozen:
            options.update(freeze=0)
        if args.tree_low_scale:
            options.update(scale=.1)
    specification = dict(initializer=str(init), initializer_sha256=sha(init),
                         dataset_version=sha(data / 'audit.json'), parameters=options,
                         script_sha256=sha(Path(__file__)), extraction_report_sha256=sha(extraction),
                         torch=torch.__version__, ultralytics=ultralytics.__version__,
                         scope=f'Official OIV7 {target_name}-head initialization, public pilot only',
                         gates=dict(val_recall_min=.8, office_false_positives_max=0,
                                    conf=.65, imgsz=640, rect=True, nms_iou=.7, max_det=32),
                         target_name=target_name,
                         experiment_comparability='Architecture/initializer intervention; not a pure capacity ablation',
                         test_policy='No fresh TEST unless validation and observed-office gates pass',
                         service_routed=False, pico_validated=False)
    if args.target == 'tree':
        specification['gates'].update(max_det=8, val_precision_min=.9, val_background_false_positive_images_max=0)
        specification['initial_runtime_report_sha256'] = sha(tree_baseline)
        if args.tree_unfrozen:
            specification['experiment_comparability'] = 'Paired with preserved-v11: freeze=10 to freeze=0 only; same initializer/data/seed/20 epochs'
            specification['rationale'] = 'TRAIN-only low matching confidence despite IoU above .5; test samples never used for training or checkpoint selection'
            specification['test_policy'] = 'Existing independent TEST sealed; no checkpoint selection or threshold tuning from it. Fresh acceptance evidence needed.'
        if args.tree_low_scale:
            transform_audit = ROOT / 'Reports/tree-v11-actual-transform-audit.json'
            specification['transform_audit_sha256'] = sha(transform_audit)
            specification['experiment_comparability'] = 'Paired with preserved-v11: scale=.7 to .1 only; same initializer/data/seed/freeze10/20 epochs'
            specification['test_policy'] = 'No existing independent TEST selection or tuning; fresh acceptance evidence required'
    (OUT / 'run.json').write_text(json.dumps(specification, indent=2))
    model = YOLO(init)
    model.train(trainer=PreservedHeadTrainer, **options)


if __name__ == '__main__':
    main()
