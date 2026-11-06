"""Predeclared validation-only confidence diagnostic; never rewrite historical gates."""
import hashlib,json,time
from pathlib import Path
import cv2,numpy as np,torch,ultralytics
from ultralytics import YOLO
from dataset_paths import manifest_relative_path as rel
from runtime_checkpoint_selection_v57 import key,metrics
ROOT=Path(__file__).resolve().parents[2]
THRESHOLDS=[.35,.4,.45,.5,.55,.6,.65,.7,.75,.8,.85,.9]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 out=ROOT/'Reports/plant-v82-confidence-diagnostic';assert not out.exists();out.mkdir()
 data=ROOT/'Datasets/plant-binary-v79';weight=ROOT/'object-detection-demo/weights/candidates/plant-binary-v81/best.pt';assert sha(weight)=='66c8f1c0bc059b8a7d14c1f7a0b2be81fc1466fe77b7a7afb0d61efa92005218'
 baseline_path=ROOT/'Reports/plant-v81-binary-diagnostic/final-val/report.json';baseline=json.loads(baseline_path.read_text());policy_path=ROOT/'Reports/plant-v81-binary-diagnostic/runtime-selection/policy.json';policy=json.loads(policy_path.read_text());old=set(policy['regression_images']);audit=json.loads((data/'audit.json').read_text());rows=[r for r in audit['samples'] if r['split']=='val'];assert len(rows)==30
 files={weight,data/'audit.json',data/'data.yaml',data/'review_hold.json',baseline_path,policy_path,Path(__file__).resolve(),ROOT/'Tools/MultiScene/evaluate_mapped.py',ROOT/'Tools/MultiScene/runtime_checkpoint_selection_v57.py'}
 for row in rows:
  for k,digest in [('image','sha256'),('label','label_sha256')]:p=data/rel(row[k]);assert sha(p)==row[digest];files.add(p)
 frozen=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)) for p in sorted(files)]
 protocol=dict(status='PRE_INFERENCE_VALIDATION_ONLY_CALIBRATION_DIAGNOSTIC',files=frozen,thresholds=THRESHOLDS,model_preselected='v81 epoch30 under historical confidence0.35',prediction=dict(conf=.001,imgsz=640,rect=True,iou=.7,max_det=8,classes=[0],device='cpu'),gates=dict(full_and_original13_precision_min=.9,full_and_original13_recall_min=.8),selection='Among passing grid values maximize full F1, original13 F1, precision, recall; prefer lower threshold for exact ties. If no value passes, selected_threshold is null.',historical_gate_or_runtime_config_changed=False,test_used=False,deployment_accepted=False)
 (out/'protocol.json').write_text(json.dumps(protocol,indent=2),encoding='utf-8')
 model=YOLO(weight);assert model.names=={0:'PottedPlant'};per_image=[]
 for row in rows:
  image=cv2.imread(str(data/rel(row['image'])));h,w=image.shape[:2];truth=[]
  for line in (data/rel(row['label'])).read_text().splitlines():
   c,cx,cy,bw,bh=map(float,line.split());assert c==0;truth.append(np.array([(cx-bw/2)*w,(cy-bh/2)*h,(cx+bw/2)*w,(cy+bh/2)*h]))
  t=time.perf_counter();pred=model.predict(image,imgsz=640,rect=True,device='cpu',conf=.001,iou=.7,max_det=8,classes=[0],verbose=False)[0];elapsed=(time.perf_counter()-t)*1000
  det=sorted(pred.boxes.data.cpu().numpy().tolist(),key=lambda d:-d[4]);used=set();flags=[]
  for d in det:
   box=np.array(d[:4]);choices=[]
   for i,target in enumerate(truth):
    if i in used:continue
    inter=np.maximum(0,np.minimum(box[2:],target[2:])-np.maximum(box[:2],target[:2])).prod();union=np.maximum(0,box[2:]-box[:2]).prod()+np.maximum(0,target[2:]-target[:2]).prod()-inter;choices.append((inter/max(union,1e-9),i))
   match=max(choices) if choices else (0,None);valid=match[0]>=.5
   if valid:used.add(match[1])
   flags.append(bool(valid))
  curves=[]
  for threshold in THRESHOLDS:
   indices=[i for i,d in enumerate(det) if d[4]>=threshold];tp=sum(flags[i] for i in indices);curves.append(dict(threshold=threshold,image=row['image'],ground_truth=len(truth),true_positive=tp,false_positive=len(indices)-tp,false_negative=len(truth)-tp))
  per_image.append(dict(image=row['image'],predictions=[dict(box_xyxy=d[:4],confidence=d[4],match_iou50=flag) for d,flag in zip(det,flags)],curves=curves,inference_ms=elapsed))
 base={key(r['image']):r for r in baseline['per_image']}
 for row in per_image:
  d=row['curves'][0];assert all(d[k]==base[key(row['image'])][k] for k in ['ground_truth','true_positive','false_positive','false_negative'])
 grid=[]
 for i,threshold in enumerate(THRESHOLDS):
  current=[r['curves'][i] for r in per_image];full=metrics(current);original=metrics([r for r in current if key(r['image']) in old]);assert full['true_positive']+full['false_negative']==22 and original['true_positive']+original['false_negative']==13
  gate=lambda m:m['precision']>=.9 and m['recall']>=.8
  grid.append(dict(threshold=threshold,full=full,original13=original,passes_both_gates=gate(full) and gate(original)))
 rank=lambda r:(r['passes_both_gates'],r['full']['f1'],r['original13']['f1'],r['full']['precision'],r['full']['recall'],-r['threshold'])
 passing=[r for r in grid if r['passes_both_gates']];selected=max(passing,key=rank) if passing else None
 for row in frozen:assert sha(ROOT/row['path'])==row['sha256'],row['path']
 report=dict(status='VALIDATION_CALIBRATION_DIAGNOSTIC_COMPLETE',grid=grid,selected_threshold=selected['threshold'] if selected else None,passing_threshold_count=len(passing),baseline035_per_image_counts_exact=True,per_image=per_image,protocol_sha256=sha(out/'protocol.json'),weight_sha256=sha(weight),dataset_sha256=sha(data/'audit.json'),all_frozen_files_verified=len(frozen),torch=torch.__version__,ultralytics=ultralytics.__version__,test_used=False,checkpoint_reselected=False,runtime_config_changed=False,deployment_accepted=False,limitations=['Repeatedly observed small development VAL; threshold choice would require new frozen policy, annotation clearance and independent TEST/PICO before use.','Historical0.35 selection/rejection is unchanged. No deployed profile or model changed.','CPU prediction timings include cold initialization, not device latency.'])
 (out/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(dict(grid=grid,selected_threshold=report['selected_threshold'],status=report['status'])))
if __name__=='__main__':main()
