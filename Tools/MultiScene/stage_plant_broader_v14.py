"""Stage one visually reviewed v14 source; never release training automatically."""
import json,hashlib,shutil
from pathlib import Path
from stage_plant_flowering_v12 import walk
from audit_plant_cutflower_v11_candidates import norm
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 review=ROOT/'Reports/plant-broader-train-v14-review/reviewed-11db.json';v=json.loads(review.read_text());idx=json.loads((review.parent/'index.json').read_text());r=next(x for x in idx['samples'] if x['original_id']==v['original_id']);assert v['corrected_overlay_actually_viewed'] and v['visible_potted_instances']==2
 ip=ROOT/r['image'];lp=ROOT/v['label'];assert sha(ip)==r['sha256'] and sha(lp)==v['label_sha256']
 paths=set((ROOT/'Datasets').glob('*/manifest.json'))|set((ROOT/'Datasets').glob('*/audit.json'))|{p for p in (ROOT/'Reports').glob('*.json') if any(t in p.name for t in ('reservation','preacquisition','preannotation','evaluation-plan'))}
 inputs=[];hits=[];errors=[]
 for p in sorted(paths):
  try:d=json.loads(p.read_text(encoding='utf-8-sig'))
  except Exception as e:errors.append(dict(path=str(p.relative_to(ROOT)),error=str(e)));continue
  inputs.append(dict(path=str(p.relative_to(ROOT)),sha256=sha(p)))
  for location,row in walk(d):
   values={norm(x) for x in row.values() if isinstance(x,str)};matched=[k for k,value in [('id',r['original_id']),('author',norm(r['group'])),('sha256',r['sha256'])] if value in values]
   if matched:hits.append(dict(path=str(p.relative_to(ROOT)),location=location,matched=matched,split=row.get('split'),reserved_split=row.get('reserved_split')))
 report=ROOT/'Reports/plant-broader-v14-source-reservation-audit.json';assert not report.exists();report.write_text(json.dumps(dict(inputs=inputs,records=hits,errors=errors,training_approved=False),indent=2)+'\n')
 out=ROOT/'Datasets/plant-broader-v14-staging';assert not out.exists();(out/'images').mkdir(parents=True);(out/'labels').mkdir();shutil.copy2(ip,out/'images'/ip.name);shutil.copy2(lp,out/'labels'/lp.name)
 source=ROOT/'Datasets/openimages-plant-fresh-general-source-v4/manifest.json';m=json.loads(source.read_text());row=dict(original_id=r['original_id'],image='images/'+ip.name,label='labels/'+lp.name,sha256=sha(ip),label_sha256=sha(lp),group=r['group'],attribution=r['attribution'],source='openimages-v7',reserved_split='train',review_approved=True)
 (out/'manifest.json').write_text(json.dumps(dict(samples=[row],names=['PottedPlant','CutArrangement'],sources=m['sources'],source_manifest_sha256=sha(source),review_sha256=sha(review),training_approved=False,scope='Staging only; full clearance and broader batch still pending'),indent=2)+'\n');print(json.dumps(dict(metadata_files=len(inputs),hits=hits,errors=len(errors),staged=1)))
if __name__=='__main__':main()
