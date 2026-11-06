"""Verify official COCO58 -> canonical plant0 transfer before bounded training."""
from copy import deepcopy
import hashlib,inspect,json
from pathlib import Path
from types import SimpleNamespace
import torch,ultralytics
from ultralytics import YOLO
from ultralytics.models.yolo.detect import DetectionTrainer
from ultralytics.nn.tasks import BaseModel
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 source=ROOT/'object-detection-demo/weights/candidates/common-yolov8m-official-v1/best.pt'
 digest=sha(source);assert digest=='5d4a90cdc7a21786cc59cd19778e9eafff836df9e2da32524737c7ee6efe4fe5'
 out=ROOT/'Reports/plant-official-initializer-v55';assert not out.exists()
 wrapper=YOLO(source);original=wrapper.model.float();assert original.names[58]=='potted plant'
 before={k:v.clone() for k,v in original.state_dict().items()}
 renamed=deepcopy(original);renamed.names[58]='PottedPlant'
 trainer=object.__new__(DetectionTrainer)
 trainer.data=dict(nc=2,channels=3,names={0:'PottedPlant',1:'CutArrangement'})
 trainer.args=SimpleNamespace(cls_remap=True)
 torch.manual_seed(20261105)
 model=trainer.get_model(cfg=renamed.yaml,weights=renamed,verbose=False)
 assert model.names==trainer.data['names'] and model.model[-1].nc==2
 prefix=f'model.{len(model.model)-1}.cv3.'
 mapped=[];unchanged=0
 for k,v in model.state_dict().items():
  old=before[k]
  if k.startswith(prefix) and v.shape!=old.shape:
   assert old.shape[0]==80 and v.shape[0]==2 and v.shape[1:]==old.shape[1:]
   assert torch.equal(v[0],old[58]),k
   assert not torch.equal(v[1],old[58]),k
   mapped.append(k)
  else:assert torch.equal(v,old),k;unchanged+=1
 assert len(mapped)==6 and all(b[-1].in_channels==192 for b in model.model[-1].cv3)
 out.mkdir();wrapper.model=model;wrapper.save(out/'initializer.pt')
 loaded=YOLO(out/'initializer.pt').model.float()
 for k in mapped:assert torch.equal(loaded.state_dict()[k][0],before[k][58]),k
 # The next training build must preserve the serialized initializer as well.
 again=trainer.get_model(cfg=loaded.yaml,weights=loaded,verbose=False)
 assert all(torch.equal(v,loaded.state_dict()[k]) for k,v in again.state_dict().items())
 assert sha(source)==digest
 impl=[Path(inspect.getfile(DetectionTrainer)),Path(inspect.getfile(BaseModel))]
 report=dict(status='OFFICIAL_PLANT_ROW_TRANSFER_VERIFIED',source_sha256=digest,
  initializer_sha256=sha(out/'initializer.pt'),native_class_id=58,target_class_id=0,
  alias_change='In-memory source name potted plant -> PottedPlant before trainer load; no source checkpoint mutation.',
  classifier_hidden_width=192,plant_row_tensors_exact=mapped,other_state_tensors_unchanged=unchanged,
  auxiliary_initialization='New independent class row with fixed seed20261105, not duplicated plant weights.',
  save_reload_plant_row_exact=True,next_trainer_load_all_state_exact=True,
  torch=torch.__version__,ultralytics=ultralytics.__version__,
  implementation_files=[dict(path=str(p),sha256=sha(p)) for p in impl],
  deployment_accepted=False,training_performed=False,
  limitations=['Parameter transfer does not establish accuracy improvement or preserve all 80-class capabilities.',
               'Auxiliary row is untrained; this initializer is not a deployable recognition model.'])
 (out/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
 print(json.dumps(report))
if __name__=='__main__':main()
