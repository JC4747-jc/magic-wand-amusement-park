"""Download reserved unreviewed source photos, retain failures and attribution."""
import argparse,json,hashlib,concurrent.futures,io
from pathlib import Path
import requests
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--plan',default='Reports/plant-cut-arrangement-expansion-v15-selection.json');parser.add_argument('--out',default='Datasets/plant-cut-arrangement-source-v15');args=parser.parse_args()
 plan=ROOT/args.plan;selection=json.loads(plan.read_text());out=ROOT/args.out;out.mkdir(exist_ok=False);(out/'images').mkdir();(out/'selection.json').write_bytes(plan.read_bytes())
 def fetch(r):
  try:
   response=requests.get(r['download_url'],timeout=(10,25));response.raise_for_status();raw=response.content
   with Image.open(io.BytesIO(raw)) as im:im.verify()
   path=out/'images'/f'{r["original_id"]}.jpg';path.write_bytes(raw);return dict(**r,image='images/'+path.name,sha256=hashlib.sha256(raw).hexdigest())
  except Exception as e:return dict(original_id=r['original_id'],error=type(e).__name__,detail=str(e)[:350])
 rows=[];failures=[]
 with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
  for result in pool.map(fetch,selection['samples']):
   (rows if 'image' in result else failures).append(result);print(result['original_id'],'DOWNLOADED' if 'image' in result else 'FAILED',flush=True)
 record=dict(source='openimages-v7',upstream_split='test',project_reserved_split='train',selection_sha256=hashlib.sha256(plan.read_bytes()).hexdigest(),sources=[dict(id='openimages-v7',url='https://storage.googleapis.com/openimages/web/download_v7.html',license='Selected image metadata CC BY2.0; per-image attribution retained')],samples=rows,failures=failures,training_approved=False,scope='Unreviewed source photos only; title keywords do not determine classes. No labels generated. Source/pixel/author reservation clearance still required.');(out/'manifest.json').write_text(json.dumps(record,indent=2)+'\n');print(json.dumps(dict(downloaded=len(rows),failed=len(failures))),flush=True)
if __name__=='__main__':main()
