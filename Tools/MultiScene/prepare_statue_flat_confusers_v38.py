"""Append only seven reviewed TRAIN confusers, preserve every parent payload/fold."""
import json,hashlib,shutil
from pathlib import Path
from collections import Counter
from dataset_paths import manifest_relative_path as rel
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
 parent=ROOT/'Datasets/statue-articulated-release-v36';source=ROOT/'Datasets/statue-flat-train-source-v37';out=ROOT/'Datasets/statue-flat-confusers-v38'
 assert not out.exists() and sha(parent/'audit.json')=='7d9617bba3b5c36f36673e4df6cd56533b5228a3c48b7fb1dfbb89ef5910a6d3'
 review=ROOT/'Reports/statue-flat-train-v37-reviewed.json';d=read(review);m=read(source/'manifest.json');a=read(parent/'audit.json')
 assert d['source_manifest_sha256']==sha(source/'manifest.json') and d['training_approved']
 for p,h in d['context_evidence'].items():assert sha(ROOT/p)==h
 ids=read(ROOT/'Reports/statue-flat-train-v37-identity-history-v2.json');cr=read(ROOT/'Reports/statue-flat-train-v37-creator-links.json');px=read(ROOT/'Reports/statue-flat-train-v37-pixel-history.json')
 assert not ids['errors'] and all(not r['historical_hits'] for r in ids['rows']) and ids['source_manifest_sha256']==sha(source/'manifest.json')
 assert not cr['collisions'] and not cr['missing_creator_link_indices'] and cr['source_sha256']==sha(source/'manifest.json')
 assert px['scanned_paths']==px['enumerated_paths']==17824 and not px['errors'] and not px['matches'] and not px['internal_matches'] and px['source_manifest_sha256']==sha(source/'manifest.json')
 assert read(source/'train-reservation.json')['reserved_split']=='train'
 old=list(a['samples'])
 for row in old:
  for k,digest in [('image','sha256'),('label','label_sha256')]:
   p=parent/rel(row[k]);assert sha(p)==row[digest];dst=out/rel(row[k]);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dst);assert sha(dst)==row[digest]
 sources={r['page_id']:r for r in m['samples']};selected=[r for r in d['samples'] if r['training_approved']];assert len(selected)==7
 for b in selected:
  r=sources[b['page_id']];assert b['original_actually_viewed'] and b['boxes']==[] and sha(source/rel(r['image']))==r['sha256']==b['sha256']
  assert all(r['group']!=x['group'] and r['sha256']!=x['sha256'] for x in a['samples'])
  assert r['license'] in ['CC BY 2.0','CC BY 3.0','CC BY 4.0','CC BY-SA 4.0','CC0'] and r['license_url']
  image=out/'images/train'/Path(r['image']).name;label=out/'labels/train'/(image.stem+'.txt');shutil.copy2(source/rel(r['image']),image);label.write_text('',encoding='utf-8')
  a['samples'].append(dict(r,image=image.relative_to(out).as_posix(),label=label.relative_to(out).as_posix(),label_sha256=sha(label),split='train',training_approved=True,annotation_status='REVIEWED_NO_STATUE',review=review.relative_to(ROOT).as_posix()))
 a.update(parent_audit_sha256=sha(parent/'audit.json'),unique_images=len(a['samples']),split_counts=dict(Counter(x['split'] for x in a['samples'])),training_approved=True,deployment_accepted=False,revision='Seven public TRAIN negative photos; all103parent payloads/groups/folds unchanged. Storefront128465397 held outside adopted data.')
 a['context_evidence'].update(d['context_evidence']);a['context_evidence'].update({p.relative_to(ROOT).as_posix():sha(p) for p in [review,source/'manifest.json',source/'train-reservation.json',Path(__file__).resolve()]})
 a['sources'].append(dict(id='commons-flat-confusers-v37',url='https://commons.wikimedia.org/wiki/Category:Cardboard_cutouts',license='Per-image CC licenses and photographer attribution retained',reviewed=True,review_scope='Seven explicitly reviewed complete-image negatives only'))
 (out/'audit.json').write_text(json.dumps(a,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 (out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='images/val',test='images/test',names=['Statue']),indent=2)+'\n',encoding='utf-8')
 (out/'annotation_release.json').write_text(json.dumps(dict(status='SEVEN_NEGATIVES_RELEASED_ONE_SOURCE_HELD',dataset_audit_sha256=sha(out/'audit.json'),held_source_page_ids=[128465397],training_approved=True,deployment_accepted=False),indent=2),encoding='utf-8')
 print(json.dumps(dict(status='PASS',photos=len(a['samples']),splits=a['split_counts'],audit_sha256=sha(out/'audit.json'))))
if __name__=='__main__':main()
