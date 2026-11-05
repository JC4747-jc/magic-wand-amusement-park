"""Apply visually approved TRAIN-only label patches to a new immutable data version."""
import json,hashlib,shutil,collections
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 parent=ROOT/'Datasets/plant-rooted-v18';out=ROOT/'Datasets/plant-instance-v19';patch=ROOT/'Reports/plant-train-instance-audit-v19/corrections/reviewed.json';assert not out.exists();a=json.loads((parent/'audit.json').read_text());review=json.loads(patch.read_text());assert review['parent_dataset_sha256']==sha(parent/'audit.json')
 patches={r['original_id']:r for r in review['samples']};assert len(patches)==len(review['samples'])==2
 for old in a['samples']:
  for k,digest in [('image','sha256'),('label','label_sha256')]:assert sha(parent/old[k])==old[digest]
  if old['original_id'] in patches:
   r=patches[old['original_id']];assert old['split']==r['split']=='train' and r['overlay_reviewed'] and r['visual_annotation_approved'];assert old['sha256']==r['source_sha256'] and old['label_sha256']==r['parent_label_sha256'];assert sha(ROOT/r['label'])==r['label_sha256'] and sha(ROOT/r['overlay'])==r['overlay_sha256']
 rows=[];changes=[]
 for old in a['samples']:
  r=dict(old)
  for key in ('image','label'):
   dest=out/r[key];dest.parent.mkdir(parents=True,exist_ok=True);source=parent/old[key]
   if key=='label' and r['original_id'] in patches:source=ROOT/patches[r['original_id']]['label']
   shutil.copy2(source,dest)
  if r['original_id'] in patches:
   fix=patches[r['original_id']];r.update(label_sha256=sha(out/r['label']),parent_label_sha256=old['label_sha256'],label_correction_reason=fix['reason']);changes.append(dict(original_id=r['original_id'],old_label_sha256=old['label_sha256'],new_label_sha256=r['label_sha256'],old_instances=fix['old_instances'],new_instances=fix['new_instances']))
  rows.append(r)
 shutil.copy2(parent/'native-val.txt',out/'native-val.txt');a.update(samples=rows,parent_audit_sha256=sha(parent/'audit.json'),train_label_patch_sha256=sha(patch),scope='TRAIN-only visible container boundary and three-basket instance corrections. All109images and allfolds unchanged; allVAL/TESTlabels byte-identical. No new model acceptance.',deployment_accepted=False,training_approved=True)
 (out/'audit.json').write_text(json.dumps(a,indent=2));(out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='native-val.txt',test='images/test',names=a['names']),indent=2));counts=collections.Counter()
 for old,r in zip(json.loads((parent/'audit.json').read_text())['samples'],rows):
  assert old['image']==r['image'] and old['split']==r['split'] and sha(out/r['image'])==old['sha256']
  if r['split']!='train':assert (out/r['label']).read_bytes()==(parent/old['label']).read_bytes()
  for line in (out/r['label']).read_text().splitlines():counts[(r['split'],a['names'][int(line.split()[0])])]+=1
 report=dict(status='PASS_REVIEWED_TRAIN_ONLY_PATCHES',dataset_sha256=sha(out/'audit.json'),images=109,counts=a['counts'],instances={str(k):v for k,v in counts.items()},changes=changes,all_image_bytes_retained=True,all_folds_retained=True,all_val_test_labels_retained=True,native_val_list_unchanged=(out/'native-val.txt').read_bytes()==(parent/'native-val.txt').read_bytes(),training_started=False,model_accepted=False)
 (ROOT/'Reports/plant-instance-v19-release-integrity.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
if __name__=='__main__':main()
