"""Acquire balanced title candidates for rooted-container review; never infer labels."""
import csv,json,hashlib,collections,re
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
 keywords=['bonsai','succulent','flowerpot','houseplants','pot plant','potted plant','indoor plant','window box'];buckets=collections.defaultdict(list)
 with metadata.open(encoding='utf-8') as stream:
  for r in csv.DictReader(stream):
   title=r.get('Title','').lower();category=next((k for k in keywords if re.search(r'\b'+re.escape(k)+r's?\b',title)),None);author=r.get('AuthorProfileURL','');identifier=r['ImageID']
   if not category or identifier in ids or not author or norm(author) in authors:continue
   if r.get('License','').replace('http:','https:').rstrip('/')!='https://creativecommons.org/licenses/by/2.0' or r.get('Rotation') not in ('0','0.0'):continue
   buckets[category].append(dict(original_id=identifier,group=author,attribution={k:r.get(k) for k in ['License','Author','AuthorProfileURL','OriginalLandingURL','Title']},download_url=f'https://open-images-dataset.s3.amazonaws.com/test/{identifier}.jpg',reserved_split='train',selection_keyword=category,annotation_status='NOT_REVIEWED'))
 selected=[];groups=set()
 for key in keywords:
  count=0
  for r in sorted(buckets[key],key=lambda r:r['original_id']):
   if norm(r['group']) in groups:continue
   groups.add(norm(r['group']));selected.append(r);count+=1
   if count==8:break
 report=dict(scope='Balanced lexical source candidates only; no rooting/class labels inferred. Full source/reservation/pixel and original review required.',metadata_sha256=hashlib.sha256(metadata.read_bytes()).hexdigest(),keywords=keywords,matching='Whole word with optional plural s; potted does not match spotted',per_keyword_limit=8,eligible_counts={k:len(v) for k,v in buckets.items()},selected_counts=dict(collections.Counter(r['selection_keyword'] for r in selected)),metadata_errors=errors,training_approved=False,samples=selected);out=ROOT/'Reports/plant-rooted-expansion-v17-selection.json';assert not out.exists();out.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({k:v for k,v in report.items() if k not in ('samples','scope')}))
if __name__=='__main__':main()
