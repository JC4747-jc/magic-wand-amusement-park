import json,hashlib
from pathlib import Path
from PIL import Image,ImageDraw
root=Path.cwd();out=root/'Reports/plant-broader-train-v14-review-batch2';index=json.loads((out/'index.json').read_text());(out/'draft-labels').mkdir();(out/'overlays').mkdir();spec={'2c716fe6536d46a6':[(0,299,25,830,562),(0,0,519,110,589)],'3287fcb539ba2915':[(1,272,754,457,1024)]};records=[]
for identifier,boxes in spec.items():
 r=next(r for r in index['samples'] if r['original_id']==identifier);im=Image.open(root/r['image']).convert('RGB');w,h=im.size;draw=ImageDraw.Draw(im);lines=[]
 for c,x1,y1,x2,y2 in boxes:
  assert 0<=x1<x2<=w and 0<=y1<y2<=h
  lines.append(f'{c} {(x1+x2)/2/w:.8f} {(y1+y2)/2/h:.8f} {(x2-x1)/w:.8f} {(y2-y1)/h:.8f}');draw.rectangle((x1,y1,x2-1,y2-1),outline='red' if c==0 else 'blue',width=3)
 label=out/'draft-labels'/f'{identifier}.txt';label.write_text('\n'.join(lines)+'\n');overlay=out/'overlays'/f'{identifier}.jpg';im.save(overlay);records.append(dict(original_id=identifier,source_image=r['image'],source_sha256=r['sha256'],group=r['group'],attribution=r['attribution'],label=str(label.relative_to(root)),label_sha256=hashlib.sha256(label.read_bytes()).hexdigest(),overlay=str(overlay.relative_to(root)),boxes_class_xyxy=boxes,original_reviewed=True,overlay_reviewed=False))
(out/'draft.json').write_text(json.dumps(dict(scope='Manual native class0 rooted plant/class1 decorative cut-arrangement drafts; not TRAIN release',training_approved=False,samples=records),indent=2)+'\n');print(len(records))
