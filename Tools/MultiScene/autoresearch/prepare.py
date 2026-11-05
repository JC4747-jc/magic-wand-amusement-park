"""Freeze and verify project YOLO experiment inputs; never edit datasets or deploy."""
import argparse,json,hashlib,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'Tools/MultiScene'))
from dataset_paths import manifest_relative_path
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def verify(contract,root=ROOT):
 for r in contract['files']:
  p=root.joinpath(*manifest_relative_path(r['path']).parts)
  if not p.is_file() or sha(p)!=r['sha256']:raise ValueError('Frozen experiment input changed: '+r['path'])
 return dict(status='FROZEN_INPUTS_VERIFIED',files=len(contract['files']),deployment_accepted=False)
def freeze(dataset,out,root=ROOT):
 dataset=dataset.resolve();out=out.resolve()
 if out.exists():raise ValueError('Preserve existing experiment contract')
 a=json.loads((dataset/'audit.json').read_text());assert a['training_approved'];hold=dataset/'review_hold.json'
 if hold.exists() and json.loads(hold.read_text()).get('status')=='OPEN':raise ValueError('Open annotation hold')
 paths={dataset/'audit.json',dataset/'data.yaml',dataset/'native-val.txt',root/'Tools/MultiScene/evaluate_mapped.py',root/'Tools/MultiScene/dataset_paths.py',root/'Tools/MultiScene/runtime_checkpoint_selection.py',Path(__file__).resolve()}
 for r in a['samples']:
  for k,digest in [('image','sha256'),('label','label_sha256')]:
   p=dataset.joinpath(*manifest_relative_path(r[k]).parts);assert sha(p)==r[digest];paths.add(p)
 paths.add(root/'object-detection-demo/weights/candidates/plant-native-v12/best.pt');paths.add(root/'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt')
 for p in (root/'object-detection-demo').glob('scene_profiles*.json'):paths.add(p)
 files=[dict(path=p.relative_to(root).as_posix(),sha256=sha(p)) for p in sorted(paths)]
 c=dict(format_version=1,dataset=dataset.relative_to(root).as_posix(),files=files,evaluation=dict(split='val',images=13,confidence=.35,imgsz=640,rect=True,nms_iou=.7,max_det=8,allowed_classes=['PottedPlant']),selection='Gate pass thenF1,P,R,AP95; earliest exact tie; TEST never selected',gates=dict(precision_min=.9,recall_min=.8),editable_training_fields=['lr0','lrf','freeze','scale','translate','hsv_h','hsv_s','hsv_v','optimizer','batch'],budget_policy='Every experiment requires explicit equal budget and run count; no unbounded search or concurrent GPU jobs',failure_policy='Retain failed experiments, logs and hashes; never delete rejected evidence or overwrite deployment',test_policy='Repeated validation search is development only; fresh source-grouped independent TEST required before pilot acceptance',deployment_accepted=False)
 verify(c,root);out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(c,indent=2));return c
def main():
 p=argparse.ArgumentParser();p.add_argument('--dataset',default='Datasets/plant-reviewed-v20');p.add_argument('--contract',required=True);p.add_argument('--verify',action='store_true');a=p.parse_args();contract=ROOT/a.contract
 c=json.loads(contract.read_text()) if a.verify else freeze(ROOT/a.dataset,contract);print(json.dumps(verify(c)))
if __name__=='__main__':main()
