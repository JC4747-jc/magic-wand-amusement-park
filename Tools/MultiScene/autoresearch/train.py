"""Bounded serial YOLO experiments; immutable inputs, VAL-only selection, retained failures."""
import argparse,json,sys,math,os,time,shutil,traceback,gc
from pathlib import Path
from prepare import ROOT,sha,verify
from runtime_checkpoint_selection import RuntimeCheckpointSelection,rank_report

def validate_plan(plan,contract):
 if plan.get('epochs')!=15 or not 1<=len(plan.get('trials',[]))<=3:raise ValueError('Budget requires15epochs and1..3trials')
 seen=set()
 for trial in plan['trials']:
  name=trial['name']
  if not name or any(c not in 'abcdefghijklmnopqrstuvwxyz0123456789-_' for c in name) or name in seen:raise ValueError('Invalid/duplicate trial name')
  seen.add(name)
  for key,value in trial['overrides'].items():
   if key not in {'lr0','freeze','scale'} or key not in contract['editable_training_fields']:raise ValueError('Noneditable training field:'+key)
   if isinstance(value,bool) or not isinstance(value,(int,float)) or not math.isfinite(value):raise ValueError('Nonfinite parameter')
   if key=='lr0' and not 1e-6<=value<=.001:raise ValueError('Learning rate out of bound')
   if key=='freeze' and (type(value)!=int or not 0<=value<=20):raise ValueError('Freeze out of bound')
   if key=='scale' and not 0<=value<=.5:raise ValueError('Scale out of bound')
 return plan

def run(args):
 import torch,ultralytics
 from ultralytics import YOLO
 from ultralytics.utils.torch_utils import strip_optimizer
 cp=ROOT/args.contract;contract=json.loads(cp.read_text());plan=json.loads((ROOT/args.plan).read_text());validate_plan(plan,contract);verify(contract)
 out=(ROOT/args.out).resolve()
 if not out.is_relative_to(ROOT/'Reports') or out.exists():raise ValueError('New Reports output required')
 lock=ROOT/'Reports/autoresearch-gpu.lock'
 with lock.open('x') as f:f.write(str(os.getpid()))
 ledger=[];best=None
 try:
  out.mkdir();shutil.copy2(ROOT/args.plan,out/'plan.json');shutil.copy2(cp,out/'contract.json');(out/'environment.json').write_text(json.dumps(dict(torch=torch.__version__,ultralytics=ultralytics.__version__,training_script_sha256=sha(Path(__file__)),budget='Equal15epochs; measured wall clock includes validation, not claimed fixed5minutes',deployment_accepted=False),indent=2))
  data=ROOT/contract['dataset'];weight=ROOT/'object-detection-demo/weights/candidates/plant-native-v12/best.pt'
  for trial in plan['trials']:
   verify(contract);trialout=out/trial['name'];trialout.mkdir();start=time.perf_counter();record=dict(name=trial['name'],hypothesis=trial['hypothesis'],overrides=trial['overrides'],status='RUNNING',service_routed=False)
   (trialout/'state.json').write_text(json.dumps(record,indent=2))
   try:
    opts=dict(data=str(data/'data.yaml'),device=0,epochs=15,imgsz=640,batch=4,workers=0,seed=20261105,deterministic=True,freeze=0,optimizer='AdamW',lr0=.0001,lrf=.1,warmup_bias_lr=.0001,mosaic=0.,mixup=0.,cutmix=0.,scale=.3,translate=.05,hsv_h=.01,hsv_s=.3,hsv_v=.2,close_mosaic=0,project=str(trialout),name='train',plots=True);opts.update(trial['overrides'])
    spec=dict(dataset_version=sha(data/'audit.json'),initializer_sha256=sha(weight),parameters=opts,torch=torch.__version__,ultralytics=ultralytics.__version__,contract_sha256=sha(cp),hypothesis=trial['hypothesis'],test_selection=False,service_routed=False);(trialout/'run.json').write_text(json.dumps(spec,indent=2))
    selector=RuntimeCheckpointSelection(data,trialout/'runtime-selection',python=sys.executable);model=YOLO(weight);model.add_callback('on_model_save',selector);model.train(**opts);assert len(selector.records)==15
    native=trialout/'train/weights/best.pt';shutil.copy2(native,trialout/'train/weights/native-fitness-best.pt');strip_optimizer(selector.best);shutil.copy2(selector.best,native)
    subprocess_args=[sys.executable,str(ROOT/'Tools/MultiScene/evaluate_mapped.py'),'--model',str(native),'--data',str(data),'--out',str(trialout/'final-val'),'--split','val','--imgsz','640','--operating-conf','.35','--allowed-classes','PottedPlant','--max-det','8','--nms-iou','.7']
    import subprocess
    subprocess.run(subprocess_args,cwd=ROOT,check=True,stdout=subprocess.DEVNULL);report=json.loads((trialout/'final-val/report.json').read_text());score=rank_report(report);assert report['sha256']==sha(native);verify(contract)
    improved=best is None or score>best['rank'];record.update(status='COMPLETED_VAL_CANDIDATE' if score[0] else 'REJECTED_VALIDATION',rank=score,weight_sha256=sha(native),weight=str(native.relative_to(ROOT)),selected_for_development=improved,selected_epoch=next(r['epoch'] for r in reversed(selector.records) if r['selected']),deployment_accepted=False)
    if improved:best=dict(name=trial['name'],rank=score,weight=record['weight'],sha256=record['weight_sha256'])
    del model;gc.collect();torch.cuda.empty_cache()
   except Exception as exc:
    record.update(status='FAILED',error=repr(exc),traceback=traceback.format_exc());(trialout/'failure.json').write_text(json.dumps(record,indent=2))
   record['elapsed_seconds']=time.perf_counter()-start;ledger.append(record);(trialout/'state.json').write_text(json.dumps(record,indent=2));(out/'ledger.json').write_text(json.dumps(dict(trials=ledger,best_development_candidate=best,all_artifacts_retained=True,fresh_test_performed=False,deployment_accepted=False),indent=2));verify(contract)
  print(json.dumps(dict(status='BOUNDED_EXPERIMENTS_TERMINAL',trials=len(ledger),best=best,deployment_accepted=False)),flush=True)
 finally:
  lock.unlink()
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--contract',required=True);p.add_argument('--plan',required=True);p.add_argument('--out',required=True);a=p.parse_args();run(a)
