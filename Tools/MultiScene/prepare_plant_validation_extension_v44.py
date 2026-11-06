"""Prepare reviewed VAL extension with explicit OPEN coverage hold; no training release."""
from pathlib import Path
import json,hashlib,shutil
from dataset_paths import manifest_relative_path as rel
from source_author_identity import reservation_author_keys
from dataset_pipeline import decoded_pixel_digest
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
 parent=ROOT/'Datasets/plant-validation-extension-v42';source=ROOT/'Datasets/plant-herb-val-source-v43';review=ROOT/'Reports/plant-herb-val-v43-boxes/reviewed-three.json';out=ROOT/'Datasets/plant-validation-extension-v44'
 assert not out.exists() and sha(parent/'audit.json')=='1c177df140b7420c6e75ab6cf3e7b2889b8a2a08c363d1a1e08a51ac63b408a2'
 a=read(parent/'audit.json');m=read(source/'manifest.json');r=read(review);ids={b['page_id'] for b in r['samples']};assert ids=={90070712,63522100,5882114}
 rows=[x for x in m['samples'] if x['page_id'] in ids];groups={x['group'] for x in rows};authors=set().union(*(reservation_author_keys(x) for x in rows));assert len(groups)==3 and authors
 identity=ROOT/'Reports/plant-herb-val-v43-identity-history-v2.json';ident=read(identity);assert ident['source_manifest_sha256']==sha(source/'manifest.json') and not ident['errors'];assert all(not x['historical_hits'] for x in ident['rows'] if x['page_id'] in ids)
 pixel=ROOT/'Reports/plant-herb-val-v43-pixel-history.json';px=read(pixel);assert px['source_manifest_sha256']==sha(source/'manifest.json') and px['scanned_paths']==px['enumerated_paths']==18951 and not any(px[k] for k in ('errors','matches','internal_matches'))
 links=ROOT/'Reports/plant-herb-val-v43-creator-links.json';lk=read(links);assert not lk['collisions'] and not lk['missing_creator_link_indices']
 reservation=read(source/'val-reservation.json');assert reservation['reserved_split']=='val' and reservation['source_manifest_sha256']==sha(source/'manifest.json')
 hits=[];history=[]
 def walk(v,split=None):
  if isinstance(v,dict):
   split=v.get('split') or v.get('reserved_split') or v.get('project_reserved_split') or split
   if split in ('train','test') and (v.get('page_id') in ids or v.get('group') in groups or reservation_author_keys(v)&authors):hits.append(dict(split=split,group=v.get('group'),page_id=v.get('page_id')))
   for child in v.values():walk(child,split)
  elif isinstance(v,list):
   for child in v:walk(child,split)
 for name in ('audit.json','manifest.json','selection.json'):
  for p in (ROOT/'Datasets').glob('*/'+name):history.append(dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)));walk(read(p))
 assert not hits,hits
 old=list(a['samples']);assert len(old)==152
 for row in old:
  for key,digest in [('image','sha256'),('label','label_sha256')]:
   p=parent/rel(row[key]);assert sha(p)==row[digest];dst=out/rel(row[key]);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dst);assert sha(dst)==row[digest]
 instances=0
 for b in r['samples']:
  assert b['original_reviewed'] and b['overlay_reviewed'] and b['annotation_approved'] and b['reserved_split']=='val'
  s=next(x for x in rows if x['page_id']==b['page_id']);image=ROOT/rel(b['image']);label=ROOT/rel(b['label'])
  for p,digest in [(image,b['image_sha256']),(label,b['label_sha256']),(ROOT/rel(b['overlay']),b['overlay_sha256'])]:assert sha(p)==digest
  assert sha(image)==s['sha256'] and s['sha256'] not in {x['sha256'] for x in old}
  lines=label.read_text().splitlines();instances+=len(lines);assert all(x.split()[0]=='0' for x in lines)
  ip=f"images/val/{s['sha256'][:24]}.jpg";lp=f"labels/val/{s['sha256'][:24]}.txt";shutil.copy2(image,out/ip);shutil.copy2(label,out/lp)
  a['samples'].append(dict(image=ip,label=lp,sha256=sha(image),label_sha256=sha(label),decoded_rgb_sha256=decoded_pixel_digest(image),split='val',source='commons',group=s['group'],original_id=str(s['page_id']),page_id=s['page_id'],negative=False,auxiliary_instances=0,artist=s['artist'],publisher_metadata=s['publisher_metadata'],attribution=dict(Author=s['artist'],License=s['license_url'],OriginalLandingURL=s['description_url'],Title=s['title']),review=review.relative_to(ROOT).as_posix(),validation_cohort='herb-v43'))
 assert instances==4
 shutil.copy2(parent/'legacy-native-val.txt',out/'legacy-native-val.txt')
 a.setdefault('held_val_samples',[]).extend(dict(s,reserved_split='val',reason='Exact creator history conflict or incomplete dense-container census; not adopted') for s in m['samples'] if s['page_id'] not in ids)
 a.update(unique_images=155,counts=dict(train=125,val=26,test=4),parent_audit_sha256=sha(parent/'audit.json'),training_approved=False,deployment_accepted=False,scope='Prepared VAL extension only: three herb-container photos/four targets; original152rows/payloads/folds unchanged. OPEN coverage hold prevents training release. Remaining scene coverage still incomplete.',target_runtime_val_images=26)
 a['herb_val_extension_clearance']=dict(cross_train_test_hits=hits,historical_inputs=history,evidence={p.relative_to(ROOT).as_posix():sha(p) for p in [review,source/'manifest.json',identity,pixel,links,Path(__file__).resolve()]},limitations='Recorded identity/pixels only, unknown aliases/physical instances/pretraining not excluded.')
 (out/'audit.json').write_text(json.dumps(a,indent=2),encoding='utf-8');(out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='images/val',test='images/test',names=a['names']),indent=2),encoding='utf-8')
 hold=dict(status='OPEN',dataset_audit_sha256=sha(out/'audit.json'),reason='Prospective validation coverage incomplete: thirteen cumulative source holds and harvested-leaf validation still incomplete. Herb/vessel/cut coverage remains a small pilot. Current single-assistant annotations not independently double reviewed.',training_approved=False)
 (out/'review_hold.json').write_text(json.dumps(hold,indent=2),encoding='utf-8')
 result=dict(status='PREPARED_WITH_OPEN_HOLD',counts=a['counts'],inherited_payloads_verified=152,new_val_images=3,new_val_targets=4,dataset_sha256=sha(out/'audit.json'),training_approved=False,deployment_accepted=False)
 (ROOT/'Reports/plant-validation-extension-v44-integrity.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
if __name__=='__main__':main()
