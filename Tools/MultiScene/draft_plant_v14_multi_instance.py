"""Draft multi-instance labels from full-resolution original review; preserve sources."""
import json,hashlib
from pathlib import Path
from PIL import Image,ImageDraw
root=Path.cwd();index=json.loads((root/'Reports/plant-broader-train-v14-review-batch2/index.json').read_text());out=root/'Reports/plant-broader-v14-multi-instance-review';out.mkdir(exist_ok=False);(out/'labels').mkdir();spec={'307dd3482f39b31b':[(0,0,541,105,874),(0,219,705,334,863),(0,555,707,683,881)],'3c086d4c1b00cd15':[(0,0,211,294,671),(0,343,837,450,978)]};rows=[]
for identifier,boxes in spec.items():
 r=next(r for r in index['samples'] if r['original_id']==identifier);im=Image.open(root/r['image']).convert('RGB');w,h=im.size;draw=ImageDraw.Draw(im);lines=[]
 for c,a,b,x,y in boxes:
  assert 0<=a<x<=w and 0<=b<y<=h;lines.append(f'{c} {(a+x)/2/w:.8f} {(b+y)/2/h:.8f} {(x-a)/w:.8f} {(y-b)/h:.8f}');draw.rectangle((a,b,x-1,y-1),outline='red',width=3)
 label=out/'labels'/f'{identifier}.txt';label.write_text('\n'.join(lines)+'\n');overlay=out/f'{identifier}.jpg';im.save(overlay);rows.append(dict(original_id=identifier,source_image=r['image'],source_sha256=r['sha256'],group=r['group'],attribution=r['attribution'],label=str(label.relative_to(root)),label_sha256=hashlib.sha256(label.read_bytes()).hexdigest(),overlay=str(overlay.relative_to(root)),boxes_class_xyxy=boxes,original_reviewed=True,overlay_reviewed=False,training_approved=False))
(out/'draft.json').write_text(json.dumps(dict(samples=rows,scope='Manual instance drafts only; no training release',training_approved=False),indent=2)+'\n')
