"""Merge six reviewed, cleared TRAIN additions; preserve every parent fold/label."""
import json,hashlib,shutil,collections
from pathlib import Path
from audit_plant_cutflower_v11_candidates import norm
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 parent=ROOT/'Datasets/plant-confuser-v13';out=ROOT/'Datasets/plant-broader-v14';assert not out.exists();assert sha(parent/'audit.json')=='39fe41890c36c832cf9dfcaf2afb72729ca78fb22dc24ac2e294279f80c0f6c3';a=json.loads((parent/'audit.json').read_text());new=[];inputs={}
 specs=[('plant-broader-v14-staging','plant-broader-v14'),('plant-broader-v14-batch2-staging','plant-broader-v14-batch2'),('plant-broader-v14-multi-instance-staging','plant-broader-v14-multi-instance'),('plant-broader-v14-waiting-room-staging','plant-broader-v14-waiting-room'),('plant-broader-v14-palm-staging','plant-broader-v14-palm')]
 ids={r['original_id'] for r in a['samples']};groups={norm(r['group']) for r in a['samples'] if r.get('group')}
 for folder,prefix in specs:
  stage=ROOT/'Datasets'/folder;mp=stage/'manifest.json';m=json.loads(mp.read_text());rp=ROOT/'Reports'/f'{prefix}-pixel-history.json';pixels=json.loads(rp.read_text());mr=ROOT/'Reports'/f'{prefix}-source-reservation-audit.json';meta=json.loads(mr.read_text());assert pixels['source_manifest_sha256']==sha(mp);assert not pixels['errors'] and not pixels['internal_matches'] and not meta['errors'];assert all(r['path'].replace('\\','/')=='Datasets/openimages-plant-fresh-general-source-v4/manifest.json' for r in meta['records']);assert len(pixels['matches'])==len(m['samples'])
  for match in pixels['matches']:assert match['encoded_exact'] and match['decoded_exact'] and match['historical_image'].replace('\\','/')=='openimages-plant-fresh-general-source-v4/'+match['candidate_id']
  for p in (mp,rp,mr):inputs[str(p.relative_to(ROOT))]=sha(p)
  for r in m['samples']:
   assert r['reserved_split']=='train' and r['review_approved'];assert r['original_id'] not in ids and norm(r['group']) not in groups;ids.add(r['original_id']);groups.add(norm(r['group']));assert r['attribution']['License'].rstrip('/')=='https://creativecommons.org/licenses/by/2.0'
   for key,digest in [('image','sha256'),('label','label_sha256')]:assert sha(stage/r[key])==r[digest]
   new.append((stage,r))
 assert len(new)==6
 for r in a['samples']:
  assert sha(parent/r['image'])==r['sha256'] and sha(parent/r['label'])==r['label_sha256']
 for r in a['samples']:
  for key in ('image','label'):
   target=out/r[key];target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(parent/r[key],target)
 rows=list(a['samples'])
 for stage,r in new:
  row=dict(r);row.update(split='train',image='images/train/'+Path(r['image']).name,label='labels/train/'+Path(r['label']).name,negative=False,auxiliary_instances=sum(line.split()[0]=='1' for line in (stage/r['label']).read_text().splitlines()))
  for key in ('image','label'):shutil.copy2(stage/r[key],out/row[key])
  rows.append(row)
 shutil.copy2(parent/'native-val.txt',out/'native-val.txt');a.update(samples=rows,unique_images=len(rows),parent_audit_sha256=sha(parent/'audit.json'),counts={s:sum(r['split']==s for r in rows) for s in ('train','val','test')},training_approved=True,addition_clearance_inputs=inputs,scope='Six independently reviewed TRAIN additions with per-source pixel/metadata clearance; all parent labels and folds retained. Development experiment only; not deployment acceptance.',deployment_accepted=False)
 (out/'audit.json').write_text(json.dumps(a,indent=2));(out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='native-val.txt',test='images/test',names=a['names']),indent=2));counts=collections.Counter()
 for r in rows:
  for line in (out/r['label']).read_text().splitlines():counts[(r['split'],a['names'][int(line.split()[0])])]+=1
 report=dict(status='PASS_PARENT_LABELS_FOLDS_AND_ADDITION_CLEARANCE',dataset_sha256=sha(out/'audit.json'),counts=a['counts'],instances={str(k):v for k,v in counts.items()},new_images=6,parent_rows_unchanged=len(a['samples'])-6,mandatory_target_val=13,native_fitness_val=12,test_inference_performed=False,training_started=False);(ROOT/'Reports/plant-broader-v14-release-integrity.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
if __name__=='__main__':main()
