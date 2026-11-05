"""Select checkpoints on complete target VAL with frozen service preprocessing."""
import json,hashlib,shutil,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def rank_report(report):
 assert report['split']=='val' and report['images']==63 and report['ground_truth_instances']==13 and not report['excluded_samples']
 assert report['allowed_classes']==['JumpingSpider'] and report['operating_confidence']==.65 and report['imgsz']==640 and report['rect'] and report['max_det']==8 and report['nms_iou']==.7
 row=next(r for r in report['operating_per_class'] if r['name']=='JumpingSpider');p=row['precision'] or 0.;r=row['recall'] or 0.;f1=2*p*r/(p+r) if p+r else 0.;ap=next((x['mAP50_95'] for x in report['per_class'] if x['name']=='JumpingSpider'),0.)
 return (int(p>=.9 and r>=.8 and report['false_positive_images']==0),f1,p,r,ap)
class RuntimeCheckpointSelection:
 def __init__(self,data,out,python=None):
  self.data=Path(data);self.out=Path(out);self.python=python or sys.executable;self.best_rank=None;self.records=[];self.best=self.out/'runtime-best.pt';assert not self.out.exists();self.out.mkdir(parents=True)
  audit=json.loads((self.data/'audit.json').read_text());val=[r for r in audit['samples'] if r['split']=='val'];assert len(val)==63 and not any(r.get('evaluation_excluded') for r in val);self.dataset_sha=sha(self.data/'audit.json')
 def __call__(self,trainer):
  epoch=trainer.epoch+1;out=self.out/f'epoch-{epoch:03d}';assert not out.exists();assert sha(self.data/'audit.json')==self.dataset_sha
  subprocess.run([self.python,str(ROOT/'Tools/MultiScene/evaluate_mapped.py'),'--model',str(trainer.last),'--data',str(self.data),'--out',str(out),'--split','val','--imgsz','640','--operating-conf','.65','--allowed-classes','JumpingSpider','--max-det','8','--nms-iou','.7'],cwd=ROOT,check=True,stdout=subprocess.DEVNULL)
  report=json.loads((out/'report.json').read_text());assert report['sha256']==sha(trainer.last) and report['dataset_version']==self.dataset_sha;score=rank_report(report);selected=self.best_rank is None or score>self.best_rank
  if selected:shutil.copy2(trainer.last,self.best);self.best_rank=score
  self.records.append(dict(epoch=epoch,checkpoint_sha256=report['sha256'],report_sha256=sha(out/'report.json'),rank=score,selected=selected))
  (self.out/'selection.json').write_text(json.dumps(dict(policy='Gate pass, F1, precision, recall, AP50-95; strict greater only retains earliest exact tie. VAL only, no confidence search.',dataset_sha256=self.dataset_sha,records=self.records,best_sha256=sha(self.best),deployment_accepted=False),indent=2))
  print(f'FULL63_JUMPING_SPIDER_RUNTIME_VAL epoch={epoch} rank={score} selected={selected}',flush=True)
