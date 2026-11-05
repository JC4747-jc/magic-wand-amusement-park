"""Select title-based cut-arrangement source candidates, never label from keywords."""
import csv,json,hashlib
from pathlib import Path
from audit_plant_cutflower_v11_candidates import norm
ROOT=Path(__file__).resolve().parents[2]
def main():
 metadata=ROOT/'Datasets/openimages-plant-fresh-general-source-v4/metadata.csv';ids=set();authors=set();errors=[]
 for p in (ROOT/'Datasets').glob('*/manifest.json'):
  try:m=json.loads(p.read_text(encoding='utf-8-sig'))
  except Exception as e:errors.append(dict(path=str(p.relative_to(ROOT)),error=str(e)));continue
  for r in m.get('samples',[]):
   if r.get('original_id'):ids.add(r['original_id'])
   if r.get('group'):authors.add(norm(r['group']))
 candidates=[];keywords=['bouquet','flower arrangement','floral arrangement','flowers in a vase','flower vase']
 with metadata.open(encoding='utf-8') as stream:
  for r in csv.DictReader(stream):
   title=r.get('Title','').lower();author=r.get('AuthorProfileURL','');identifier=r['ImageID']
   if not any(t in title for t in keywords) or identifier in ids or not author or norm(author) in authors:continue
   if r.get('License','').replace('http:','https:').rstrip('/')!='https://creativecommons.org/licenses/by/2.0' or r.get('Rotation') not in ('0','0.0'):continue
   candidates.append(dict(original_id=identifier,group=author,attribution={k:r.get(k) for k in ['License','Author','AuthorProfileURL','OriginalLandingURL','Title']},download_url=f'https://open-images-dataset.s3.amazonaws.com/test/{identifier}.jpg',reserved_split='train',annotation_status='NOT_REVIEWED'))
 selected=[];groups=set()
 for r in sorted(candidates,key=lambda r:r['original_id']):
  if norm(r['group']) in groups:continue
  groups.add(norm(r['group']));selected.append(r)
 out=ROOT/'Reports/plant-cut-arrangement-expansion-v15-selection.json';assert not out.exists();out.write_text(json.dumps(dict(scope='Title-based source acquisition only, not semantic labels; all-current-manifest ID/author exclusions, full clearance still required',metadata_sha256=hashlib.sha256(metadata.read_bytes()).hexdigest(),keywords=keywords,eligible_images=len(candidates),unique_author_candidates=len(selected),metadata_errors=errors,training_approved=False,samples=selected[:24]),indent=2)+'\n');print(json.dumps(dict(eligible=len(candidates),unique_authors=len(selected),selected=min(24,len(selected)),errors=len(errors))))
if __name__=='__main__':main()
