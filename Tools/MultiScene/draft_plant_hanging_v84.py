"""Manual terrestrial potted-plant draft annotations, not model-generated labels."""
import hashlib,json
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
BOXES={48286181: [[38, 342, 950, 960]], 169907542: [[358, 778, 649, 1229]], 169798691: [[35, 352, 823, 1165]]}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 source=ROOT/'Datasets/plant-hanging-train-source-v83';manifest=source/'manifest.json';data=json.loads(manifest.read_text());out=ROOT/'Reports/plant-hanging-v84-boxes';assert not out.exists();out.mkdir();rows=[]
 for pid,boxes in BOXES.items():
  r=next(r for r in data['samples'] if r['page_id']==pid);image=source/r['image'];assert sha(image)==r['sha256'];im=Image.open(image).convert('RGB');w,h=im.size;draw=ImageDraw.Draw(im);lines=[]
  for i,(x1,y1,x2,y2) in enumerate(boxes):
   assert 0<=x1<x2<=w and 0<=y1<y2<=h
   lines.append('0 '+' '.join(f'{v:.8f}' for v in [(x1+x2)/2/w,(y1+y2)/2/h,(x2-x1)/w,(y2-y1)/h]));draw.rectangle((x1,y1,x2-1,y2-1),outline='lime' if i==0 else 'cyan',width=4);draw.text((x1+5,y1+5),f'DRAFT rooted container {i+1}',fill='red')
  label=out/(str(pid)+'.txt');label.write_text('\n'.join(lines)+'\n',encoding='utf-8');overlay=out/(str(pid)+'.png');im.save(overlay)
  rows.append(dict(page_id=pid,image=image.relative_to(ROOT).as_posix(),image_sha256=sha(image),label=label.relative_to(ROOT).as_posix(),label_sha256=sha(label),overlay=overlay.relative_to(ROOT).as_posix(),overlay_sha256=sha(overlay),boxes_class_xyxy=[[0,*b] for b in boxes],reserved_split='train',original_reviewed=True,overlay_reviewed=False,annotation_approved=False,artist=r['artist'],group=r['group'],license=r['license'],license_url=r['license_url'],publisher_metadata=r['publisher_metadata']))
 (out/'draft.json').write_text(json.dumps(dict(samples=rows,source_manifest_sha256=sha(manifest),training_approved=False,deployment_accepted=False),indent=2),encoding='utf-8');print('Three manual drafts/three rooted hanging container instances; overlays require review')
if __name__=='__main__':main()
