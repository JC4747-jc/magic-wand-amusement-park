"""Release reviewed hanging-container TRAIN additions, preserving all inherited payloads."""
import hashlib,json,shutil
from pathlib import Path
from dataset_paths import manifest_relative_path as rel
from dataset_pipeline import decoded_pixel_digest
from source_author_identity import reservation_author_keys
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
 parent=ROOT/'Datasets/plant-validation-extension-v46';out=ROOT/'Datasets/plant-hanging-v50'
 source=ROOT/'Datasets/plant-hanging-train-source-v49';review=ROOT/'Reports/plant-hanging-train-v49-boxes/reviewed-two.json'
 assert not (out/'audit.json').exists(), 'Preserve completed dataset'
 assert sha(parent/'audit.json')=='5c18bc75c706af67d9bdcb20d4197f31fc41791fcb5e58220bc8f1ac615db66a'
 a=read(parent/'audit.json');m=read(source/'manifest.json');r=read(review)
 assert len(r['samples'])==2
 selected={x['page_id'] for x in r['samples']}; assert selected=={134319541,63438809}
 rows=[x for x in m['samples'] if x['page_id'] in selected]
 groups={x['group'] for x in rows};assert len(groups)==2
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
 for name,digest in r['proof_sha256'].items():
  p=ROOT/rel(name);assert sha(p)==digest;inputs.append(p)
 px=read(ROOT/'Reports/plant-hanging-train-v49-pixel-history.json')
 assert px['source_manifest_sha256']==sha(source/'manifest.json')
 assert px['scanned_paths']==px['enumerated_paths']==19279 and not any(px[k] for k in ('errors','matches','internal_matches'))
 old=list(a['samples']);assert len(old)==159
 expected={}
 for row in old:
  for key,digest in [('image','sha256'),('label','label_sha256')]:expected[rel(row[key]).as_posix()]=row[digest]
 for b in r['samples']:
  expected[f"images/train/{b['image_sha256'][:24]}.jpg"]=b['image_sha256']
  expected[f"labels/train/{b['image_sha256'][:24]}.txt"]=b['label_sha256']
 if out.exists():
  for p in out.rglob('*'):
   if p.is_file():assert expected.get(p.relative_to(out).as_posix())==sha(p), 'Unexpected partial output: '+str(p)
 for row in old:
  for key,digest in [('image','sha256'),('label','label_sha256')]:
   p=parent/rel(row[key]);assert sha(p)==row[digest];dst=out/rel(row[key]);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dst);assert sha(dst)==row[digest]
 instances=0
 for b in r['samples']:
  assert b['original_reviewed'] and b['overlay_reviewed'] and b['annotation_approved']
  s=next(x for x in rows if x['page_id']==b['page_id'])
  image=ROOT/rel(b['image']);label=ROOT/rel(b['label'])
  for p,digest in [(image,b['image_sha256']),(label,b['label_sha256']),(ROOT/rel(b['overlay']),b['overlay_sha256'])]:assert sha(p)==digest
  assert sha(image)==s['sha256'] and s['license'] in ('CC BY 4.0','CC BY-SA 4.0')
  assert s['group'] not in {x['group'] for x in old} and s['sha256'] not in {x['sha256'] for x in old}
  lines=label.read_text().splitlines();instances+=len(lines)
  for line in lines:
   parts=line.split();assert len(parts)==5 and parts[0]=='0' and all(0<=float(v)<=1 for v in parts[1:])
  ip=f"images/train/{s['sha256'][:24]}.jpg";lp=f"labels/train/{s['sha256'][:24]}.txt"
  shutil.copy2(image,out/ip);shutil.copy2(label,out/lp)
  a['samples'].append(dict(image=ip,label=lp,sha256=sha(image),label_sha256=sha(label),decoded_rgb_sha256=decoded_pixel_digest(image),split='train',source='commons',group=s['group'],original_id=str(s['page_id']),page_id=s['page_id'],negative=False,auxiliary_instances=0,artist=s['artist'],publisher_metadata=s['publisher_metadata'],attribution=dict(Author=s['artist'],License=s['license_url'],OriginalLandingURL=s['description_url'],Title=s['title']),review=review.relative_to(ROOT).as_posix()))
 assert instances==3
 shutil.copy2(parent/'legacy-native-val.txt',out/'legacy-native-val.txt')
 a.setdefault('held_train_samples',[]).extend(dict(x,reserved_split='train',reason='Incomplete container census/domain review; no label payload adopted') for x in m['samples'] if x['page_id'] not in selected)
 a.update(unique_images=161,counts=dict(train=127,val=30,test=4),parent_audit_sha256=sha(parent/'audit.json'),scope='v46 plus two reviewed TRAIN photos/three plant instances; all159 inherited rows and payloads unchanged. Prospective validation remains a diagnostic pilot.',training_approved=False,deployment_accepted=False)
 a.setdefault('context_evidence',{}).update({p.relative_to(ROOT).as_posix():sha(p) for p in inputs+[Path(__file__).resolve()]})
 a['hanging_addition_clearance']=dict(cross_val_test_hits=hits,historical_inputs=evidence,limitations='Recorded author IDs and current pixel evidence do not exclude unknown aliases, unrecorded crops/physical instances or pretraining exposure.')
 (out/'audit.json').write_text(json.dumps(a,indent=2),encoding='utf-8')
 (out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='images/val',test='images/test',names=a['names']),indent=2),encoding='utf-8')
 result=dict(status='PREPARED_WITH_OPEN_HOLD',photos=161,counts=a['counts'],inherited_payloads_verified=159,new_target_instances=3,held_new_sources=6,dataset_sha256=sha(out/'audit.json'),deployment_accepted=False)
 hold=read(parent/'review_hold.json');hold.update(dataset_audit_sha256=sha(out/'audit.json'));(out/'review_hold.json').write_text(json.dumps(hold,indent=2),encoding='utf-8')
 (ROOT/'Reports/plant-hanging-v50-release-integrity.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
if __name__=='__main__':main()
