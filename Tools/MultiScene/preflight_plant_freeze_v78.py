"""Verify installed trainer freeze behavior before a bounded freeze=10 comparison."""
import hashlib,inspect,json,types
from pathlib import Path
import torch,ultralytics
from ultralytics import YOLO
from ultralytics.engine.trainer import BaseTrainer
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 out=ROOT/'Reports/plant-v78-freeze-preflight';assert not out.exists();out.mkdir()
 init=ROOT/'Reports/plant-official-initializer-v55/initializer.pt';assert sha(init)=='e155a229ca5c197b40e2f2728d900bc495beca748895fb308950b4dc714595d3'
 torch.manual_seed(20261105);model=YOLO(init).model.float();prefixes=[f'model.{i}.' for i in range(10)]+['.dfl'];frozen=[];trainable=[]
 for name,param in model.named_parameters():
  param.requires_grad=not any(p in name for p in prefixes)
  (frozen if not param.requires_grad else trainable).append(name)
 assert frozen and trainable
 trainer=types.SimpleNamespace(model=model,freeze_layer_names=prefixes)
 BaseTrainer._model_train(trainer)
 frozen_bn={name:m for name,m in model.named_modules() if isinstance(m,torch.nn.BatchNorm2d) and any(p in name for p in prefixes)}
 active_bn={name:m for name,m in model.named_modules() if isinstance(m,torch.nn.BatchNorm2d) and name not in frozen_bn}
 assert frozen_bn and active_bn and all(not m.training for m in frozen_bn.values()) and all(m.training for m in active_bn.values())
 before={name:{k:v.clone() for k,v in m.named_buffers()} for name,m in {**frozen_bn,**active_bn}.items()}
 with torch.no_grad():model(torch.randn(2,3,64,64))
 unchanged=all(torch.equal(before[name][k],v) for name,m in frozen_bn.items() for k,v in m.named_buffers())
 changed=[name for name,m in active_bn.items() if any(not torch.equal(before[name][k],v) for k,v in m.named_buffers())]
 assert unchanged and changed
 source=Path(inspect.getsourcefile(BaseTrainer));code=inspect.getsource(BaseTrainer._model_train)
 (out/'installed-model-train.txt').write_text(code,encoding='utf-8')
 result=dict(status='INSTALLED_FREEZE10_BN_BEHAVIOR_ACTUALLY_VERIFIED',initializer_sha256=sha(init),trainer_source_path=str(source),trainer_source_sha256=sha(source),method_source_sha256=sha(out/'installed-model-train.txt'),torch=torch.__version__,ultralytics=ultralytics.__version__,frozen_parameter_names=frozen,trainable_parameter_names=trainable,frozen_bn_names=list(frozen_bn),active_bn_names=list(active_bn),frozen_bn_buffers_unchanged_after_actual_forward=unchanged,active_bn_changed_after_forward=changed,actual_forward_shape=[2,3,64,64],training_run_performed=False,deployment_accepted=False,scope='Calls installed _model_train with actual model and freeze-layer names; actual training run must separately verify setup flags, epoch modes, and final tensor lineage.')
 (out/'report.json').write_text(json.dumps(result,indent=2),encoding='utf-8');assert sha(init)==result['initializer_sha256'];print(json.dumps(dict(status=result['status'],frozen_parameters=len(frozen),frozen_bn=len(frozen_bn),active_bn=len(active_bn),changed_active_bn=len(changed))))
if __name__=='__main__':main()
