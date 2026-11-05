"""Stage four reviewed negatives and enumerate historical metadata reservations."""
import hashlib,json,shutil
from pathlib import Path
from audit_plant_cutflower_v11_candidates import norm
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    review=json.loads((ROOT/'Reports/plant-cutflower-v11-original-review.json').read_text(encoding='utf-8-sig'))
    ids={r['original_id'] for r in review['samples'] if r['decision'].startswith('VISUAL_NEGATIVE')}
    old=ROOT/'Datasets/openimages-plant-fresh-general-source-v4/manifest.json';manifest=json.loads(old.read_text())
    selected=[r for r in manifest['samples'] if r['original_id'] in ids];assert len(selected)==4
    records=[];errors=[];inputs=[]
    paths=set((ROOT/'Datasets').glob('*/manifest.json'))|set((ROOT/'Datasets').glob('*/audit.json'))
    paths|={p for p in (ROOT/'Reports').glob('*.json') if any(s in p.name for s in ['reservation','preacquisition','preannotation','evaluation-plan'])}
    def walk(obj,where='$'):
        if isinstance(obj,dict):
            yield where,obj
            for k,v in obj.items():yield from walk(v,where+'.'+k)
        elif isinstance(obj,list):
            for i,v in enumerate(obj):yield from walk(v,f'{where}[{i}]')
    for p in sorted(paths):
        try:doc=json.loads(p.read_text(encoding='utf-8-sig'))
        except Exception as e:errors.append(dict(path=str(p.relative_to(ROOT)),error=str(e)));continue
        inputs.append(dict(path=str(p.relative_to(ROOT)),sha256=sha(p)))
        for location,row in walk(doc):
            values={norm(v) for v in row.values() if isinstance(v,str)}
            for r in selected:
                matched=[]
                if r['original_id'] in values:matched.append('id')
                if norm(r['group']) in values:matched.append('author')
                if r.get('image_sha256') in values:matched.append('sha256')
                if matched:records.append(dict(candidate=r['original_id'],path=str(p.relative_to(ROOT)),location=location,matched=matched,split=row.get('split'),reservation=row.get('reserved_split'),scope=doc.get('scope'),upstream_split=doc.get('upstream_split')))
    target=ROOT/'Reports/plant-cutflower-v11-source-reservation-audit.json';assert not target.exists()
    target.write_text(json.dumps(dict(inputs=inputs,records=records,errors=errors,training_approved=False,scope='All top-level dataset manifests/audits and Reports reservation/preacquisition/preannotation/evaluation-plan records; upstream_split is publisher split, not project assignment'),indent=2))
    out=ROOT/'Datasets/plant-cutflower-v11-staging';assert not out.exists();(out/'images').mkdir(parents=True);(out/'labels').mkdir()
    rows=[]
    for r in selected:
        ip=old.parent/r['image'];dst=out/'images'/ip.name;shutil.copy2(ip,dst);lp=out/'labels'/f'{ip.stem}.txt';lp.write_text('')
        rows.append(dict(original_id=r['original_id'],image=f'images/{dst.name}',label=f'labels/{lp.name}',sha256=sha(dst),label_sha256=sha(lp),group=r['group'],attribution=r['attribution'],source=r['source'],reserved_split='train',negative=True,review_approved=True))
    (out/'manifest.json').write_text(json.dumps(dict(names=['PottedPlant'],samples=rows,sources=manifest['sources'],parent_manifest_sha256=sha(old),original_review_sha256=sha(ROOT/'Reports/plant-cutflower-v11-original-review.json'),training_approved=False,scope='TRAIN-reserved staging; reservation/pixel clearance required before release'),indent=2))
    print(json.dumps(dict(metadata_files=len(inputs),collisions=len(records),errors=len(errors),staged=len(rows))))
if __name__=='__main__':main()
