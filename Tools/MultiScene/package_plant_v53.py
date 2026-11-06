"""Package rejected diagnostic weights without editing service configuration."""
import hashlib
import json
import shutil
from pathlib import Path
from ultralytics import YOLO

ROOT=Path(__file__).resolve().parents[2]


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8'))


def main():
 run=ROOT/'Reports/plant-v53-pathfix-diagnostic'
 out=ROOT/'object-detection-demo/weights/candidates/plant-pathfix-v53'
 assert not out.exists()
 audit=read(run/'independent-audit.json');report=read(run/'final-val/report.json');plan=read(run/'protocol.json')
 weight=run/'train/weights/best.pt'
 assert sha(weight)==audit['best_sha256']==report['sha256']
 model=YOLO(weight)
 assert model.names=={0:'PottedPlant',1:'CutArrangement'}
 lighter=ROOT/'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt'
 assert sha(lighter)=='fd4a31001feb66127a8c527ffb1e0ba32bfe9f72566cfde4f2f9b5fc0c3e7ca7'
 out.mkdir()
 shutil.copy2(weight,out/'best.pt');shutil.copy2(run/'train/args.yaml',out/'train-args.yaml')
 meta=dict(architecture='YOLOv8m two-class DetectionModel',task='detect',classes=model.names,
  allowed_classes=['PottedPlant'],unity={'0':'ParkPlant','1':None},sha256=sha(out/'best.pt'),
  status='REJECTED_VALIDATION',deployment_accepted=False,service_routed=False,
  annotation_review_hold=report['annotation_review_hold'],
  torch_training=plan['torch'],torch_evaluation=report['torch'],ultralytics=report['ultralytics'],
  dataset='Datasets/plant-label-paths-v52',dataset_sha256=report['dataset_version'],
  dataset_sources=report['dataset_sources'],parameters=plan['parameters'],
  initializer_sha256=plan['initializer_sha256'],native_selected_epoch=22,
  selection=plan['selection'],validation=report,
  evidence={p.relative_to(ROOT).as_posix():sha(p) for p in [run/'protocol.json',run/'completed.json',run/'independent-audit.json',run/'train/results.csv']},
  inference_timing=dict(device=report['device'],mean_ms=report['mean_inference_ms'],warm_mean_ms=report['warm_mean_ms'],p95_ms=report['p95_inference_ms'],scope=report['timing_scope']),
  training_command='python -X utf8 Tools/MultiScene/train_plant_v53_pathfix.py',
  reproduction='Original command refuses existing run directory. Restore licensed source bytes and regenerate data.yaml path for target OS; use isolated fresh output for rerun.',
  last_weight='Reports/plant-v53-pathfix-diagnostic/train/weights/last.pt',optimizer_resume_available=False,
  last_weight_usage='Optimizer stripped: fine-tuning initializer only, not exact optimizer resume.',
  test_freshness='NO_TEST_EVALUATION_FOR_THIS_WEIGHT',pico_validation='NOT_PERFORMED',
  limitations=['Development-only OPEN annotation hold; fails plant precision/recall gates.',
               'Native validation and runtime-shaped evaluation differ; report both without mixing.',
               'CutArrangement is auxiliary only, never routes to a Unity scene.',
               'Packaging is not deployment acceptance. Existing lighter and service profiles untouched.'])
 (out/'model.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
 assert sha(out/'best.pt')==sha(weight)
 assert read(out/'model.json')['sha256']==sha(out/'best.pt')
 print(json.dumps(dict(status=meta['status'],path=out.relative_to(ROOT).as_posix(),sha256=meta['sha256'],service_routed=False)))


if __name__=='__main__':main()
