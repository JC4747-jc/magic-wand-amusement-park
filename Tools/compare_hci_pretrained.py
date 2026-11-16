"""Bounded diagnostic on draft public labels; never deployment or held-out accuracy acceptance."""
import os
os.environ.setdefault('OMP_NUM_THREADS','1')
os.environ.setdefault('MKL_NUM_THREADS','1')
os.environ.setdefault('YOLO_AUTOINSTALL','false')
import argparse,hashlib,json,time,gc
from pathlib import Path
from collections import Counter

ROOT=Path(__file__).resolve().parents[1]
MAPPING={'cat':'Cat','dog':'Dog','chair':'chair','couch':'couch','bed':'bed','dining table':'diningtable','potted plant':'PottedPlant'}
REQUIRED=['Lighter','Tree','Cockroach','Spider','Statue','BronzeBeast','chair','couch','bed','diningtable','PottedPlant','Cat','Dog']
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def iou(a,b):
 x=max(0,min(a[2],b[2])-max(a[0],b[0]));y=max(0,min(a[3],b[3])-max(a[1],b[1]));i=x*y
 return i/max(1e-9,(a[2]-a[0])*(a[3]-a[1])+(b[2]-b[0])*(b[3]-b[1])-i)
def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
 out=args.output.resolve();assert out.is_relative_to((ROOT/'.codex-tmp').resolve()) and not out.exists()
 import psutil,torch,ultralytics
 from ultralytics import YOLO,settings
 torch.set_num_threads(1);torch.set_num_interop_threads(1)
 for key in ['sync','clearml','comet','dvc','mlflow','raytune','tensorboard','wandb']:
  if key in settings:settings.update({key:False})
 started=time.monotonic();process=psutil.Process();initialFree=psutil.disk_usage(str(ROOT)).free;minRam=float('inf');maxRss=0
 def guard():
  nonlocal minRam,maxRss
  ram=psutil.virtual_memory().available;rss=process.memory_info().rss;disk=psutil.disk_usage(str(ROOT)).free
  minRam=min(minRam,ram);maxRss=max(maxRss,rss)
  assert ram>=3*1024**3 and rss<3*1024**3,'RAM guard'
  assert disk>=20*1024**3 and initialFree-disk<128*1024**2,'Disk guard'
  assert time.monotonic()-started<240,'Four minute budget'
 guard();source=ROOT/'TrainingData/openimages-manual-review-v1.json';data=json.loads(source.read_text(encoding='utf-8-sig'))
 rows=[r for r in data['records'] if r['boxes']];assert len(rows)==41 and not data['training_approved']
 before={p:sha(ROOT/p) for p in ['yolov8n.pt','yolo26n.pt','object-detection-demo/scene_profiles.json']}
 records=[];summaries=[]
 for weight in ['yolov8n.pt','yolo26n.pt']:
  model=YOLO(str(ROOT/weight));supported=set(model.names.values());matched=Counter();total=Counter();latencies=[]
  assert len(supported)==80 and set(MAPPING).issubset(supported)
  for row in rows:
   guard();image=(ROOT/row['local_path']).resolve();assert image.is_relative_to(ROOT) and image.stat().st_size<=2*1024**2 and sha(image)==row['sha256']
   t=time.monotonic();result=model.predict(str(image),imgsz=416,conf=.25,iou=.7,device='cpu',save=False,verbose=False,stream=False)[0];elapsed=time.monotonic()-t;latencies.append(elapsed)
   predictions=[]
   for box in result.boxes:
    name=model.names[int(box.cls.item())]
    if name in MAPPING:predictions.append({'class':MAPPING[name],'confidence':float(box.conf.item()),'xyxy':[float(v) for v in box.xyxy[0].tolist()]})
   used=set();comparisons=[]
   for box in row['boxes']:
    name=box['class'];total[name]+=1
    candidates=[(iou(box['xyxy_pixels'],p['xyxy']),i) for i,p in enumerate(predictions) if p['class']==name and i not in used]
    overlap,index=max(candidates,default=(0,-1));ok=overlap>=.5
    if ok:used.add(index);matched[name]+=1
    comparisons.append({'draftClass':name,'draftBox':box['xyxy_pixels'],'bestIoU':overlap,'matchedAtIoU50':ok})
   records.append({'weight':weight,'imageId':row['image_id'],'imageSHA256':row['sha256'],'elapsedSeconds':elapsed,'predictions':predictions,'draftComparisons':comparisons})
  summaries.append({'weight':weight,'weightSHA256':before[weight],'projectClassesAvailable':sorted(set(MAPPING.values())),'projectClassesMissing':sorted(set(REQUIRED)-set(MAPPING.values())),'images':len(rows),'draftBoxes':dict(total),'matchedDraftBoxes':dict(matched),'meanCpuSeconds':sum(latencies)/len(latencies),'maxCpuSeconds':max(latencies),'timingIncludesFirstCallWarmup':True})
  del result,model;gc.collect();guard()
 assert all(sha(ROOT/p)==digest for p,digest in before.items())
 out.mkdir(parents=True)
 report={'status':'DIAGNOSTIC_ONLY_UNAPPROVED_DRAFTS_NOT_HELDOUT_OR_PICO_ACCURACY','sourceManifestSHA256':sha(source),'inputTrainingApproved':False,'trainingPerformed':False,'downloads':0,'deploymentChanged':False,'versions':{'torch':torch.__version__,'ultralytics':ultralytics.__version__},'settings':{'imgsz':416,'confidence':.25,'nmsIoU':.7,'draftMatchIoU':.5,'device':'cpu','threads':1},'limitations':['Draft labels not fully reviewed; counts are diagnostic matches, not accuracy or mAP.','Public images may overlap model pretraining; no independent holdout claim.','Only positive draft images used; no negative specificity estimate.','No PICO camera domain, stereo, real hand or animation validation.','CPU timing is Windows offline end-to-end, not headset FPS.'],'models':summaries,'resources':{'minimumSampledAvailableRAMGiB':minRam/1024**3,'maximumSampledRSSMiB':maxRss/1024**2,'seconds':time.monotonic()-started,'diskGuardMiB':128,'ramFloorGiB':3,'sampling':'before and after each image/model, not continuous peaks'},'records':records}
 (out/'result.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in report.items() if k!='records'}))
if __name__=='__main__':main()

