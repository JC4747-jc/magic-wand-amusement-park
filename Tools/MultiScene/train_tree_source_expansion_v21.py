"""Bounded real-source TRAIN expansion experiment; validation payloads unchanged."""
import hashlib
import json
from pathlib import Path
from ultralytics import YOLO
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
    out=ROOT/'Reports/tree-oiv7n-source-expansion-v21-training'
    if out.exists():
        raise FileExistsError('Inspect existing process and outputs before resume')
    out.mkdir()
    options=dict(control['parameters'],data=str(data/'data.yaml'),project=str(out),device=0)
    specification=dict(control,parameters=options,dataset_version=sha(data/'audit.json'),
        experiment_comparability='One real snow-tree photo and three negatives added; same initializer/seed/20epochs/scale.1/freeze10 as v13, now CUDA rather than CPU, more batches, not equal compute.',
        test_policy='No existing independent TEST for selection or tuning; fresh acceptance evidence required',
        service_routed=False,pico_validated=False,script_sha256=sha(Path(__file__)))
    (out/'run.json').write_text(json.dumps(specification,indent=2))
    preserved.OUT=out
    preserved.PreservedHeadTrainer.target_name='Tree'
    YOLO(initializer).train(trainer=preserved.PreservedHeadTrainer,**options)


if __name__=='__main__':
    main()
