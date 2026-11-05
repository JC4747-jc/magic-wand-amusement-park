"""Bounded rear-view Statue experiment with service-shaped VAL checkpoint selection."""
import hashlib
import json
import shutil
from pathlib import Path
import torch
import ultralytics
from ultralytics import YOLO
from ultralytics.utils.torch_utils import strip_optimizer
from statue_flat_confusers_v39_selection import ExpandedValidationSelection
from dataset_paths import manifest_relative_path

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    data = ROOT / 'Datasets/statue-flat-confusers-v38'
    assert sha(data/'audit.json') == '7a5c03ca6f1a75cac5338f8b76fbf992ff136441d4fa2580c46a3124929e3f95'
    initializer = ROOT / 'Reports/statue-oiv7m-single-class-extraction/initializer.pt'
    out = ROOT / 'Reports/statue-oiv7m-flat-confusers-v39-training'
    assert not out.exists()
    audit = json.loads((data/'audit.json').read_text(encoding='utf-8'))
    assert audit['training_approved'] and not (data/'review_hold.json').exists()
    paths = [data/'audit.json', data/'data.yaml', initializer, Path(__file__).resolve(),
             ROOT/'Tools/MultiScene/statue_flat_confusers_v39_selection.py',
             ROOT/'Tools/MultiScene/evaluate_mapped.py', ROOT/'Tools/MultiScene/dataset_paths.py',
             ROOT/'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt']
    for row in audit['samples']:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            path = data / manifest_relative_path(row[key])
            assert sha(path) == row[digest]
            paths.append(path)
    plan = ROOT/'Reports/statue-flat-confusers-v39-sampling-plan.json'
    assert json.loads(plan.read_text())['dataset_audit_sha256'] == sha(data/'audit.json')
    paths.extend([plan, ROOT/'Tools/MultiScene/hard_positive_world_trainer.py', ROOT/'Tools/MultiScene/hard_positive_sampler.py'])
    paths.extend([ROOT/'Tools/MultiScene/preserved_medium_statue_v39_trainer.py', ROOT/'Tools/MultiScene/extract_oiv7m_statue_head.py', ROOT/'Reports/statue-oiv7m-single-class-extraction/report.json', ROOT/'yolov8m-oiv7.pt'])
    parent = ROOT/'Datasets/statue-articulated-release-v36'
    assert sha(parent/'audit.json') == audit['parent_audit_sha256']
    for row in json.loads((parent/'audit.json').read_text())['samples']:
        for key in ['image','label']:
            assert sha(parent/manifest_relative_path(row[key])) == sha(data/manifest_relative_path(row[key]))
    for relative, digest in audit['context_evidence'].items():
        path = ROOT/relative
        assert sha(path) == digest
        paths.append(path)
    paths.extend([parent/'audit.json',ROOT/'Tools/MultiScene/prepare_statue_hands_v26.py',ROOT/'Tools/MultiScene/prepare_statue_groups_v25.py'])
    paths.extend([ROOT/'Tools/MultiScene/prepare_statue_validation_extension_v32.py', ROOT/'Tools/MultiScene/test_statue_expanded_validation.py', data/'annotation_release.json'])
    paths.extend([ROOT/'Reports/statue-flat-confusers-v39-pretraining-policy.json', ROOT/'Tools/MultiScene/statue_expanded_validation_selection.py', ROOT/'Datasets/statue-hands-v26/audit.json', ROOT/'Reports/statue-expanded-val-v33-pretraining-policy.json', ROOT/'Tools/MultiScene/statue_runtime_checkpoint_selection.py'])
    frozen = [dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in paths]
    options = dict(data=str(data/'data.yaml'), device=0, epochs=20, batch=4, workers=0,
        imgsz=640, seed=4747, deterministic=True, freeze=0, optimizer='AdamW',
        lr0=.0001, lrf=.1, warmup_bias_lr=.0001, mosaic=0., mixup=0., cutmix=0.,
        scale=.1, translate=.05, hsv_h=.01, hsv_s=.3, hsv_v=.2, close_mosaic=0,
        project=str(out), name='train', plots=True)
    out.mkdir()
    (out/'run.json').write_text(json.dumps(dict(dataset_version=sha(data/'audit.json'),
        initializer=str(initializer), initializer_sha256=sha(initializer), parameters=options,
        torch=torch.__version__, ultralytics=ultralytics.__version__, frozen_inputs=frozen,
        scope='Statue visual category; does not establish historical identity or 3D pose.',
        comparability='More TRAIN data/exposures vs v33(80/82); combined data intervention, not isolated single-negative ablation or equal compute. Same raw initializer/options and19VAL policy. TRAIN88/90exposures;one20epoch trial;no TEST selection or deployment acceptance.',
        service_routed=False, deployment_accepted=False), indent=2))
    selector = ExpandedValidationSelection(data, out/'runtime-selection')
    model = YOLO(initializer)
    model.add_callback('on_model_save', selector)
    from preserved_medium_statue_v39_trainer import PreservedMediumTrainer
    model.train(trainer=PreservedMediumTrainer, **options)
    assert len(selector.records) == 20
    best = out/'train/weights/best.pt'
    shutil.copy2(best, out/'native-fitness-best.pt')
    selected = out/'runtime-selection/selected-stripped.pt'
    shutil.copy2(selector.best, selected)
    strip_optimizer(selected)
    shutil.copy2(selected, best)
    for item in frozen:
        assert sha(ROOT/item['path']) == item['sha256']
    (out/'completion.json').write_text(json.dumps(dict(status='TRAINING_COMPLETED_PENDING_FINAL_VAL',
        sha256=sha(best), raw_selected_sha256=sha(selector.best), rank=selector.best_rank,
        frozen_inputs_verified=len(frozen), service_routed=False, deployment_accepted=False), indent=2))


if __name__ == '__main__':
    main()
