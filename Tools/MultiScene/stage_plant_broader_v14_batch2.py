"""Stage reviewed batch2 without changing earlier manifests or data folds."""
import argparse,json,shutil
from pathlib import Path
from stage_plant_broader_v14 import sha,walk,norm,ROOT


def validate_review_row(r, root=ROOT):
 assert r['original_reviewed'] and r['visual_annotation_approved']
 ip=root/r['source_image'];lp=root/r['label']
 assert sha(ip)==r['source_sha256'] and sha(lp)==r['label_sha256']
 if r.get('negative') is True:
  assert r.get('original_negative_reviewed') is True and not lp.read_text().strip(), 'Negative requires original review and empty labels'
 else:
  assert lp.read_text().strip() and r['overlay_reviewed'], 'Positive requires reviewed label overlay'
  assert sha(root/r['overlay'])==r['overlay_sha256']
 return ip,lp

def main():
 parser=argparse.ArgumentParser(description=__doc__)
 parser.add_argument('--review',default='Reports/plant-broader-train-v14-review-batch2/reviewed.json')
 parser.add_argument('--out',default='Datasets/plant-broader-v14-batch2-staging')
 parser.add_argument('--source-manifest',default='Datasets/openimages-plant-fresh-general-source-v4/manifest.json')
 parser.add_argument('--audit-out',default='Reports/plant-broader-v14-batch2-source-reservation-audit.json')
 args=parser.parse_args();review=ROOT/args.review;out=ROOT/args.out;report=ROOT/args.audit_out
 assert not out.exists() and not report.exists(),'Preserve prior staging and audit outputs'
 v=json.loads(review.read_text());selected=v['samples'];assert selected
 for r in selected:
  validate_review_row(r)
 paths=set((ROOT/'Datasets').glob('*/manifest.json'))|set((ROOT/'Datasets').glob('*/audit.json'))|{p for p in (ROOT/'Reports').glob('*.json') if any(t in p.name for t in ('reservation','preacquisition','preannotation','evaluation-plan'))}
 inputs=[];hits=[];errors=[]
 for p in sorted(paths):
  try:d=json.loads(p.read_text(encoding='utf-8-sig'))
  except Exception as e:errors.append(dict(path=str(p.relative_to(ROOT)),error=str(e)));continue
  inputs.append(dict(path=str(p.relative_to(ROOT)),sha256=sha(p)))
  for location,row in walk(d):
   values={norm(x) for x in row.values() if isinstance(x,str)}
   for r in selected:
    matched=[k for k,value in [('id',r['original_id']),('author',norm(r['group'])),('sha256',r['source_sha256'])] if value in values]
    if matched:hits.append(dict(candidate=r['original_id'],path=str(p.relative_to(ROOT)),location=location,matched=matched,split=row.get('split'),reserved_split=row.get('reserved_split')))
 report.write_text(json.dumps(dict(inputs=inputs,records=hits,errors=errors,training_approved=False),indent=2)+'\n')
 (out/'images').mkdir(parents=True);(out/'labels').mkdir();rows=[]
 for r in selected:
  ip,lp=validate_review_row(r)
  shutil.copy2(ip,out/'images'/ip.name);shutil.copy2(lp,out/'labels'/lp.name);rows.append(dict(original_id=r['original_id'],image='images/'+ip.name,label='labels/'+lp.name,sha256=sha(ip),label_sha256=sha(lp),group=r['group'],attribution=r['attribution'],source='openimages-v7',reserved_split='train',review_approved=True,negative=not lp.read_text().strip()))
 source=ROOT/args.source_manifest;m=json.loads(source.read_text());(out/'manifest.json').write_text(json.dumps(dict(samples=rows,names=['PottedPlant','CutArrangement'],sources=m['sources'],source_manifest_sha256=sha(source),review_sha256=sha(review),training_approved=False,scope='TRAIN-reserved staging only; full clearance and broader batch required'),indent=2)+'\n');print(json.dumps(dict(metadata_files=len(inputs),hits=hits,errors=len(errors),staged=len(rows))))
if __name__=='__main__':main()
