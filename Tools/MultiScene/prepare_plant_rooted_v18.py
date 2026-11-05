"""Merge cleared rooted TRAIN additions while preserving every parent byte/fold."""
import json,hashlib,shutil,collections
from pathlib import Path
from audit_plant_cutflower_v11_candidates import norm
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 parent=ROOT/'Datasets/plant-confuser-v15';out=ROOT/'Datasets/plant-rooted-v18';assert not out.exists()
 assert sha(parent/'audit.json')=='e2a8547af666b1fa4519fd11b356828ad70aeff244cd5541ddb3ba7f99dddf18'
 a=json.loads((parent/'audit.json').read_text());rows=[];ids={r['original_id'] for r in a['samples']};groups={norm(r['group']) for r in a['samples']};additions=[];clearances=[]
 for tag,stage_name in [('plant-rooted-v16','plant-rooted-v16-staging'),('plant-rooted-v17','plant-rooted-v17-staging'),('plant-multi-v18','plant-multi-v18-staging')]:
  stage=ROOT/'Datasets'/stage_name;cp=ROOT/'Reports'/f'{tag}-clearance.json';c=json.loads(cp.read_text());assert c['status']=='SOURCE_AND_PIXEL_CLEARANCE_PASSED' and c['source_manifest_sha256']==sha(stage/'manifest.json')
  for suffix,key in [('source-reservation-audit','metadata_report_sha256'),('pixel-history','pixel_report_sha256')]:assert sha(ROOT/'Reports'/f'{tag}-{suffix}.json')==c[key]
  clearances.append(dict(path=str(cp.relative_to(ROOT)),sha256=sha(cp)))
  for r in json.loads((stage/'manifest.json').read_text())['samples']:
   assert r['original_id'] not in ids and norm(r['group']) not in groups;ids.add(r['original_id']);groups.add(norm(r['group']));assert r['reserved_split']=='train' and r['review_approved']
   for key,digest in [('image','sha256'),('label','label_sha256')]:assert sha(stage/r[key])==r[digest]
   additions.append((stage,r))
 assert len(additions)==9
 for r in a['samples']:
  for key,digest in [('image','sha256'),('label','label_sha256')]:assert sha(parent/r[key])==r[digest]
 for old in a['samples']:
  r=dict(old)
  for key in ('image','label'):
   p=out/r[key];p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(parent/r[key],p)
  rows.append(r)
 for stage,old in additions:
  r=dict(old);r.update(split='train',image='images/train/'+Path(old['image']).name,label='labels/train/'+Path(old['label']).name,auxiliary_instances=0)
  for key in ('image','label'):shutil.copy2(stage/old[key],out/r[key])
  rows.append(r)
 shutil.copy2(parent/'native-val.txt',out/'native-val.txt');a.update(samples=rows,unique_images=len(rows),counts={s:sum(r['split']==s for r in rows) for s in ('train','val','test')},parent_audit_sha256=sha(parent/'audit.json'),addition_clearances=clearances,training_approved=True,scope='Nine independently reviewed rooted TRAIN photos,12targetinstances including four overlapping/clipped windowsill containers; all100parentimages/labels/folds unchanged.',deployment_accepted=False)
 (out/'audit.json').write_text(json.dumps(a,indent=2));(out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='native-val.txt',test='images/test',names=a['names']),indent=2));counts=collections.Counter()
 for r in rows:
  for line in (out/r['label']).read_text().splitlines():counts[(r['split'],a['names'][int(line.split()[0])])]+=1
 report=dict(status='PASS_PARENT_LABELS_FOLDS_AND_CLEARED_ADDITIONS',dataset_sha256=sha(out/'audit.json'),counts=a['counts'],instances={str(k):v for k,v in counts.items()},parent_images_labels_folds_retained=100,new_images=9,new_target_instances=12,mandatory_target_val_images=13,native_fitness_images=12,training_started=False,test_inference_performed=False);(ROOT/'Reports/plant-rooted-v18-release-integrity.json').write_text(json.dumps(report,indent=2)+chr(10));print(json.dumps(report))
if __name__=='__main__':main()
