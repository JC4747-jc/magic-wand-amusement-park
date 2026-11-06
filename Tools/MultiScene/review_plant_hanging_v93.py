"""Record actual v93 overlay review and current recorded-source fold clearance."""
import hashlib,json
from pathlib import Path
from dataset_paths import manifest_relative_path as rel
from source_author_identity import reservation_author_keys
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
 source=ROOT/'Datasets/plant-hanging-train-source-v92';folder=ROOT/'Reports/plant-hanging-v93-boxes';dest=folder/'reviewed-two.json';assert not dest.exists()
 manifest=read(source/'manifest.json');draft=read(folder/'draft.json');assert draft['source_manifest_sha256']==sha(source/'manifest.json')
 reservation=read(source/'train-reservation.json');assert reservation['reserved_split']=='train' and reservation['manifest_sha256']==sha(source/'manifest.json')
 selected={90151588,97729076};rows=[r for r in manifest['samples'] if r['page_id'] in selected];groups={r['group'] for r in rows};authors=set().union(*(reservation_author_keys(r) for r in rows));assert authors
 proofs=[source/'manifest.json',source/'train-reservation.json',folder/'draft.json',folder/'revised-draft.json',Path(__file__).resolve()]
 for suffix in ['pixel-history','identity-history-v2','creator-links','original-review','preacquisition']:
  proofs.append(ROOT/f'Reports/plant-hanging-train-v92-{suffix}.json')
 pixel=read(proofs[-5]);assert pixel['source_manifest_sha256']==sha(source/'manifest.json') and pixel['scanned_paths']==pixel['enumerated_paths']==21383 and not any(pixel[k] for k in ['errors','matches','internal_matches'])
 identity=read(proofs[-4]);assert not identity['errors'] and all(not r['historical_hits'] for r in identity['rows'] if r['page_id'] in selected)
 creator=read(proofs[-3]);assert not creator['collisions'] and not creator['missing_creator_link_indices']
 history=[];hits=[];subject_hits=[]
 def walk(v,split=None):
  if isinstance(v,dict):
   split=v.get('split') or v.get('reserved_split') or v.get('project_reserved_split') or split
   if split in ('val','test'):
    if v.get('page_id') in selected or v.get('group') in groups or reservation_author_keys(v)&authors:hits.append(dict(page_id=v.get('page_id'),split=split))
    text=json.dumps({k:v.get(k) for k in ['title','description_url','publisher_metadata']}).casefold()
    if 'mitre' in text or 'edingburgh' in text:subject_hits.append(dict(page_id=v.get('page_id'),split=split))
   for child in v.values():walk(child,split)
  elif isinstance(v,list):
   for child in v:walk(child,split)
 for name in ['manifest.json','audit.json','selection.json']:
  for p in sorted((ROOT/'Datasets').glob('*/'+name)):history.append(dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)));walk(read(p))
 assert not hits and not subject_hits
 reviewed=[]
 for r in draft['samples']:
  if r['page_id']==90151588:r=read(folder/'revised-draft.json')
  for key,digest in [('image','image_sha256'),('label','label_sha256'),('overlay','overlay_sha256')]:assert sha(ROOT/rel(r[key]))==r[digest]
  assert r['license']=='CC BY-SA 4.0'
  r.update(overlay_reviewed=True,annotation_approved=True,observation='Four distinct hanging pots; second box expanded to include right/down trailing flowers. Original draft retained.' if r['page_id']==90151588 else 'One hanging petunia pot with attached flowers; foliage clipped at image left/right, support wires excluded.')
  reviewed.append(r)
 result=dict(samples=reviewed,source_manifest_sha256=sha(source/'manifest.json'),proofs={p.relative_to(ROOT).as_posix():sha(p) for p in proofs},historical_inputs=history,cross_val_test_hits=hits,recorded_mitre_subject_val_test_hits=subject_hits,review_scope='Single assistant actually viewed originals, both initial overlays and revised90151588 overlay. Not an independent second reviewer.',limitations='Recorded identity/title checks cannot exclude unknown aliases, unrecorded physical-subject matches or pretraining exposure. Other10 source photos remain held; no empty-negative labeling.',training_approved=False,deployment_accepted=False)
 dest.write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(dict(reviewed=2,instances=5,historical_documents=len(history),cross_val_test_hits=0,review_sha256=sha(dest))))
if __name__=='__main__':main()
