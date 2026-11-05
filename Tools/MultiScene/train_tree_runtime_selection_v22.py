"""Bounded real-source TRAIN expansion experiment; validation payloads unchanged."""
import hashlib
import json
from pathlib import Path
from ultralytics import YOLO
from tree_runtime_checkpoint_selection import RuntimeCheckpointSelection
import shutil
from ultralytics.utils.torch_utils import strip_optimizer
import train_oiv7_statue_preserved as preserved

ROOT=Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    data=ROOT/'Datasets/tree-reviewed-expansion-v21'
    assert sha(data/'audit.json')=='93486d0425afb423956d7d76755d1ed8d961e3449fb5791d970b37a5b37c1749'
    audit=json.loads((data/'audit.json').read_text(encoding='utf-8'))
    assert audit['training_approved'] is True
    for row in audit['samples']:
        assert sha(data/row['image'])==row['sha256'] and sha(data/row['label'])==row['label_sha256']
    control=json.loads((ROOT/'Reports/tree-oiv7n-low-scale-v13-training/run.json').read_text())
    initializer=Path(control['initializer'])
    assert sha(initializer)==control['initializer_sha256']
    out=ROOT/'Reports/tree-oiv7n-runtime-selection-v22-training'
    if out.exists():
        raise FileExistsError('Inspect existing process and outputs before resume')
    out.mkdir()
    options=dict(control['parameters'],data=str(data/'data.yaml'),project=str(out),device=0)
    specification=dict(control,parameters=options,dataset_version=sha(data/'audit.json'),
        experiment_comparability='Same v21 dataset/initializer/CUDA/seed/hyperparameters. Only checkpoint selection changes: all11runtimeVAL .65rect640/.7/max8 Tree after each saved epoch; earliest exact tie, no TEST/threshold search.',
        test_policy='No existing independent TEST for selection or tuning; fresh acceptance evidence required',
        service_routed=False,pico_validated=False,script_sha256=sha(Path(__file__)))
    (out/'run.json').write_text(json.dumps(specification,indent=2))
    preserved.OUT=out
    preserved.PreservedHeadTrainer.target_name='Tree'
    selector = RuntimeCheckpointSelection(data, out/'runtime-selection')
    model = YOLO(initializer)
    model.add_callback('on_model_save', selector)
    model.train(trainer=preserved.PreservedHeadTrainer, **options)
    assert len(selector.records) == options['epochs']
    native = out/'train/weights/best.pt'
    shutil.copy2(native, out/'native-fitness-best.pt')
    stripped = out/'runtime-selection/selected-stripped.pt'
    shutil.copy2(selector.best, stripped)
    strip_optimizer(stripped)
    shutil.copy2(stripped, native)
    (out/'runtime-selection/final-weight.json').write_text(json.dumps(dict(
        sha256=sha(native), raw_selected_sha256=sha(selector.best),
        rank=selector.best_rank, service_routed=False, deployment_accepted=False), indent=2))


if __name__=='__main__':
    main()
