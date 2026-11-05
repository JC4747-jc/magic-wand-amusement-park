import json,hashlib
from pathlib import Path
from ultralytics import YOLO
root=Path.cwd(); modelpath=root/'Reports/plant-confuser-v13-training/train/weights/best.pt'; data=root/'Datasets/plant-confuser-v13'
m=YOLO(modelpath); audit=json.loads((data/'audit.json').read_text()); results=[]
for row in audit['samples']:
 if row['split']!='val':continue
 p=data/row['image'].replace('\\','/')
 variants={}
 for name,classes in [('target_only',[0]),('all_native',None)]:
  r=m.predict(str(p),imgsz=640,rect=True,device='cpu',conf=.35,iou=.7,classes=classes,max_det=8,verbose=False)[0]
  variants[name]=[{'class':m.names[int(b.cls.item())],'confidence':float(b.conf.item()),'xyxy':b.xyxy[0].tolist()} for b in r.boxes]
 a=[b for b in variants['all_native'] if b['class']=='PottedPlant']; b=variants['target_only']
 results.append({'image':row['image'],'variants':variants,'target_detections_equal':a==b})
out={'scope':'Full13VAL diagnostic of class filtering at frozen deployment settings; not fresh TEST or threshold selection','weight_sha256':hashlib.sha256(modelpath.read_bytes()).hexdigest(),'confidence':.35,'images':len(results),'all_target_predictions_equal':all(r['target_detections_equal'] for r in results),'results':results,'service_changed':False,'pico_validated':False}
(root/'Reports/plant-confuser-v13-class-filter-diagnostic.json').write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps({k:v for k,v in out.items() if k!='results'}))
