"""Bounded JumpingSpider experiment with service-shaped VAL checkpoint selection."""
import hashlib
import json
import shutil
from pathlib import Path
import torch
import ultralytics
from ultralytics import YOLO
from ultralytics.utils.torch_utils import strip_optimizer
from jumping_spider_runtime_checkpoint_selection import RuntimeCheckpointSelection
from dataset_paths import manifest_relative_path

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    data = ROOT / 'Datasets/jumping-spider-confusers-v10'
    assert sha(data/'audit.json') == '1cffbf88636061a9f515b9ca8f02f7667e1893b0ec8aa8e89e87c1c21722510e'
    initializer = ROOT / 'object-detection-demo/weights/candidates/jumping-spider-context-v7-yolov8m/best.pt'
    out = ROOT / 'Reports/jumping-spider-runtime-v11-training'
    assert not out.exists()
    audit = json.loads((data/'audit.json').read_text(encoding='utf-8'))
    assert audit['training_approved']
    paths = [data/'audit.json', data/'data.yaml', initializer, Path(__file__).resolve(),
             ROOT/'Tools/MultiScene/jumping_spider_runtime_checkpoint_selection.py',
             ROOT/'Tools/MultiScene/evaluate_mapped.py', ROOT/'Tools/MultiScene/dataset_paths.py',
             ROOT/'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt']
    for row in audit['samples']:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            path = data / manifest_relative_path(row[key])
            assert sha(path) == row[digest]
            paths.append(path)
    frozen = [dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in paths]
    options = dict(data=str(data/'data.yaml'), device=0, epochs=20, batch=4, workers=0,
        imgsz=640, seed=20261103, deterministic=True, freeze=0, optimizer='AdamW',
        lr0=.00001, lrf=.1, warmup_bias_lr=.00001, mosaic=0., mixup=0., cutmix=0.,
        scale=.1, translate=.05, hsv_h=.01, hsv_s=.3, hsv_v=.2, close_mosaic=0,
        project=str(out), name='train', plots=True)
    out.mkdir()
    (out/'run.json').write_text(json.dumps(dict(dataset_version=sha(data/'audit.json'),
        initializer=str(initializer), initializer_sha256=sha(initializer), parameters=options,
        torch=torch.__version__, ultralytics=ultralytics.__version__, frozen_inputs=frozen,
        scope='Salticidae family only, not all spiders or a biological species.',
        comparability='Fresh v7 initialization; CUDA and current library version versus old CPU/resumed v10. Not an equal-compute selector-only ablation.',
        service_routed=False, deployment_accepted=False), indent=2))
    selector = RuntimeCheckpointSelection(data, out/'runtime-selection')
    model = YOLO(initializer)
    model.add_callback('on_model_save', selector)
    model.train(**options)
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
