"""Add reviewed confuser/negative TRAIN photos without changing held-out labels."""
import json,hashlib,shutil,collections
from pathlib import Path
from audit_plant_cutflower_v11_candidates import norm
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 parent=ROOT/'Datasets/plant-broader-v14';stage=ROOT/'Datasets/plant-cut-arrangement-v15-staging';out=ROOT/'Datasets/plant-confuser-v15';assert not out.exists();assert sha(parent/'audit.json')=='2d62c0ad3ce35b80d9c402b71924c94ff1c9bc276bb813366d519881016b7339';a=json.loads((parent/'audit.json').read_text());m=json.loads((stage/'manifest.json').read_text());cp=ROOT/'Reports/plant-cut-arrangement-v15-clearance.json';c=json.loads(cp.read_text());assert c['status']=='SOURCE_AND_PIXEL_CLEARANCE_PASSED' and c['source_manifest_sha256']==sha(stage/'manifest.json')
 for name,key in [('plant-cut-arrangement-v15-source-reservation-audit.json','metadata_report_sha256'),('plant-cut-arrangement-v15-pixel-history.json','pixel_report_sha256')]:assert sha(ROOT/'Reports'/name)==c[key]
 attribution_path=ROOT/'Reports/plant-broader-v14-attribution-recovery.json';recovery=json.loads(attribution_path.read_text());assert recovery['dataset_audit_sha256']==sha(parent/'audit.json') and recovery['unresolved']==0;attrs={r['original_id']:r for r in recovery['samples']}
 ids={r['original_id'] for r in a['samples']};groups={norm(r['group']) for r in a['samples']};rows=[]
 for r in a['samples']:
  for key,digest in [('image','sha256'),('label','label_sha256')]:assert sha(parent/r[key])==r[digest]
 for r in m['samples']:
  assert r['original_id'] not in ids and norm(r['group']) not in groups;ids.add(r['original_id']);groups.add(norm(r['group']));assert r['reserved_split']=='train' and r['review_approved']
  for key,digest in [('image','sha256'),('label','label_sha256')]:assert sha(stage/r[key])==r[digest]
 assert len(m['samples'])==10
 for old in a['samples']:
  r=dict(old);r['attribution']=attrs[r['original_id']]['attribution']
  for key in ('image','label'):
   p=out/r[key];p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(parent/r[key],p)
  rows.append(r)
 for old in m['samples']:
  r=dict(old);r.update(split='train',image='images/train/'+Path(old['image']).name,label='labels/train/'+Path(old['label']).name,auxiliary_instances=sum(line.split()[0]=='1' for line in (stage/old['label']).read_text().splitlines()))
  for key in ('image','label'):shutil.copy2(stage/old[key],out/r[key])
  rows.append(r)
 shutil.copy2(parent/'native-val.txt',out/'native-val.txt');a.update(samples=rows,unique_images=len(rows),counts={s:sum(r['split']==s for r in rows) for s in ('train','val','test')},parent_audit_sha256=sha(parent/'audit.json'),confuser_addition_clearance_sha256=sha(cp),attribution_recovery_sha256=sha(attribution_path),training_approved=True,scope='Add6reviewedCutArrangementinstances+4negativeTRAINphotos. All90parentimages/labels/folds unchanged. Per-image attribution recovered for19parentrows with exact source evidence.',deployment_accepted=False)
 (out/'audit.json').write_text(json.dumps(a,indent=2));(out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='native-val.txt',test='images/test',names=a['names']),indent=2));counts=collections.Counter()
 for r in rows:
  for line in (out/r['label']).read_text().splitlines():counts[(r['split'],a['names'][int(line.split()[0])])]+=1
 report=dict(status='PASS_PARENT_LABELS_FOLDS_AND_CLEARED_ADDITIONS',dataset_sha256=sha(out/'audit.json'),counts=a['counts'],instances={str(k):v for k,v in counts.items()},parent_images_labels_folds_retained=90,recovered_parent_attributions=19,new_images=10,mandatory_target_val_images=13,native_fitness_images=12,training_started=False,test_inference_performed=False);(ROOT/'Reports/plant-confuser-v15-release-integrity.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
if __name__=='__main__':main()
