"""Prospective full-VAL runtime selector with explicit original-cohort gates."""
import hashlib,json,math,shutil,subprocess,sys
from pathlib import Path
from dataset_paths import manifest_relative_path
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def key(value):return manifest_relative_path(value).as_posix()
def metrics(rows):
 tp=sum(r['true_positive'] for r in rows);fp=sum(r['false_positive'] for r in rows);fn=sum(r['false_negative'] for r in rows)
 p=tp/(tp+fp) if tp+fp else 0.;r=tp/(tp+fn) if tp+fn else 0.
 return dict(true_positive=tp,false_positive=fp,false_negative=fn,precision=p,recall=r,f1=2*p*r/(p+r) if p+r else 0.)
def rank_report(report,policy):
 assert report['split']=='val' and not report['excluded_samples']
 assert report['allowed_classes']==['PottedPlant'] and report['operating_confidence']==.35
 assert report['imgsz']==640 and report['rect'] and report['max_det']==8 and report['nms_iou']==.7
 rows=report['per_image'];byimage={key(r['image']):r for r in rows}
 assert len(rows)==len(byimage)==report['images']==len(policy['val_images'])
 assert set(byimage)==set(policy['val_images'])
 for row in rows:
  assert all(type(row[k]) is int and row[k]>=0 for k in ['ground_truth','true_positive','false_positive','false_negative'])
  assert row['true_positive']+row['false_negative']==row['ground_truth']
 assert sum(r['ground_truth'] for r in rows)==report['ground_truth_instances']==policy['target_instances']
 full=metrics(rows);old=metrics([byimage[k] for k in policy['regression_images']])
 assert len(policy['regression_images'])==13
 assert sum(byimage[k]['ground_truth'] for k in policy['regression_images'])==13
 operating=next(r for r in report['operating_per_class'] if r['name']=='PottedPlant')
 for k in ['true_positive','false_positive','false_negative']:assert full[k]==operating[k]
 ap=next(r['mAP50_95'] for r in report['per_class'] if r['name']=='PottedPlant');assert math.isfinite(ap)
 gate=lambda m:m['precision']>=.9 and m['recall']>=.8
 return (int(gate(full) and gate(old)),full['f1'],old['f1'],full['precision'],full['recall'],ap),dict(full=full,original13=old)

class RuntimeCheckpointSelectionV57:
 def __init__(self,data,out,regression_images,python=None):
  self.data=Path(data);self.out=Path(out);assert not self.out.exists()
  audit=json.loads((self.data/'audit.json').read_text());rows=[r for r in audit['samples'] if r['split']=='val']
  assert len(rows)==30 and not any(r.get('evaluation_excluded') for r in rows)
  self.policy=dict(val_images=[key(r['image']) for r in rows],regression_images=[key(r) for r in regression_images],target_instances=22)
  assert len(set(self.policy['regression_images']))==13 and set(self.policy['regression_images'])<=set(self.policy['val_images'])
  self.frozen={self.data/'audit.json':sha(self.data/'audit.json'),ROOT/'Tools/MultiScene/evaluate_mapped.py':sha(ROOT/'Tools/MultiScene/evaluate_mapped.py')}
  for row in rows:
   for field,digest in [('image','sha256'),('label','label_sha256')]:
    path=self.data/manifest_relative_path(row[field]);assert sha(path)==row[digest];self.frozen[path]=row[digest]
  self.python=python or sys.executable;self.best_rank=None;self.records=[];self.best=self.out/'runtime-best.pt'
  self.out.mkdir(parents=True)
  (self.out/'policy.json').write_text(json.dumps(dict(self.policy,ranking='Gate full30 AND old13 P>=.9/R>=.8, then fullF1,oldF1,P,R,AP95; earliest exact tie.',inference='single-label YOLO predict .35/640rect/.7/max8 PottedPlant only',deployment_accepted=False),indent=2))
 def __call__(self,trainer):
  for path,digest in self.frozen.items():assert sha(path)==digest,str(path)
  epoch=trainer.epoch+1;out=self.out/f'epoch-{epoch:03d}';assert not out.exists()
  subprocess.run([self.python,'-X','utf8',str(ROOT/'Tools/MultiScene/evaluate_mapped.py'),'--model',str(trainer.last),'--data',str(self.data),'--out',str(out),'--split','val','--imgsz','640','--operating-conf','.35','--allowed-classes','PottedPlant','--max-det','8','--nms-iou','.7'],cwd=ROOT,check=True,stdout=subprocess.DEVNULL)
  report=json.loads((out/'report.json').read_text());assert report['sha256']==sha(trainer.last) and report['dataset_version']==self.frozen[self.data/'audit.json']
  score,cohorts=rank_report(report,self.policy)
  selected=self.best_rank is None or score>self.best_rank
  if selected:shutil.copy2(trainer.last,self.best);self.best_rank=score
  self.records.append(dict(epoch=epoch,rank=score,cohorts=cohorts,selected=selected,checkpoint_sha256=report['sha256'],report_sha256=sha(out/'report.json')))
  for path,digest in self.frozen.items():assert sha(path)==digest,str(path)
  (self.out/'selection.json').write_text(json.dumps(dict(records=self.records,best_sha256=sha(self.best),deployment_accepted=False),indent=2))
  print(f'FULL30_RUNTIME_VAL epoch={epoch} rank={score} selected={selected}',flush=True)
