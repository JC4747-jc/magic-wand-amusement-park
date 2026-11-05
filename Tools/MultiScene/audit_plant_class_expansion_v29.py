"""Inspect actual installed trainer class expansion without training or mutating checkpoints."""
import json,hashlib,inspect
from pathlib import Path
from types import SimpleNamespace
import torch,ultralytics
from ultralytics import YOLO
from ultralytics.models.yolo.detect import DetectionTrainer
from ultralytics.nn.tasks import BaseModel
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 source=ROOT/'object-detection-demo/weights/candidates/plant-native-v12/best.pt';digest=sha(source)
 assert digest=='fe706e1763a8ffa5924c849d9798bb1083d416df6c8c6720cb6ffcdb3780177a'
 original=YOLO(source).model.float();before={k:v.clone() for k,v in original.state_dict().items()}
 trainer=object.__new__(DetectionTrainer);trainer.data=dict(nc=2,channels=3,names={0:'PottedPlant',1:'CutArrangement'});trainer.args=SimpleNamespace(cls_remap=True)
 torch.manual_seed(20261105);model=trainer.get_model(cfg=original.yaml,weights=original,verbose=False)
 assert model.names==trainer.data['names'] and model.model[-1].nc==2
 prefix=f'model.{len(model.model)-1}.cv3.';rows=[];unchanged=0
 for k,v in model.state_dict().items():
  if k.startswith(prefix) and k in before and v.shape!=before[k].shape:
   assert v.shape[0]==2 and before[k].shape[0]==1 and v.shape[1:]==before[k].shape[1:]
   assert torch.equal(v[0],before[k][0]),k
   rows.append(dict(key=k,plant_row_exact=True,new_auxiliary_row_distinct=not torch.equal(v[1],before[k][0])))
  else:assert k in before and torch.equal(v,before[k]),k;unchanged+=1
 assert len(rows)==6 and all(r['new_auxiliary_row_distinct'] for r in rows)
 assert sha(source)==digest and all(torch.equal(v,before[k]) for k,v in original.state_dict().items())
 paths=[Path(inspect.getfile(DetectionTrainer)),Path(inspect.getfile(BaseModel))]
 out=ROOT/'Reports/plant-class-expansion-v29-audit.json';assert not out.exists()
 result=dict(status='PASS_CLASS0_PRESERVED_NEW_AUXILIARY_INITIALIZED',torch=torch.__version__,ultralytics=ultralytics.__version__,source_sha256=digest,actual_trainer_method='DetectionTrainer.get_model with target names before BaseModel.load',dataset_names=trainer.data['names'],classification_hidden_widths=[b[-1].in_channels for b in model.model[-1].cv3],unchanged_other_tensors=unchanged,expanded_classification_tensors=rows,implementation_files=[dict(path=str(p),sha256=sha(p)) for p in paths],scope='Actual model construction only, not an optimizer step, full training reproduction, accuracy or PICO acceptance.',conclusion='Installed class-name remapping preserves plant class0; no evidence of this proposed initialization bug. Do not modify initializer/head merely based on nc1->2 log.',deployment_accepted=False)
 out.write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
if __name__=='__main__':main()
