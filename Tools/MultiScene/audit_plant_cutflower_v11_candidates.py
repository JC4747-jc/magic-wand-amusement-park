"""Audit cut-flower candidates against historical project folds before review."""
import json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def norm(s):return str(s).lower().replace('openimages-v7:','').replace('http:','https:').replace('www.','').rstrip('/')
def main():
    source=ROOT/'Datasets/openimages-plant-fresh-general-source-v4/manifest.json'
    m=json.loads(source.read_text());review=json.loads((ROOT/'Reports/plant-fresh-general-v4-contact-review-final.json').read_text())
    ids={r['original_id'] for r in review['samples'] if r['decision']=='EXCLUDE_CUT_FLOWER'}
    audits=[];errors=[]
    for p in sorted((ROOT/'Datasets').glob('*/audit.json')):
        try:a=json.loads(p.read_text(encoding='utf-8-sig'))
        except Exception as e:errors.append(dict(path=str(p.relative_to(ROOT)),error=str(e)));continue
        for r in a.get('samples',[]):
            if r.get('split') in ('train','val','test'):audits.append((p,r))
    result=[]
    for r in m['samples']:
        if r['original_id'] not in ids:continue
        image=source.parent/r['image'];digest=hashlib.sha256(image.read_bytes()).hexdigest()
        collisions=[]
        for p,h in audits:
            reasons=[]
            if h.get('original_id')==r['original_id']:reasons.append('id')
            if h.get('sha256')==digest or h.get('image_sha256')==digest:reasons.append('sha256')
            if h.get('group') and norm(h['group'])==norm(r['group']):reasons.append('author')
            if reasons:collisions.append(dict(audit=str(p.relative_to(ROOT)),split=h['split'],reasons=reasons))
        blocked=any(c['split']!='train' for c in collisions)
        result.append(dict(original_id=r['original_id'],image=str(image.relative_to(ROOT)),image_sha256=digest,group=r['group'],attribution=r.get('attribution'),historical=collisions,status='HOLD_NONTRAIN_RESERVATION' if blocked else 'ORIGINAL_REVIEW_REQUIRED'))
    out=dict(source_manifest_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),scope='Historical released audit ID/hash/author reservation check only; no decoded-pixel or complete manifest reservation clearance yet',audit_errors=errors,samples=result,training_approved=False)
    target=ROOT/'Reports/plant-cutflower-v11-candidate-fold-audit.json';assert not target.exists();target.write_text(json.dumps(out,indent=2));print(json.dumps(dict(candidates=len(result),eligible_for_review=sum(r['status']=='ORIGINAL_REVIEW_REQUIRED' for r in result),errors=len(errors))))
if __name__=='__main__':main()
