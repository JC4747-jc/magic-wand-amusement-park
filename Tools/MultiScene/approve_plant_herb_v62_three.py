"""Record post-overlay review; retain rejected single-box draft intact."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))


def main():
 folder=ROOT/'Reports/plant-herb-train-v62-boxes';draft=read(folder/'draft.json')
 source=ROOT/'Datasets/plant-herb-train-source-v62';manifest=read(source/'manifest.json')
 proofs=[ROOT/'Reports'/('plant-herb-train-v62-'+n+'.json') for n in ['original-review','identity-history-v2','creator-links','pixel-history','preacquisition']]
 pixel=read(proofs[3]);assert pixel['scanned_paths']==pixel['enumerated_paths']==19791
 assert not any(pixel[k] for k in ['errors','matches','internal_matches'])
 approved=[]
 for row in draft['samples']:
  for key,digest in [('image','image_sha256'),('label','label_sha256'),('overlay','overlay_sha256')]:assert sha(ROOT/row[key])==row[digest]
  if row['page_id']==36674871:continue
  assert row['page_id'] in {66493232,90466407,148029028}
  meta=next(r for r in manifest['samples'] if r['page_id']==row['page_id'])
  assert meta['license'] in ['CC BY-SA 4.0','CC BY 2.0'] and meta['license_url']
  row.update(overlay_reviewed=True,annotation_approved=True,artist=meta['artist'],group=meta['group'],
    license=meta['license'],license_url=meta['license_url'],publisher_metadata=meta['publisher_metadata'])
  approved.append(row)
 assert len(approved)==3
 out=folder/'reviewed-three.json';assert not out.exists()
 out.write_text(json.dumps(dict(samples=approved,source_manifest_sha256=sha(source/'manifest.json'),
  draft_sha256=sha(folder/'draft.json'),proof_sha256={p.relative_to(ROOT).as_posix():sha(p) for p in proofs},
  held_draft=dict(page_id=36674871,overlay_actually_viewed=True,annotation_approved=False,
   reason='Visible rear black nursery-container rim and separate foreground pot may indicate two targets; single-box draft is not a complete instance census and is not adopted.'),
  review_notes='Whole rooted plant plus visible container; do not expand to cast shadow or detached leaf. Boxes at image edges describe visible truncated extent, not inferred full shape. Three candidates only; cross-fold release clearance still required.',
  training_approved=False,deployment_accepted=False),indent=2),encoding='utf-8')
 print('Three reviewed annotations saved; possible two-pot draft held; no dataset or training release')


if __name__=='__main__':main()
