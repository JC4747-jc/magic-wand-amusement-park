"""Merge two reviewed TRAIN container photos with current reservation/pixel evidence."""
import json,hashlib,shutil
from pathlib import Path
from dataset_paths import manifest_relative_path as rel
from source_author_identity import reservation_author_keys
from dataset_pipeline import decoded_pixel_digest
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
 parent=ROOT/'Datasets/plant-reviewed-v26';out=ROOT/'Datasets/plant-reviewed-v28';source=ROOT/'Datasets/plant-container-train-source-v27';review=ROOT/'Reports/plant-container-train-v27-review/two-container-reviewed.json'
 assert not out.exists() and sha(parent/'audit.json')=='36431947142b1e9ead7e5a607738e7a18910e167bce53ad9139d7e534e06353b'
 a=read(parent/'audit.json');m=read(source/'manifest.json');d=read(review);assert d['training_approved'] and len(d['samples'])==2
 ids={x['original_id'] for x in m['samples']};authors=set().union(*(reservation_author_keys(x) for x in m['samples']));assert len(ids)==len(authors)==2
 metadata=ROOT/'Reports/plant-container-train-v27-reservations.json';reservations=read(metadata);assert reservations['source_manifest_sha256']==sha(source/'manifest.json')
 for item in reservations['inputs']:assert sha(ROOT/rel(item['path']))==item['sha256']
 # Resolve inherited split for nested attribution, held samples, and source-only reservations.
 evidence=[];hits=[]
 def walk(value,split=None):
  if isinstance(value,dict):
   split=value.get('split') or value.get('reserved_split') or value.get('project_reserved_split') or split
   if split in ('val','test') and (value.get('original_id') in ids or reservation_author_keys(value)&authors):hits.append(dict(split=split,original_id=value.get('original_id'),group=value.get('group')))
   for child in value.values():walk(child,split)
  elif isinstance(value,list):
   for child in value:walk(child,split)
 for name in ['manifest.json','audit.json','selection.json']:
  for path in (ROOT/'Datasets').glob('*/'+name):
   evidence.append(dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(path)));walk(read(path))
 assert not hits,hits
 pixel=ROOT/'Reports/plant-container-train-v27-pixel-history.json';px=read(pixel)
 assert px['source_manifest_sha256']==sha(source/'manifest.json') and px['scanned_paths']==px['enumerated_paths']==18052 and not px['errors'] and not px['internal_matches'] and len(px['matches'])==2
 for hit in px['matches']:
  assert hit['encoded_exact'] and hit['decoded_exact'] and hit['dhash_distance']==0
  path=rel(hit['historical_image']);assert path.parts[0]=='plant-container-train-source-v26'
  assert sha(ROOT/'Datasets'/path)==hit['historical_sha256']
 proof=ROOT/'Reports/plant-container-train-v27-clearance.json';assert not proof.exists();proof.write_text(json.dumps(dict(status='PASS',source_manifest_sha256=sha(source/'manifest.json'),metadata_report_sha256=sha(metadata),pixel_report_sha256=sha(pixel),cross_val_test_hits=hits,ancestor_split_checked_inputs=evidence,pixel_matches_scope='Two exact original TRAIN-reserved source copies only',limitation='Recorded source identity and pixels only; unknown aliases/crops/physical subject/pretraining not excluded.'),indent=2),encoding='utf-8')
 old=list(a['samples']);seen={x['sha256'] for x in old};seen_authors=set().union(*(reservation_author_keys(x) for x in old));assert not seen_authors&authors
 for row in old:
  for key,digest in [('image','sha256'),('label','label_sha256')]:
   p=parent/rel(row[key]);assert sha(p)==row[digest];dst=out/rel(row[key]);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dst);assert sha(dst)==row[digest]
 count=0
 for b in d['samples']:
  assert b['original_reviewed'] and b['overlay_reviewed'] and b['review_approved']
  image=ROOT/rel(b['image']);label=ROOT/rel(b['label']);assert sha(image)==b['image_sha256']==b['sha256'] and sha(label)==b['label_sha256'] and sha(ROOT/rel(b['overlay']))==b['overlay_sha256']
  assert b['attribution']['License']=='https://creativecommons.org/licenses/by/2.0/' and b['reserved_split']=='train' and b['sha256'] not in seen
  lines=label.read_text().splitlines();assert all(x.split()[0]=='0' for x in lines);count+=len(lines)
  ip=f"images/train/{b['sha256'][:24]}.jpg";lp=f"labels/train/{b['sha256'][:24]}.txt";shutil.copy2(image,out/ip);shutil.copy2(label,out/lp)
  a['samples'].append(dict(original_id=b['original_id'],image=ip,label=lp,sha256=sha(image),label_sha256=sha(label),decoded_rgb_sha256=decoded_pixel_digest(image),source='openimages-v7',group=b['group'],attribution=b['attribution'],split='train',negative=False,auxiliary_instances=0,review=review.relative_to(ROOT).as_posix()))
 assert count==3 and len(a['samples'])==136
 pending=[r for r in a['pending_train_samples'] if r['original_id'] not in ids];assert len(pending)==26
 shutil.copy2(parent/'native-val.txt',out/'native-val.txt')
 a.update(unique_images=136,counts=dict(train=119,val=13,test=4),parent_audit_sha256=sha(parent/'audit.json'),pending_train_samples=pending,training_approved=True,deployment_accepted=False,scope='v26 plus two reviewed TRAIN photos/three targets; all134parent image/label/group/fold bytes and native-val list unchanged. Historical source holds retained except two explicitly resolved candidates.')
 a.setdefault('context_evidence',{}).update({p.relative_to(ROOT).as_posix():sha(p) for p in [source/'manifest.json',review,metadata,pixel,proof,Path(__file__).resolve()]})
 (out/'audit.json').write_text(json.dumps(a,indent=2),encoding='utf-8');(out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='native-val.txt',test='images/test',names=a['names']),indent=2),encoding='utf-8')
 result=dict(status='PASS',photos=136,counts=a['counts'],new_target_instances=3,pending_train_sources=26,dataset_sha256=sha(out/'audit.json'),deployment_accepted=False)
 (ROOT/'Reports/plant-reviewed-v28-release-integrity.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
if __name__=='__main__':main()
