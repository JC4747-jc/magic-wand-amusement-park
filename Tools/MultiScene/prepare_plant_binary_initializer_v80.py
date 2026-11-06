"""Derive plant-only head from verified v55 initializer; preserve every retained tensor."""
from copy import deepcopy
import hashlib,inspect,json
from pathlib import Path
from types import SimpleNamespace
import cv2,torch,ultralytics
from ultralytics import YOLO
from ultralytics.models.yolo.detect import DetectionTrainer
from ultralytics.nn.tasks import BaseModel
from dataset_paths import manifest_relative_path as rel
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 source=ROOT/'Reports/plant-official-initializer-v55/initializer.pt';digest=sha(source);assert digest=='e155a229ca5c197b40e2f2728d900bc495beca748895fb308950b4dc714595d3'
 out=ROOT/'Reports/plant-binary-initializer-v80';assert not out.exists();wrapper=YOLO(source);original=wrapper.model.float();assert original.names=={0:'PottedPlant',1:'CutArrangement'};before={k:v.clone() for k,v in original.state_dict().items()}
 trainer=object.__new__(DetectionTrainer);trainer.data=dict(nc=1,channels=3,names={0:'PottedPlant'});trainer.args=SimpleNamespace(cls_remap=True);torch.manual_seed(20261105)
 model=trainer.get_model(cfg=original.yaml,weights=original,verbose=False);assert model.names=={0:'PottedPlant'} and model.model[-1].nc==1
 prefix=f'model.{len(model.model)-1}.cv3.';mapped=[];unchanged=0
 for k,v in model.state_dict().items():
  old=before[k]
  if v.shape!=old.shape:
   assert k.startswith(prefix) and old.shape[0]==2 and v.shape[0]==1 and old.shape[1:]==v.shape[1:];assert torch.equal(v[0],old[0]);mapped.append(k)
  else:assert torch.equal(v,old),k;unchanged+=1
 assert len(mapped)==6 and all(b[-1].in_channels==192 for b in model.model[-1].cv3)
 out.mkdir();wrapper.model=model;wrapper.save(out/'initializer.pt');loaded=YOLO(out/'initializer.pt').model.float();again=trainer.get_model(cfg=loaded.yaml,weights=loaded,verbose=False)
 assert all(torch.equal(v,loaded.state_dict()[k]) for k,v in again.state_dict().items())
 data=ROOT/'Datasets/plant-binary-v79';audit=json.loads((data/'audit.json').read_text());train=[r for r in audit['samples'] if r['split']=='train' and not r['negative']][:2];original.eval();loaded.eval();probes=[]
 with torch.no_grad():
  for row,(height,width) in zip(train,[(320,320),(384,640)]):
   image=data/rel(row['image']);assert sha(image)==row['sha256'];rgb=cv2.cvtColor(cv2.imread(str(image)),cv2.COLOR_BGR2RGB);rgb=cv2.resize(rgb,(width,height));tensor=torch.from_numpy(rgb.copy()).permute(2,0,1).unsqueeze(0).float()/255
   a=original(tensor);b=loaded(tensor);a=a[0] if isinstance(a,(tuple,list)) else a;b=b[0] if isinstance(b,(tuple,list)) else b
   assert a.shape[1]==6 and b.shape[1]==5 and a.shape[2]==b.shape[2]
   boxdiff=float((a[:,:4]-b[:,:4]).abs().max());scorediff=float((a[:,4]-b[:,4]).abs().max());assert boxdiff<=1e-5 and scorediff<=1e-6
   probes.append(dict(training_image=row['image'],image_sha256=row['sha256'],input_shape=list(tensor.shape),box_max_absolute_difference=boxdiff,plant_score_max_absolute_difference=scorediff))
 assert sha(source)==digest
 result=dict(status='PLANT_ONLY_INITIALIZER_RETAINED_TENSORS_AND_FORWARD_VERIFIED',source_initializer_sha256=digest,initializer_sha256=sha(out/'initializer.pt'),source_classes=original.names,target_classes=loaded.names,plant_class_id=0,plant_output_row_tensors_exact=mapped,all_other_state_tensors_exact=unchanged,classifier_hidden_width=192,next_training_load_all_state_exact=True,forward_probes=probes,torch=torch.__version__,ultralytics=ultralytics.__version__,implementation_files=[dict(path=str(p),sha256=sha(p)) for p in [Path(inspect.getfile(DetectionTrainer)),Path(inspect.getfile(BaseModel))]],training_performed=False,test_used=False,deployment_accepted=False,limitations=['Initializer is not a trained/deployable best.pt.','Removing auxiliary output changes task/loss dimensionality; a later comparison cannot isolate a unique class-competition mechanism.','Two TRAIN probes establish retained forward behavior on those inputs only.'])
 (out/'report.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
if __name__=='__main__':main()
