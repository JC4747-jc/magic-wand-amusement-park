"""Future-only checkpoint policy: both old and new VAL cohorts must pass."""
import json
import shutil
import subprocess
import sys
from pathlib import Path
from dataset_paths import manifest_relative_path as rel
from statue_runtime_checkpoint_selection import sha

ROOT=Path(__file__).resolve().parents[2]
PARENT=ROOT/'Datasets/statue-hands-v26/audit.json'
DATA=ROOT/'Datasets/statue-validation-extension-v32'
DATA_SHA='e4b314a992b76ac40f709ef437c11302478a1b95f6cdcdfbba99a45fac78b3be'

def cohort_metrics(report,old_paths):
    old_paths={rel(p).as_posix() for p in old_paths}
    groups={'prior':[],'extension':[]}
    for row in report['per_image']:
        groups['prior' if rel(row['image']).as_posix() in old_paths else 'extension'].append(row)
    assert len(groups['prior'])==7 and len(groups['extension'])==12
    result={}
    for name,rows in groups.items():
        tp,fp,fn=[sum(r[k] for r in rows) for k in ['true_positive','false_positive','false_negative']]
        p=tp/(tp+fp) if tp+fp else 0.;r=tp/(tp+fn) if tp+fn else 0.
        result[name]=dict(tp=tp,fp=fp,fn=fn,precision=p,recall=r,f1=2*p*r/(p+r) if p+r else 0.)
    return result

def score_report(report,old_paths):
    assert report['split']=='val' and report['images']==19 and report['ground_truth_instances']==33 and not report['excluded_samples']
    assert report['allowed_classes']==['Statue'] and report['operating_confidence']==.65 and report['imgsz']==640 and report['rect'] and report['max_det']==32 and report['nms_iou']==.7
    cohorts=cohort_metrics(report,old_paths)
    negative=[r for r in report['per_image'] if r['ground_truth']==0]
    assert negative, 'Expected reviewed flat-figure negative in expanded VAL'
    gate=all(s['precision']>=.9 and s['recall']>=.8 for s in cohorts.values()) and all(r['false_positive']==0 for r in negative)
    ap=next(x['mAP50_95'] for x in report['per_class'] if x['name']=='Statue')
    return (int(gate),sum(s['f1'] for s in cohorts.values())/2,
            min(s['recall'] for s in cohorts.values()),min(s['precision'] for s in cohorts.values()),ap)

def rank_report(report):
    parent=json.loads(PARENT.read_text(encoding='utf-8'))
    return score_report(report,[r['image'] for r in parent['samples'] if r['split']=='val'])

class ExpandedValidationSelection:
    def __init__(self,data,out):
        self.data=Path(data);self.out=Path(out);self.best_rank=None;self.records=[];self.best=self.out/'runtime-best.pt'
        assert self.data.resolve()==DATA.resolve() and sha(self.data/'audit.json')==DATA_SHA
        assert not (self.data/'review_hold.json').exists()
        assert not self.out.exists();self.out.mkdir(parents=True)
    def __call__(self,trainer):
        epoch=trainer.epoch+1;out=self.out/f'epoch-{epoch:03d}'
        assert not out.exists() and sha(self.data/'audit.json')==DATA_SHA
        subprocess.run([sys.executable,str(ROOT/'Tools/MultiScene/evaluate_mapped.py'),'--model',str(trainer.last),'--data',str(self.data),'--out',str(out),'--split','val','--imgsz','640','--operating-conf','.65','--allowed-classes','Statue','--max-det','32','--nms-iou','.7'],cwd=ROOT,check=True,stdout=subprocess.DEVNULL)
        report=json.loads((out/'report.json').read_text(encoding='utf-8'))
        assert report['sha256']==sha(trainer.last) and report['dataset_version']==DATA_SHA
        score=rank_report(report);selected=self.best_rank is None or score>self.best_rank
        if selected:shutil.copy2(trainer.last,self.best);self.best_rank=score
        self.records.append(dict(epoch=epoch,checkpoint_sha256=report['sha256'],report_sha256=sha(out/'report.json'),rank=score,selected=selected))
        (self.out/'selection.json').write_text(json.dumps(dict(policy='Both VAL cohorts P>=.9/R>=.8 and zero negative FP, then macro cohort F1/min recall/min precision/AP95; earliest exact tie. No TEST selection.',dataset_sha256=DATA_SHA,records=self.records,best_sha256=sha(self.best),deployment_accepted=False),indent=2),encoding='utf-8')
        print(f'FULL19_STATUE_VAL epoch={epoch} rank={score} selected={selected}',flush=True)
