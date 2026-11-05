"""Stage reviewed positive labels with complete source/audit reservation enumeration."""
import hashlib,json,shutil
from pathlib import Path
from audit_plant_cutflower_v11_candidates import norm
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def walk(v,location='$'):
    if isinstance(v,dict):
        yield location,v
        for k,x in v.items():yield from walk(x,location+'.'+k)
    elif isinstance(v,list):
        for i,x in enumerate(v):yield from walk(x,f'{location}[{i}]')
def main():
    review=ROOT/'Reports/plant-flowering-v12-label-review/reviewed.json';r=json.loads(review.read_text(encoding='utf-8-sig'));selected=r['samples'];assert len(selected)==2
    paths=set((ROOT/'Datasets').glob('*/manifest.json'))|set((ROOT/'Datasets').glob('*/audit.json'))|{p for p in (ROOT/'Reports').glob('*.json') if any(s in p.name for s in ['reservation','preacquisition','preannotation','evaluation-plan'])}
    records=[];inputs=[];errors=[]
    for p in sorted(paths):
        try:d=json.loads(p.read_text(encoding='utf-8-sig'))
        except Exception as e:errors.append(dict(path=str(p.relative_to(ROOT)),error=str(e)));continue
        inputs.append(dict(path=str(p.relative_to(ROOT)),sha256=sha(p)))
        for location,row in walk(d):
            values={norm(x) for x in row.values() if isinstance(x,str)}
            for s in selected:
                matched=[k for k,v in [('id',s['original_id']),('author',norm(s['group'])),('sha256',s['source_sha256'])] if v in values]
                if matched:records.append(dict(candidate=s['original_id'],path=str(p.relative_to(ROOT)),location=location,matched=matched,split=row.get('split'),reservation=row.get('reserved_split')))
    report=ROOT/'Reports/plant-flowering-v12-source-reservation-audit.json';assert not report.exists();report.write_text(json.dumps(dict(inputs=inputs,records=records,errors=errors,training_approved=False),indent=2))
    out=ROOT/'Datasets/plant-flowering-v12-staging';assert not out.exists();(out/'images').mkdir(parents=True);(out/'labels').mkdir();rows=[]
    for s in selected:
        assert s['original_reviewed'] and s['overlay_reviewed'];ip=ROOT/s['source_image'];lp=ROOT/s['label'];assert sha(ip)==s['source_sha256'] and sha(lp)==s['label_sha256']
        dst=out/'images'/ip.name;dl=out/'labels'/f'{ip.stem}.txt';shutil.copy2(ip,dst);shutil.copy2(lp,dl)
        rows.append(dict(original_id=s['original_id'],image=f'images/{dst.name}',label=f'labels/{dl.name}',sha256=sha(dst),label_sha256=sha(dl),group=s['group'],attribution=s['attribution'],source='openimages-v7',reserved_split='train',negative=False,review_approved=True))
    parent=ROOT/'Datasets/openimages-plant-expansion/manifest.json';m=json.loads(parent.read_text());(out/'manifest.json').write_text(json.dumps(dict(samples=rows,names=['PottedPlant'],sources=m['sources'],parent_manifest_sha256=sha(parent),review_sha256=sha(review),training_approved=False,scope='TRAIN-reserved staging; metadata/pixel clearance required'),indent=2));print(json.dumps(dict(metadata_files=len(inputs),collisions=len(records),errors=len(errors),staged=len(rows))))
if __name__=='__main__':main()
