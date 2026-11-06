"""Compare training fit with held-out coverage; no model selection or label edits."""
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]


def read(p):return json.loads(p.read_text(encoding='utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
 trainpath=ROOT/'Reports/plant-v51-training-diagnostic/report.json'
 valpath=ROOT/'Reports/plant-v51-diagnostic/final-val/report.json'
 data=ROOT/'Datasets/plant-hanging-v50/audit.json'
 train,val,audit=map(read,[trainpath,valpath,data])
 assert train['images']==127 and val['images']==30
 assert train['sha256']==val['sha256']=='046e0ad1597dea75c6c6ad98cf66baee8e410c8da93f608c317ed1c302bbdec9'
 assert train['dataset_version']==val['dataset_version']==sha(data)
 assert train['split']=='train' and train['operating_confidence']==val['operating_confidence']==.35
 for key in ['imgsz','rect','max_det','nms_iou','allowed_classes']:
  assert train[key]==val[key],key
 lookup={r['image'].replace('\\','/'):r for r in train['per_image']}
 selected=[]
 for row in audit['samples']:
  if row.get('page_id') in {129746819,87404883,134319541,63438809}:
   assert row['split']=='train'
   selected.append(dict(page_id=row['page_id'],**lookup[row['image'].replace('\\','/')]))
 assert len(selected)==4
 result=dict(status='TRAIN_DIAGNOSTIC_NOT_GENERALIZATION',training_metrics=train['operating_per_class'],
  validation_metrics=val['operating_per_class'],dedicated_hanging_source_training_photos=selected,
  note='Source63438809 includes hanging basket plus ground urn; do not call both instances hanging.',
  evidence={p.relative_to(ROOT).as_posix():sha(p) for p in [trainpath,valpath,data]},
  deployment_accepted=False,labels_changed=False,
  next_action='Inspect remaining TRAIN errors and annotation consistency. Strong TRAIN fit with weak VAL favors source/appearance coverage work, not assuming more epochs alone will generalize.')
 out=ROOT/'Reports/plant-v51-train-val-diagnosis.json'
 assert not out.exists()
 out.write_text(json.dumps(result,indent=2),encoding='utf-8')
 print(json.dumps(result))


if __name__=='__main__':main()
