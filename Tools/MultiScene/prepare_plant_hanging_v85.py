"""Add three reviewed hanging TRAIN plants while preserving binary v79 exactly."""
import hashlib,json,shutil
from pathlib import Path
from dataset_paths import manifest_relative_path as rel
from dataset_pipeline import decoded_pixel_digest
from source_author_identity import reservation_author_keys
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))


def main():
 parent=ROOT/'Datasets/plant-binary-v79';out=ROOT/'Datasets/plant-hanging-v85'
 source=ROOT/'Datasets/plant-hanging-train-source-v83'
 reviewpath=ROOT/'Reports/plant-hanging-v84-boxes/reviewed-three.json'
 assert not out.exists(), 'Preserve immutable or partial output'
 assert sha(parent/'audit.json')=='76561101c92b6cf08f00ccda8b47023325992b04b87153605a211a3025135cdf'
 audit=read(parent/'audit.json');manifest=read(source/'manifest.json');review=read(reviewpath)
 selected={r['page_id'] for r in review['samples']};assert selected=={48286181,169907542,169798691}
 rows=[r for r in manifest['samples'] if r['page_id'] in selected]
 assert len(rows)==3 and review['source_manifest_sha256']==sha(source/'manifest.json')
 reservation=read(source/'train-reservation.json');assert reservation['reserved_split']=='train' and reservation['manifest_sha256']==sha(source/'manifest.json')
 for name,digest in review['proofs'].items():assert sha(ROOT/rel(name))==digest
 pixel=read(ROOT/'Reports/plant-hanging-train-v83-pixel-history.json')
 assert pixel['scanned_paths']==pixel['enumerated_paths']==20963 and pixel['source_manifest_sha256']==sha(source/'manifest.json')
 assert not any(pixel[k] for k in ['errors','matches','internal_matches'])
 identity=read(ROOT/'Reports/plant-hanging-train-v83-identity-history-v2.json');assert not identity['errors']
 assert {r['page_id'] for r in identity['rows'] if r['page_id'] in selected and not r['historical_hits']}==selected
 creator=read(ROOT/'Reports/plant-hanging-train-v83-creator-links.json')
 selected_indices={i for i,r in enumerate(manifest['samples']) if r['page_id'] in selected}
 assert not selected_indices.intersection(creator['missing_creator_link_indices'])
 assert not any(r['index'] in selected_indices for r in creator['collisions'])

 groups={r['group'] for r in rows};authors=set().union(*(reservation_author_keys(r) for r in rows))
 assert authors and len(groups)==3
 history=[];hits=[]
 def walk(v,split=None):
  if isinstance(v,dict):
   split=v.get('split') or v.get('reserved_split') or v.get('project_reserved_split') or split
   if split in ('val','test') and (v.get('page_id') in selected or v.get('group') in groups or reservation_author_keys(v)&authors):hits.append(dict(page_id=v.get('page_id'),group=v.get('group'),split=split))
   for child in v.values():walk(child,split)
  elif isinstance(v,list):
   for child in v:walk(child,split)
 for name in ['manifest.json','audit.json','selection.json']:
  for p in sorted((ROOT/'Datasets').glob('*/'+name)):
   history.append(dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)));walk(read(p))
 assert not hits,hits
 old=list(audit['samples']);assert len(old)==194
 for row in old:
  for key,digest in [('image','sha256'),('label','label_sha256')]:
   p=parent/rel(row[key]);assert sha(p)==row[digest]
   target=out/rel(row[key]);target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
 for box in review['samples']:
  assert box['original_reviewed'] and box['overlay_reviewed'] and box['annotation_approved']
  r=next(r for r in rows if r['page_id']==box['page_id'])
  for key,digest in [('image','image_sha256'),('label','label_sha256'),('overlay','overlay_sha256')]:assert sha(ROOT/rel(box[key]))==box[digest]
  assert r['sha256']==box['image_sha256'] and r['license'] in ['CC BY 2.0','CC BY-SA 4.0','CC0']
  assert r['sha256'] not in {r['sha256'] for r in old} and r['group'] not in {r['group'] for r in old}
  label=ROOT/rel(box['label']);lines=label.read_text().splitlines();assert len(lines)==1
  for line in lines:
   parts=line.split();assert parts[0]=='0' and len(parts)==5
   cx,cy,w,h=map(float,parts[1:]);assert 0<w<=1 and 0<h<=1 and cx-w/2>=-1e-8 and cy-h/2>=-1e-8 and cx+w/2<=1+1e-8 and cy+h/2<=1+1e-8
  ip=f"images/train/{r['sha256'][:24]}.jpg";lp=f"labels/train/{r['sha256'][:24]}.txt"
  shutil.copy2(ROOT/rel(box['image']),out/ip);shutil.copy2(label,out/lp)
  audit['samples'].append(dict(image=ip,label=lp,sha256=r['sha256'],label_sha256=sha(label),decoded_rgb_sha256=decoded_pixel_digest(out/ip),split='train',source='commons',group=r['group'],page_id=r['page_id'],original_id=str(r['page_id']),negative=False,auxiliary_instances=0,artist=r['artist'],publisher_metadata=r['publisher_metadata'],attribution=dict(Author=r['artist'],License=r['license_url'],OriginalLandingURL=r['description_url'],Title=r['title']),review=reviewpath.relative_to(ROOT).as_posix()))
 assert audit['samples'][:194]==old
 from ultralytics.data.utils import img2label_paths
 for r in audit['samples']:
  assert sha(out/rel(r['image']))==r['sha256'] and sha(out/rel(r['label']))==r['label_sha256']
  actual=Path(img2label_paths([str(out/rel(r['image']))])[0]);assert actual.resolve()==(out/rel(r['label'])).resolve()
 audit['held_train_samples']=[r for r in audit.get('held_train_samples',[]) if r.get('page_id') not in selected]
 inputs=[source/'manifest.json',source/'train-reservation.json',reviewpath,Path(__file__).resolve()]+[ROOT/rel(n) for n in review['proofs']]
 for held in read(ROOT/'Reports/plant-hanging-train-v83-original-review.json')['samples']:
  if held['page_id'] not in selected:
   evidence=next(r for r in manifest['samples'] if r['page_id']==held['page_id'])
   audit.setdefault('held_train_samples',[]).append(dict(page_id=evidence['page_id'],artist=evidence['artist'],group=evidence['group'],reserved_split='train',reason=held['observation'],source_manifest='Datasets/plant-hanging-train-source-v83/manifest.json',annotation_approved=False))
 audit.setdefault('context_evidence',{}).update({p.relative_to(ROOT).as_posix():sha(p) for p in inputs})
 audit['hanging_addition_clearance_v85']=dict(cross_val_test_hits=hits,historical_inputs=history,limitations='Recorded creator and pixel checks cannot exclude unknown aliases, physical subjects or pretraining exposure.')
 audit.update(unique_images=197,counts=dict(train=163,val=30,test=4),parent_audit_sha256=sha(parent/'audit.json'),scope='Binary v79 plus three reviewed hanging TRAIN photos/three class0 instances; all194 parent rows/payloads/folds unchanged. Underside and dense multi-basket coverage remains incomplete.',training_approved=False,deployment_accepted=False)
 (out/'audit.json').write_text(json.dumps(audit,indent=2),encoding='utf-8')
 config=read(parent/'data.yaml');config['path']=str(out);(out/'data.yaml').write_text(json.dumps(config,indent=2),encoding='utf-8')
 hold=read(parent/'review_hold.json');hold['dataset_audit_sha256']=sha(out/'audit.json');(out/'review_hold.json').write_text(json.dumps(hold,indent=2),encoding='utf-8')
 shutil.copy2(parent/'legacy-native-val.txt',out/'legacy-native-val.txt')
 result=dict(status='PREPARED_WITH_OPEN_ANNOTATION_HOLD',images=197,counts=audit['counts'],new_class0_instances=3,inherited_rows_exact=194,actual_yolo_label_paths_checked=197,dataset_sha256=sha(out/'audit.json'),training_approved=False,deployment_accepted=False)
 (ROOT/'Reports/plant-hanging-v85-integrity.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))


if __name__=='__main__':main()
