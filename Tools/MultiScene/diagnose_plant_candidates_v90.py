"""Diagnostic only: trace low-confidence and pre/post-NMS candidates on observed VAL.
No thresholds, data labels, model selection or runtime configuration are changed.
"""
import hashlib,json,time
from pathlib import Path
import cv2,numpy as np,torch,ultralytics
from ultralytics import YOLO
from ultralytics.models.yolo.detect.predict import DetectionPredictor
from ultralytics.utils import ops,nms
from dataset_paths import manifest_relative_path as rel
ROOT=Path(__file__).resolve().parents[2]
STEMS={'a8d79cd1e9fd1cdb40e2541a','f792813aeb8521eb5e9ba43f','ad4054926f2d2738dc4a8773'}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def overlaps(boxes,truth):
 if not len(boxes):return np.zeros(0)
 inter=np.maximum(0,np.minimum(boxes[:,2:],truth[2:])-np.maximum(boxes[:,:2],truth[:2])).prod(1)
 areas=np.maximum(0,boxes[:,2:]-boxes[:,:2]).prod(1);area=np.maximum(0,truth[2:]-truth[:2]).prod()
 return inter/np.maximum(areas+area-inter,1e-9)
class TracePredictor(DetectionPredictor):
 def postprocess(self,preds,img,orig_imgs,**kwargs):
  raw=preds[0] if isinstance(preds,(list,tuple)) else preds
  self.trace_raw=raw.detach().cpu().clone()
  self.trace_shape=tuple(img.shape[2:])
  return super().postprocess(preds,img,orig_imgs,**kwargs)
