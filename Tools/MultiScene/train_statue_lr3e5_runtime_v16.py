"""Bounded JumpingSpider experiment with service-shaped VAL checkpoint selection."""
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
    data = ROOT / 'Datasets/statue-commons-positives-v14'
    assert sha(data/'audit.json') == '3139db9919eb13e358c5e0e9ddf974b1fab9181af19269f365cfe6e9c306155e'
    initializer = ROOT / 'object-detection-demo/weights/candidates/statue-reviewed-v7/best.pt'
    out = ROOT / 'Reports/statue-lr3e5-runtime-v16-training'
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
    plan = ROOT/'Reports/statue-v14-commons-sampling-plan.json'
    assert json.loads(plan.read_text())['dataset_audit_sha256'] == sha(data/'audit.json')
    paths.extend([plan, ROOT/'Tools/MultiScene/hard_positive_world_trainer.py', ROOT/'Tools/MultiScene/hard_positive_sampler.py'])
    paths.extend((ROOT/'Reports/pretrained-yoloworld').glob('*.pt'))
    frozen = [dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in paths]
    options = dict(data=str(data/'data.yaml'), device=0, epochs=20, batch=4, workers=0,
        imgsz=640, seed=4747, deterministic=True, freeze=0, optimizer='AdamW',
        lr0=.00003, lrf=.1, warmup_bias_lr=.00001, mosaic=0., mixup=0., cutmix=0.,
        scale=.1, translate=.05, hsv_h=.01, hsv_s=.3, hsv_v=.2, close_mosaic=0,
        project=str(out), name='train', plots=True)
    previous_path = ROOT/'Reports/statue-unfrozen-runtime-v15-training/run.json'
    previous = json.loads(previous_path.read_text())
    assert previous['initializer_sha256'] == sha(initializer)
    assert previous['torch'] == torch.__version__ and previous['ultralytics'] == ultralytics.__version__
    for key, value in options.items():
        if key not in ['project', 'lr0']:
            assert previous['parameters'][key] == value, key
    frozen.append(dict(path=previous_path.relative_to(ROOT).as_posix(), sha256=sha(previous_path)))
    out.mkdir()
    (out/'run.json').write_text(json.dumps(dict(dataset_version=sha(data/'audit.json'),
        initializer=str(initializer), initializer_sha256=sha(initializer), parameters=options,
        torch=torch.__version__, ultralytics=ultralytics.__version__, frozen_inputs=frozen,
        scope='Statue visual category; does not establish historical identity or 3D pose.',
        comparability='Paired with v15: lr0 3e-5 instead of 1e-5; same initializer, dataset, sampling, seed, CUDA software, other hyperparameters and runtime checkpoint policy. Single seed and tiny public VAL remain limitations.',
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
