"""Manual v93 drafts; no automatic labels or training approval."""
import hashlib,json
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
BOXES={90151588:[[0,171,64,250],[268,177,343,248],[604,182,677,252],[872,174,960,253]],97729076:[[0,36,934,623]]}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 src=ROOT/'Datasets/plant-hanging-train-source-v92';manifest=src/'manifest.json';data=json.loads(manifest.read_text());out=ROOT/'Reports/plant-hanging-v93-boxes';assert not out.exists();out.mkdir();rows=[]
 for pid,boxes in BOXES.items():
  row=next(r for r in data['samples'] if r['page_id']==pid);image=src/row['image'];assert sha(image)==row['sha256'];im=Image.open(image).convert('RGB');w,h=im.size;draw=ImageDraw.Draw(im);lines=[]
  for i,(x1,y1,x2,y2) in enumerate(boxes):
   assert 0<=x1<x2<=w and 0<=y1<y2<=h
   lines.append('0 '+' '.join(f'{v:.8f}' for v in [(x1+x2)/2/w,(y1+y2)/2/h,(x2-x1)/w,(y2-y1)/h]));draw.rectangle((x1,y1,x2-1,y2-1),outline='lime',width=3);draw.text((x1+1,y1+1),str(i+1),fill='red')
  label=out/f'{pid}.txt';label.write_text('\n'.join(lines)+'\n');overlay=out/f'{pid}.png';im.save(overlay)
  rows.append(dict(page_id=pid,image=image.relative_to(ROOT).as_posix(),image_sha256=sha(image),label=label.relative_to(ROOT).as_posix(),label_sha256=sha(label),overlay=overlay.relative_to(ROOT).as_posix(),overlay_sha256=sha(overlay),boxes_class_xyxy=[[0,*b] for b in boxes],reserved_split='train',original_reviewed=True,overlay_reviewed=False,annotation_approved=False,artist=row['artist'],group=row['group'],license=row['license'],license_url=row['license_url'],publisher_metadata=row['publisher_metadata']))
 (out/'draft.json').write_text(json.dumps(dict(samples=rows,source_manifest_sha256=sha(manifest),training_approved=False,deployment_accepted=False),indent=2));print('Two drafts/five instances; no annotation approval')
if __name__=='__main__':main()
