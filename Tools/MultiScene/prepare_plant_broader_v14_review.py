import json,hashlib
from pathlib import Path
from PIL import Image,ImageOps,ImageDraw
root=Path.cwd();source=root/'Datasets/openimages-plant-fresh-general-source-v4/manifest.json';m=json.loads(source.read_text());review=json.loads((root/'Reports/plant-fresh-general-v4-contact-review-final.json').read_text());wanted={r['original_id'] for r in review['samples'] if r['decision']=='ORIGINAL_REVIEW_REQUIRED'}
def norm(s):return str(s).lower().replace('openimages-v7:','').replace('http:','https:').replace('www.','').rstrip('/')
ids=set();authors=set();digests=set();errors=[]
for p in (root/'Datasets').glob('*/audit.json'):
 try:a=json.loads(p.read_text(encoding='utf-8-sig'))
 except Exception as e:errors.append({'path':str(p.relative_to(root)),'error':str(e)});continue
 for r in a.get('samples',[]):
  if r.get('split') not in ('train','val','test'):continue
  ids.add(r.get('original_id'));authors.add(norm(r.get('group','')));digests.add(r.get('sha256',r.get('image_sha256')))
rows=[]
for r in m['samples']:
 if r['original_id'] not in wanted or r['original_id'] in ids or norm(r['group']) in authors:continue
 p=source.parent/r['image'];digest=hashlib.sha256(p.read_bytes()).hexdigest()
 if digest in digests:continue
 rows.append(dict(original_id=r['original_id'],image=str(p.relative_to(root)),group=r['group'],sha256=digest,attribution=r['attribution'],source_label=str((source.parent/r['label']).relative_to(root))))
out=root/'Reports/plant-broader-train-v14-review';out.mkdir(exist_ok=False)
selected=rows[:12];sheet=Image.new('RGB',(1200,((len(selected)+2)//3)*320),'white');draw=ImageDraw.Draw(sheet)
for i,r in enumerate(selected):
 im=Image.open(root/r['image']).convert('RGB');im.thumbnail((390,285));x=(i%3)*400;y=(i//3)*320;sheet.paste(im,(x+(400-im.width)//2,y));draw.text((x+5,y+288),str(i)+' '+r['original_id'],fill='black')
sheet.save(out/'contact.jpg');record=dict(scope='Provisional unused-source original review; not TRAIN release. Full manifest/reservation and decoded-pixel clearance still required.',source_manifest_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),eligible_audit_disjoint_candidates=len(rows),audit_errors=errors,training_approved=False,samples=selected);(out/'index.json').write_text(json.dumps(record,indent=2)+'\n');print(json.dumps({'eligible':len(rows),'selected':len(selected),'errors':len(errors)}))
