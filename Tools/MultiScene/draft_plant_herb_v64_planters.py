"""Draft both visible balcony planters, preserving prior immutable reviews."""
import hashlib,json
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
 source=ROOT/'Datasets/plant-herb-train-source-v62'
 manifest=json.loads((source/'manifest.json').read_text())
 row=next(r for r in manifest['samples'] if r['page_id']==146665257)
 image=source/row['image'];assert sha(image)==row['sha256']
 im=Image.open(image).convert('RGB');w,h=im.size;assert (w,h)==(960,1276)
 boxes=[[0,95,35,814,1276],[0,105,0,488,103]]
 out=ROOT/'Reports/plant-herb-planters-v64-boxes';assert not out.exists();out.mkdir()
 labels=[];draw=ImageDraw.Draw(im)
 for i,(cls,x1,y1,x2,y2) in enumerate(boxes):
  assert 0<=x1<x2<=w and 0<=y1<y2<=h
  labels.append(str(cls)+' '+' '.join(f'{v:.8f}' for v in [(x1+x2)/2/w,(y1+y2)/2/h,(x2-x1)/w,(y2-y1)/h]))
  draw.rectangle((x1,y1,x2-1,y2-1),outline='lime' if i==0 else 'cyan',width=4)
  draw.text((x1+5,y1+5),f'DRAFT container {i+1}',fill='red')
 label=out/'146665257.txt';label.write_text('\n'.join(labels)+'\n',encoding='utf-8')
 overlay=out/'146665257.png';im.save(overlay)
 record=dict(page_id=row['page_id'],image=image.relative_to(ROOT).as_posix(),image_sha256=sha(image),
  label=label.relative_to(ROOT).as_posix(),label_sha256=sha(label),overlay=overlay.relative_to(ROOT).as_posix(),
  overlay_sha256=sha(overlay),boxes_class_xyxy=boxes,reserved_split='train',original_reviewed=True,
  overlay_reviewed=False,annotation_approved=False,artist=row['artist'],group=row['group'],license=row['license'],license_url=row['license_url'],publisher_metadata=row['publisher_metadata'])
 (out/'draft.json').write_text(json.dumps(dict(samples=[record],source_manifest_sha256=sha(source/'manifest.json'),training_approved=False),indent=2),encoding='utf-8')
 print('One image, two manual draft container boxes; not adopted')


if __name__=='__main__':main()