def main():
 out=ROOT/'Reports/plant-v90-candidate-diagnostic';assert not out.exists();out.mkdir()
 data=ROOT/'Datasets/plant-hanging-v88';audit=json.loads((data/'audit.json').read_text());rows=[r for r in audit['samples'] if r['split']=='val' and Path(r['image'].replace(chr(92),'/')).stem in STEMS];assert len(rows)==3
 weights={'official_coco_m':ROOT/'object-detection-demo/weights/candidates/common-yolov8m-official-v1/best.pt','v81':ROOT/'object-detection-demo/weights/candidates/plant-binary-v81/best.pt','v89':ROOT/'object-detection-demo/weights/candidates/plant-hanging-v89/best.pt'}
 inputs={data/'audit.json',Path(__file__).resolve()}
 for row in rows:
  for key,digest in [('image','sha256'),('label','label_sha256')]:
   p=data/rel(row[key]);assert sha(p)==row[digest];inputs.add(p)
 expected={'official_coco_m':'5d4a90cdc7a21786cc59cd19778e9eafff836df9e2da32524737c7ee6efe4fe5','v81':'66c8f1c0bc059b8a7d14c1f7a0b2be81fc1466fe77b7a7afb0d61efa92005218','v89':'49e7c0332e4cd1988070893fa3b13b1aed92bf279073c54f0bbf55ca443a5d04'}
 for key,path in weights.items():assert sha(path)==expected[key]
 inputs.update(weights.values());files=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)) for p in sorted(inputs)]
 protocol=dict(status='PRE_INFERENCE_DIAGNOSTIC_PROTOCOL',files=files,observed_val_only=True,models=list(weights),image_stems=sorted(STEMS),imgsz=640,rect=True,device='cpu',prediction_confidence=.001,nms_iou=.7,max_det=8,operating_confidence=.35,iou_match=.5,test_used=False,checkpoint_selection=False,threshold_selection=False,class_filter='Native plant ID before NMS max_det, matching actual scene allowlist',deployment_accepted=False)
 (out/'protocol.json').write_text(json.dumps(protocol,indent=2),encoding='utf-8');results={}
 for version,weight in weights.items():
  model=YOLO(weight);plant=[i for i,name in model.names.items() if name.lower() in ['potted plant','pottedplant']];assert len(plant)==1;plant=plant[0];images=[]
  for row in rows:
   image=cv2.imread(str(data/rel(row['image'])));h,w=image.shape[:2];start=time.perf_counter();pred=model.predict(image,predictor=TracePredictor,imgsz=640,rect=True,device='cpu',conf=.001,iou=.7,max_det=8,classes=[plant],verbose=False)[0];elapsed=(time.perf_counter()-start)*1000
   raw=model.predictor.trace_raw;assert raw.ndim==3 and raw.shape[0]==1 and raw.shape[1]==4+len(model.names)
   anchors=raw[0].T;xyxy=ops.xywh2xyxy(anchors[:,:4]);xyxy=ops.scale_boxes(model.predictor.trace_shape,xyxy,(h,w)).numpy();scores=anchors[:,4:].numpy();winner=scores.argmax(1)
   # Re-run the exact captured tensor with a larger output cap solely to diagnose max_det/NMS survival.
   expanded=nms.non_max_suppression(raw.clone(),.001,.7,max_det=300,nc=0,classes=[plant])[0];expanded[:,:4]=ops.scale_boxes(model.predictor.trace_shape,expanded[:,:4],(h,w))
   actual=pred.boxes.data.cpu().numpy();expanded=expanded.numpy();truth=[]
   for line in (data/rel(row['label'])).read_text().splitlines():
    c,cx,cy,bw,bh=map(float,line.split())
    if int(c)==0:truth.append(np.array([(cx-bw/2)*w,(cy-bh/2)*h,(cx+bw/2)*w,(cy+bh/2)*h]))
   traces=[]
   for target in truth:
    iou=overlaps(xyxy,target);matching=np.flatnonzero(iou>=.5);ranked=matching[np.argsort(-scores[matching,plant])][:3]
    top=[dict(box_xyxy=xyxy[i].tolist(),iou=float(iou[i]),plant_score=float(scores[i,plant]),winning_class=model.names[int(winner[i])],winning_score=float(scores[i,winner[i]])) for i in ranked]
    def retained(dets):
     matching=overlaps(dets[:,:4],target)>=.5
     return [dict(box_xyxy=d[:4].tolist(),score=float(d[4]),native_class=int(d[5]),name=model.names[int(d[5])]) for d in dets[matching]]
    traces.append(dict(truth_xyxy=target.tolist(),raw_anchors_matching_iou50=len(matching),top_matching_raw_plant_candidates=top,raw_plant_winner_above_operating_confidence=int(sum((scores[matching,plant]>=.35)&(winner[matching]==plant))),post_nms_max8_matches=retained(actual),post_nms_max300_matches=retained(expanded)))
   images.append(dict(image=row['image'],actual_input_shape=model.predictor.trace_shape,raw_shape=list(raw.shape),all_max8_detections=actual.tolist(),ground_truth_traces=traces,inference_ms=elapsed));print(version,Path(row['image']).name,'traced',flush=True)
  results[version]=dict(weight_sha256=sha(weight),plant_native_id=plant,model_names=model.names,images=images)
 for row in files:assert sha(ROOT/row['path'])==row['sha256']
 report=dict(status='ACTUAL_RAW_AND_POST_NMS_DIAGNOSTIC_COMPLETE',results=results,protocol_sha256=sha(out/'protocol.json'),all_frozen_files_verified=len(files),torch=torch.__version__,ultralytics=ultralytics.__version__,test_used=False,checkpoint_selection=False,threshold_changed=False,deployment_accepted=False,limitations=['Three repeatedly observed development VAL photos; not generalization or acceptance evidence.','Independent per-truth raw candidates are not one-to-one detection matching.','Raw anchor evidence can distinguish missing scores/geometry/class winners; does not by itself establish unique training-data causality.','CPU timing includes initialization; not PICO latency.'])
 (out/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(report['status'],flush=True)
if __name__=='__main__':main()
