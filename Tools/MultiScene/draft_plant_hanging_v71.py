"""Manual draft for two reserved TRAIN hanging containers; no automatic adoption."""
import hashlib,json
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 out=ROOT/'Reports/plant-hanging-v71-boxes';assert not out.exists();out.mkdir()
 rows=[]
 for version,pid,box in [('v49',106792794,[171,519,741,1092]),('v70',57348594,[305,0,952,541])]:
  source=ROOT/('Datasets/plant-hanging-train-source-'+version);manifest=source/'manifest.json';r=next(r for r in json.loads(manifest.read_text())['samples'] if r['page_id']==pid)
  image=source/r['image'];assert sha(image)==r['sha256'];im=Image.open(image).convert('RGB');w,h=im.size;x1,y1,x2,y2=box;assert 0<=x1<x2<=w and 0<=y1<y2<=h
  label=out/(str(pid)+'.txt');label.write_text('0 '+' '.join(f'{v:.8f}' for v in [(x1+x2)/2/w,(y1+y2)/2/h,(x2-x1)/w,(y2-y1)/h])+'\n',encoding='utf-8')
  d=ImageDraw.Draw(im);d.rectangle((x1,y1,x2-1,y2-1),outline='lime',width=4);d.text((x1+5,y1+5),'DRAFT PottedPlant',fill='red');overlay=out/(str(pid)+'.png');im.save(overlay)
  rows.append(dict(page_id=pid,image=image.relative_to(ROOT).as_posix(),image_sha256=sha(image),source_manifest_sha256=sha(manifest),label=label.relative_to(ROOT).as_posix(),label_sha256=sha(label),overlay=overlay.relative_to(ROOT).as_posix(),overlay_sha256=sha(overlay),boxes_class_xyxy=[[0,*box]],reserved_split='train',original_reviewed=True,overlay_reviewed=False,annotation_approved=False,artist=r['artist'],group=r['group'],license=r['license'],license_url=r['license_url'],publisher_metadata=r['publisher_metadata']))
 (out/'draft.json').write_text(json.dumps(dict(samples=rows,training_approved=False,deployment_accepted=False,scope='Visible rooted plant and container extent; hanging straps/hooks excluded. Sparse planted container remains positive; surrounding ground vegetation excluded.'),indent=2),encoding='utf-8');print('Two reserved TRAIN manual drafts, no dataset adopted')
if __name__=='__main__':main()
