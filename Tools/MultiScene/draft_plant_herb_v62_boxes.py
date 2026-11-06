"""Create manual, non-prediction box drafts for actual visual QA."""
import hashlib,json
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
BOXES={66493232:[15,229,874,1280],90466407:[44,520,960,1768],148029028:[85,54,800,800],36674871:[138,50,762,629]}


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
 source=ROOT/'Datasets/plant-herb-train-source-v62'
 m=json.loads((source/'manifest.json').read_text())
 out=ROOT/'Reports/plant-herb-train-v62-boxes';assert not out.exists();out.mkdir()
 rows=[]
 for key,box in BOXES.items():
  r=next(r for r in m['samples'] if r['page_id']==key)
  p=source/r['image'];assert sha(p)==r['sha256']
  im=Image.open(p).convert('RGB');w,h=im.size;x1,y1,x2,y2=box
  assert (w,h)==tuple(r['dimensions']) and 0<=x1<x2<=w and 0<=y1<y2<=h
  label=out/f'{key}.txt';label.write_text('0 '+' '.join(f'{v:.8f}' for v in [(x1+x2)/2/w,(y1+y2)/2/h,(x2-x1)/w,(y2-y1)/h])+'\n',encoding='utf-8')
  draw=ImageDraw.Draw(im);draw.rectangle((x1,y1,x2-1,y2-1),outline='lime',width=4);draw.text((x1+5,y1+5),'DRAFT PottedPlant',fill='red')
  overlay=out/f'{key}.png';im.save(overlay)
  rows.append(dict(page_id=key,image=p.relative_to(ROOT).as_posix(),image_sha256=sha(p),
   label=label.relative_to(ROOT).as_posix(),label_sha256=sha(label),overlay=overlay.relative_to(ROOT).as_posix(),
   overlay_sha256=sha(overlay),boxes_class_xyxy=[[0,*box]],original_reviewed=True,overlay_reviewed=False,annotation_approved=False,reserved_split='train'))
 (out/'draft.json').write_text(json.dumps(dict(samples=rows,source_manifest_sha256=sha(source/'manifest.json'),training_approved=False),indent=2),encoding='utf-8')
 print('Created four manual box drafts; overlays require actual inspection')


if __name__=='__main__':main()
