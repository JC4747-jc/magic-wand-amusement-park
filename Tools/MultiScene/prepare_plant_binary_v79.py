"""Prepare a plant-only task view without changing plant boxes, images or folds."""
import copy,hashlib,json,shutil
from pathlib import Path
from dataset_paths import manifest_relative_path as rel
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text())
def main():
 parent=ROOT/'Datasets/plant-houseplant-v75';out=ROOT/'Datasets/plant-binary-v79';assert not out.exists();assert sha(parent/'audit.json')=='4f2edd8d341ab2b9bdcbcd8bd57f44ce9f3829ca586a5e780b494541618d91fe'
 audit=copy.deepcopy(read(parent/'audit.json'));assert len(audit['samples'])==194;out.mkdir();plant_counts={k:0 for k in ['train','val','test']};removed={k:0 for k in plant_counts};changed=0
 for row in audit['samples']:
  image=parent/rel(row['image']);label=parent/rel(row['label']);assert sha(image)==row['sha256'] and sha(label)==row['label_sha256'];raw=label.read_bytes();lines=raw.splitlines(keepends=True)
  assert all(not line.strip() or line.strip().split()[0] in [b'0',b'1'] for line in lines)
  kept=b''.join(line for line in lines if line.strip() and line.strip().split()[0]==b'0');n=sum(bool(line.strip()) and line.strip().split()[0]==b'1' for line in lines);plant_counts[row['split']]+=len(kept.splitlines());removed[row['split']]+=n
  for key in ['image','label']:(out/rel(row[key])).parent.mkdir(parents=True,exist_ok=True)
  shutil.copy2(image,out/rel(row['image']));(out/rel(row['label'])).write_bytes(kept if n else raw)
  if n:
   changed+=1;row['parent_label_sha256']=row['label_sha256'];row['parent_negative']=row['negative'];row['removed_auxiliary_instances']=n
  row['label_sha256']=sha(out/rel(row['label']));row['negative']=not bool(kept.strip());row['auxiliary_instances']=0
  assert sha(out/rel(row['image']))==row['sha256']
  assert [l for l in (out/rel(row['label'])).read_bytes().splitlines() if l.strip()]==[l for l in raw.splitlines() if l.strip() and l.split()[0]==b'0']
 audit.update(parent_audit_sha256=sha(parent/'audit.json'),scope='Plant-only task view of v75: all194 images, source identities, groups, folds and class0 label lines unchanged. Auxiliary class1 labels omitted from task, so cut arrangements remain image hard negatives for rooted PottedPlant. This is not a correction of historical labels or evidence that the full image contains no objects.',names=['PottedPlant'],training_approved=False,deployment_accepted=False)
 audit['task_projection']=dict(parent_class_names=['PottedPlant','CutArrangement'],names=['PottedPlant'],source_to_target={'0':0,'1':None},changed_label_files=changed,plant_instances_preserved=plant_counts,auxiliary_annotations_removed=removed,all_images_and_folds_unchanged=True,class0_label_lines_exact=True,parent_labels_preserved=True)
 audit.setdefault('context_evidence',{})['Tools/MultiScene/prepare_plant_binary_v79.py']=sha(Path(__file__).resolve())
 (out/'audit.json').write_text(json.dumps(audit,indent=2),encoding='utf-8');config=read(parent/'data.yaml');config.update(path=str(out),names=['PottedPlant']);(out/'data.yaml').write_text(json.dumps(config,indent=2),encoding='utf-8')
 hold=read(parent/'review_hold.json');hold['dataset_audit_sha256']=sha(out/'audit.json');(out/'review_hold.json').write_text(json.dumps(hold,indent=2),encoding='utf-8');shutil.copy2(parent/'legacy-native-val.txt',out/'legacy-native-val.txt')
 report=dict(status='PLANT_ONLY_TASK_VIEW_PREPARED_WITH_OPEN_HOLD',dataset_sha256=sha(out/'audit.json'),images=194,counts=audit['counts'],**audit['task_projection'],training_approved=False,deployment_accepted=False,test_inference_performed=False)
 (ROOT/'Reports/plant-binary-v79-integrity.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
if __name__=='__main__':main()
