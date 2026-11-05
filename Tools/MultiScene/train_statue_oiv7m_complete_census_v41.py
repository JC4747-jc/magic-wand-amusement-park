"""Bounded rear-view Statue experiment with service-shaped VAL checkpoint selection."""
import hashlib
import json
import shutil
from pathlib import Path
import torch
import ultralytics
from ultralytics import YOLO
from ultralytics.utils.torch_utils import strip_optimizer
from statue_complete_census_v41_selection import ExpandedValidationSelection
from dataset_paths import manifest_relative_path

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    data = ROOT / 'Datasets/statue-complete-census-v40'
    assert sha(data/'audit.json') == '458b5c0f0c4d420979b877368e6a11887c8069645d6ef209c7ed9e6dc2c3ee59'
    initializer = ROOT / 'Reports/statue-oiv7m-single-class-extraction/initializer.pt'
    out = ROOT / 'Reports/statue-oiv7m-complete-census-v41-training'
    assert not out.exists()
    audit = json.loads((data/'audit.json').read_text(encoding='utf-8'))
    assert audit['training_approved'] and not (data/'review_hold.json').exists()
    paths = [data/'audit.json', data/'data.yaml', initializer, Path(__file__).resolve(),
             ROOT/'Tools/MultiScene/statue_complete_census_v41_selection.py',
             ROOT/'Tools/MultiScene/evaluate_mapped.py', ROOT/'Tools/MultiScene/dataset_paths.py',
             ROOT/'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt']
    for row in audit['samples']:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            path = data / manifest_relative_path(row[key])
            assert sha(path) == row[digest]
            paths.append(path)
    plan = ROOT/'Reports/statue-complete-census-v41-sampling-plan.json'
    assert json.loads(plan.read_text())['dataset_audit_sha256'] == sha(data/'audit.json')
    paths.extend([plan, ROOT/'Tools/MultiScene/hard_positive_world_trainer.py', ROOT/'Tools/MultiScene/hard_positive_sampler.py'])
    paths.extend([ROOT/'Tools/MultiScene/preserved_medium_statue_v41_trainer.py', ROOT/'Tools/MultiScene/extract_oiv7m_statue_head.py', ROOT/'Reports/statue-oiv7m-single-class-extraction/report.json', ROOT/'yolov8m-oiv7.pt'])
    parent = ROOT/'Datasets/statue-flat-confusers-v38'
    assert sha(parent/'audit.json') == audit['parent_audit_sha256']
    for row in json.loads((parent/'audit.json').read_text())['samples']:
        for key in ['image','label']:
            before=parent/manifest_relative_path(row[key]);after=data/manifest_relative_path(row[key])
            if key=='label' and row['sha256']=='e2ce7aaa49ea84334e86ffb9a4ab20124499df5ce0492fd7cc5d12181149c2ae':
                assert row['split']=='val' and after.read_bytes().startswith(before.read_bytes())
                assert len(before.read_bytes().splitlines())==14 and len(after.read_bytes().splitlines())==21
            else:assert sha(before)==sha(after)
    for relative, digest in audit['context_evidence'].items():
        path = ROOT/relative
        assert sha(path) == digest
        paths.append(path)
    paths.extend([parent/'audit.json',ROOT/'Tools/MultiScene/prepare_statue_hands_v26.py',ROOT/'Tools/MultiScene/prepare_statue_groups_v25.py'])
    paths.extend([ROOT/'Tools/MultiScene/prepare_statue_validation_extension_v32.py', ROOT/'Tools/MultiScene/test_statue_expanded_validation.py', data/'annotation_release.json'])
    paths.extend([ROOT/'Reports/statue-complete-census-v41-pretraining-policy.json', ROOT/'Reports/statue-complete-census-v41-prospective-policy.json', ROOT/'Tools/MultiScene/test_statue_complete_census_v41.py', ROOT/'Tools/MultiScene/statue_expanded_validation_selection.py', ROOT/'Datasets/statue-hands-v26/audit.json', ROOT/'Reports/statue-expanded-val-v33-pretraining-policy.json', ROOT/'Tools/MultiScene/statue_runtime_checkpoint_selection.py'])
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
        comparability='One VAL label gains seven manually reviewed visible instances,33->40GT. TRAIN images/labels, sampling options and thresholds unchanged. Validation feedback/checkpoint policy uses corrected labels, so entire training execution need not equal v39. No TEST selection or automatic deployment.',
        service_routed=False, deployment_accepted=False), indent=2))
    selector = ExpandedValidationSelection(data, out/'runtime-selection')
    model = YOLO(initializer)
    model.add_callback('on_model_save', selector)
    from preserved_medium_statue_v41_trainer import PreservedMediumTrainer
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
