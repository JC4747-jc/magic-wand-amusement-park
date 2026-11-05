"""Release reviewed cut-arrangement TRAIN additions, preserving all inherited payloads."""
import hashlib,json,shutil
from pathlib import Path
from dataset_paths import manifest_relative_path as rel
from dataset_pipeline import decoded_pixel_digest
from source_author_identity import reservation_author_keys
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
 parent=ROOT/'Datasets/plant-hanging-v32';out=ROOT/'Datasets/plant-cut-v35'
 source=ROOT/'Datasets/plant-cut-train-source-v34';review=ROOT/'Reports/plant-cut-v34-boxes/reviewed-four.json'
 assert not out.exists()
 assert sha(parent/'audit.json')=='f80bcaf229194a957e8ff850af24cb213e79b2a9c002e557c5f197b6783aa40a'
 a=read(parent/'audit.json');m=read(source/'manifest.json');r=read(review)
 assert len(r['samples'])==4 and all(b['annotation_approved'] for b in r['samples'])
 selected={x['page_id'] for x in r['samples']}; assert selected=={57951583,113450346,93095143,117953988}
 rows=[x for x in m['samples'] if x['page_id'] in selected]
 groups={x['group'] for x in rows};assert len(groups)==4
 authors=set().union(*(reservation_author_keys(x) for x in rows)); assert authors
 evidence=[];hits=[]
 def walk(v,split=None):
  if isinstance(v,dict):
   split=v.get('split') or v.get('reserved_split') or v.get('project_reserved_split') or split
   if split in ('val','test') and (v.get('page_id') in selected or v.get('group') in groups or reservation_author_keys(v)&authors):hits.append(dict(split=split,group=v.get('group'),page_id=v.get('page_id')))
   for x in v.values():walk(x,split)
  elif isinstance(v,list):
   for x in v:walk(x,split)
 for name in ('manifest.json','audit.json','selection.json'):
  for p in (ROOT/'Datasets').glob('*/'+name):evidence.append(dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)));walk(read(p))
 assert not hits,hits
 inputs=[source/'manifest.json',source/'train-reservation.json',review]
 for item in r['proof_inputs']:
  p=ROOT/rel(item['path']);assert sha(p)==item['sha256'];inputs.append(p)
 px=read(ROOT/'Reports/plant-cut-v34-pixel-history.json')
 assert px['source_manifest_sha256']==sha(source/'manifest.json')
 assert px['scanned_paths']==px['enumerated_paths']==18336 and not any(px[k] for k in ('errors','matches','internal_matches'))
 creator=read(ROOT/'Reports/plant-cut-v34-creator-links.json')
 assert not creator['collisions']
 selected_indices={i for i,row in enumerate(m['samples']) if row['page_id'] in selected}
 assert not selected_indices.intersection(creator['missing_creator_link_indices'])
 reservation=read(source/'train-reservation.json');assert reservation['reserved_split']=='train' and reservation['source_manifest_sha256']==sha(source/'manifest.json')
 old=list(a['samples']);assert len(old)==138
 for row in old:
  for key,digest in [('image','sha256'),('label','label_sha256')]:
   p=parent/rel(row[key]);assert sha(p)==row[digest];dst=out/rel(row[key]);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dst);assert sha(dst)==row[digest]
 instances=0
 for b in r['samples']:
  assert b['original_reviewed'] and b['overlay_reviewed'] and b['annotation_approved']
  s=next(x for x in rows if x['page_id']==b['page_id'])
  image=ROOT/rel(b['image']);label=ROOT/rel(b['label'])
  for p,digest in [(image,b['image_sha256']),(label,b['label_sha256']),(ROOT/rel(b['overlay']),b['overlay_sha256'])]:assert sha(p)==digest
  assert sha(image)==s['sha256'] and s['license'] in {'CC BY 2.0','CC0','CC BY-SA 4.0'}
  assert s['group'] not in {x['group'] for x in old} and s['sha256'] not in {x['sha256'] for x in old}
  lines=label.read_text().splitlines();instances+=len(lines)
  for line in lines:
   parts=line.split();assert len(parts)==5 and parts[0]=='1' and all(0<=float(v)<=1 for v in parts[1:])
  ip=f"images/train/{s['sha256'][:24]}.jpg";lp=f"labels/train/{s['sha256'][:24]}.txt"
  shutil.copy2(image,out/ip);shutil.copy2(label,out/lp)
  a['samples'].append(dict(image=ip,label=lp,sha256=sha(image),label_sha256=sha(label),decoded_rgb_sha256=decoded_pixel_digest(image),split='train',source='commons',group=s['group'],original_id=str(s['page_id']),page_id=s['page_id'],negative=False,auxiliary_instances=len(lines),artist=s['artist'],publisher_metadata=s['publisher_metadata'],attribution=dict(Author=s['artist'],License=s['license_url'],OriginalLandingURL=s['description_url'],Title=s['title']),review=review.relative_to(ROOT).as_posix()))
 assert instances==4
 shutil.copy2(parent/'native-val.txt',out/'native-val.txt')
 a.setdefault('held_train_samples',[]).extend(dict(x,reserved_split='train',reason='Incomplete container census/domain review; no label payload adopted') for x in m['samples'] if x['page_id'] not in selected)
 a.update(unique_images=142,counts=dict(train=125,val=13,test=4),parent_audit_sha256=sha(parent/'audit.json'),scope='v32 plus four reviewed TRAIN cut-arrangement photos/four auxiliary instances; all138 inherited payloads and folds unchanged; four unused new sources held TRAIN only.',training_approved=True,deployment_accepted=False)
 a.setdefault('context_evidence',{}).update({p.relative_to(ROOT).as_posix():sha(p) for p in inputs+[Path(__file__).resolve()]})
 a['cut_addition_clearance']=dict(cross_val_test_hits=hits,historical_inputs=evidence,limitations='Recorded author IDs and current pixel evidence do not exclude unknown aliases, unrecorded crops/physical instances or pretraining exposure.')
 a['sources']=[*a['sources'],dict(id='commons',url='https://commons.wikimedia.org/',license='Mixed per-photo CC BY2.0, CC0 and CC BY-SA4.0. Retain artist, original publisher URL, license URL and change notices for each sample; no blanket license for mixed dataset.',reviewed=True)]
 assert sha(out/'native-val.txt')==sha(parent/'native-val.txt')
 (out/'audit.json').write_text(json.dumps(a,indent=2),encoding='utf-8')
 (out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='native-val.txt',test='images/test',names=a['names']),indent=2),encoding='utf-8')
 result=dict(status='PASS',photos=142,counts=a['counts'],inherited_payloads_verified=138,new_auxiliary_instances=4,held_new_sources=4,dataset_sha256=sha(out/'audit.json'),deployment_accepted=False)
 (ROOT/'Reports/plant-cut-v35-release-integrity.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
if __name__=='__main__':main()
