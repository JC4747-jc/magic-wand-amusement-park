"""Inspect observed regression failures; never select thresholds or mutate folds."""
import json,hashlib
from pathlib import Path
import numpy as np
from ultralytics import YOLO
ROOT=Path(__file__).resolve().parents[2]
def iou(a,b):
 inter=np.maximum(0,np.minimum(a[2:],b[2:])-np.maximum(a[:2],b[:2])).prod();union=np.maximum(0,a[2:]-a[:2]).prod()+np.maximum(0,b[2:]-b[:2]).prod()-inter;return float(inter/max(union,1e-9))
def main():
 modelpath=ROOT/'Reports/plant-confuser-v15-training/train/weights/best.pt';assert hashlib.sha256(modelpath.read_bytes()).hexdigest()=='df5d6156c68d31a77fad4029e8375d38e8fe490d84b35847209bab79625ba5f6';data=ROOT/'Datasets/plant-new-author-heldout-v5';audit=json.loads((data/'audit.json').read_text());report=json.loads((ROOT/'Reports/plant-confuser-v15-conf035-observed-v5/report.json').read_text());error_images={r['image'].replace('\\','/') for r in report['errors']};m=YOLO(modelpath);rows=[]
 for row in audit['samples']:
  relative=row['image'].replace('\\','/')
  if relative not in error_images:continue
  image=data/relative;result=m.predict(str(image),imgsz=640,rect=True,device='cpu',conf=.001,iou=.7,classes=None,max_det=8,verbose=False)[0];h,w=result.orig_shape;det=[dict(name=m.names[int(b.cls.item())],confidence=float(b.conf.item()),xyxy=b.xyxy[0].tolist()) for b in result.boxes];label=data/(row.get('label') or relative.replace('images/','labels/').rsplit('.',1)[0]+'.txt').replace('\\','/');targets=[]
  for line in label.read_text().splitlines():
   c,x,y,bw,bh=map(float,line.split())
   if int(c)!=58:continue
   box=np.array([(x-bw/2)*w,(y-bh/2)*h,(x+bw/2)*w,(y+bh/2)*h]);best={}
   for name in ['PottedPlant','CutArrangement']:
    matches=[dict(confidence=d['confidence'],iou=iou(box,np.array(d['xyxy']))) for d in det if d['name']==name and iou(box,np.array(d['xyxy']))>=.5];best[name]=max(matches,key=lambda d:d['confidence']) if matches else None
   targets.append(dict(ground_truth_xyxy=box.tolist(),best_iou05_by_class=best))
  rows.append(dict(image=relative,predictions=det,target_diagnostics=targets))
 out=dict(scope='Observed regression error analysis at fixed preprocessing/max8; .001 diagnostic floor only, no threshold selection or fresh TEST claim',model_sha256=report['sha256'],dataset_sha256=report['dataset_version'],runtime_confidence_unchanged=.35,diagnostic_floor=.001,images=len(rows),rows=rows,labels_changed=False,threshold_selected=False,service_routed=False,pico_validated=False);path=ROOT/'Reports/plant-confuser-v15-native-error-diagnostic.json';assert not path.exists();path.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps([dict(image=r['image'],targets=r['target_diagnostics'],detections_above035=[d for d in r['predictions'] if d['confidence']>=.35]) for r in rows]))
if __name__=='__main__':main()
