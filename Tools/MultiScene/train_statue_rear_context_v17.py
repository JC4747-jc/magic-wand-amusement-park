"""Bounded rear-view Statue experiment with service-shaped VAL checkpoint selection."""
import hashlib
import json
import shutil
from pathlib import Path
import torch
import ultralytics
from ultralytics import YOLO
from ultralytics.utils.torch_utils import strip_optimizer
from statue_runtime_checkpoint_selection import RuntimeCheckpointSelection
from dataset_paths import manifest_relative_path

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    data = ROOT / 'Datasets/statue-rear-context-v17'
    assert sha(data/'audit.json') == '280a95cdd1508a371b52fee5f5977ad837c0213b10dbcb7409df117ea25dbaca'
    initializer = ROOT / 'object-detection-demo/weights/candidates/statue-reviewed-v7/best.pt'
    out = ROOT / 'Reports/statue-rear-context-v17-training'
    assert not out.exists()
    audit = json.loads((data/'audit.json').read_text(encoding='utf-8'))
    assert audit['training_approved']
    paths = [data/'audit.json', data/'data.yaml', initializer, Path(__file__).resolve(),
             ROOT/'Tools/MultiScene/statue_runtime_checkpoint_selection.py',
             ROOT/'Tools/MultiScene/evaluate_mapped.py', ROOT/'Tools/MultiScene/dataset_paths.py',
             ROOT/'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt']
    for row in audit['samples']:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            path = data / manifest_relative_path(row[key])
            assert sha(path) == row[digest]
            paths.append(path)
    plan = ROOT/'Reports/statue-rear-context-v17-sampling-plan.json'
    assert json.loads(plan.read_text())['dataset_audit_sha256'] == sha(data/'audit.json')
    paths.extend([plan, ROOT/'Tools/MultiScene/hard_positive_world_trainer.py', ROOT/'Tools/MultiScene/hard_positive_sampler.py'])
    paths.extend((ROOT/'Reports/pretrained-yoloworld').glob('*.pt'))
    parent = ROOT/'Datasets/statue-commons-positives-v14'
    assert sha(parent/'audit.json') == audit['parent_audit_sha256']
    for row in json.loads((parent/'audit.json').read_text())['samples']:
        for key in ['image','label']:
            assert sha(parent/manifest_relative_path(row[key])) == sha(data/manifest_relative_path(row[key]))
    for relative, digest in audit['context_evidence'].items():
        path = ROOT/relative
        assert sha(path) == digest
        paths.append(path)
    paths.extend([parent/'audit.json',ROOT/'Tools/MultiScene/prepare_statue_rear_context_v17.py'])
    frozen = [dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in paths]
    options = dict(data=str(data/'data.yaml'), device=0, epochs=20, batch=4, workers=0,
        imgsz=640, seed=4747, deterministic=True, freeze=0, optimizer='AdamW',
        lr0=.00001, lrf=.1, warmup_bias_lr=.00001, mosaic=0., mixup=0., cutmix=0.,
        scale=.1, translate=.05, hsv_h=.01, hsv_s=.3, hsv_v=.2, close_mosaic=0,
        project=str(out), name='train', plots=True)
    out.mkdir()
    (out/'run.json').write_text(json.dumps(dict(dataset_version=sha(data/'audit.json'),
        initializer=str(initializer), initializer_sha256=sha(initializer), parameters=options,
        torch=torch.__version__, ultralytics=ultralytics.__version__, frozen_inputs=frozen,
        scope='Statue visual category; does not establish historical identity or 3D pose.',
        comparability='Same v15 initializer, CUDA options, freeze0, lr1e-5, seed and runtime policy; four new TRAIN rear-view photos. Complete sampling budget76 versus72, so not equal-compute.',
        service_routed=False, deployment_accepted=False), indent=2))
    selector = RuntimeCheckpointSelection(data, out/'runtime-selection')
    model = YOLO(initializer)
    model.add_callback('on_model_save', selector)
    import ultralytics.nn.text_model as text_model
    from hard_positive_world_trainer import HardPositiveWorldTrainer
    text_model.WEIGHTS_DIR = ROOT/'Reports/pretrained-yoloworld'
    class ConfiguredTrainer(HardPositiveWorldTrainer):
        sampling_plan = str(plan)
    model.train(trainer=ConfiguredTrainer, **options)
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
