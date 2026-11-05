import json,hashlib
from pathlib import Path
from PIL import Image,ImageDraw
root=Path.cwd();out=root/'Reports/plant-broader-train-v14-review';idx=json.loads((out/'index.json').read_text());r=idx['samples'][2];im=Image.open(root/r['image']).convert('RGB');w,h=im.size;boxes=[(0,410,173,768),(96,641,184,768)];lines=[];draw=ImageDraw.Draw(im)
for x1,y1,x2,y2 in boxes:
 lines.append(f'0 {(x1+x2)/2/w:.8f} {(y1+y2)/2/h:.8f} {(x2-x1)/w:.8f} {(y2-y1)/h:.8f}')
 draw.rectangle((x1,y1,x2-1,y2-1),outline='red',width=3)
(out/'draft-labels').mkdir();label=out/'draft-labels'/f'{r["original_id"]}.txt';label.write_text('\n'.join(lines)+'\n');im.save(out/'draft-11db-overlay.jpg')
record=dict(scope='Nine original-resolution photos actually viewed; no inference-based selection',training_approved=False,samples=[])
notes={1:'Right potted palm clear, but dark plants behind car partly occluded; requires exhaustive background instance review.',2:'Two left-edge planted containers visible, both clipped; drafted two class0 boxes.',3:'Three foreground large palm planters plus left-edge truncated planter; background vegetation association requires review.',4:'Foreground rubber plant plus multiple distant plants behind globe/rails and downstairs; reflections excluded, exhaustive instances pending.',5:'Continuous planted architectural bed foreground and two rear troughs; per-container convention needs explicit scope review.',6:'Multiple hanging baskets plus many nursery seedling pots/troughs; incomplete source labels must not be accepted as exhaustive.',7:'Center large cropped pot and right small pot, additional upper-edge foliage with hidden containers; review association before release.',8:'Large foreground planter and many distant patio pots; exhaustive annotations pending.',9:'Central clear cut-flower vase auxiliary and multiple rooted peripheral pots; left background arrangement root/cut status uncertain.'}
for i,n in notes.items():
 row=idx['samples'][i];record['samples'].append(dict(original_id=row['original_id'],image_sha256=row['sha256'],original_resolution_viewed=True,status='DRAFT_ANNOTATION_REVIEW_REQUIRED' if i==2 else 'HOLD_EXHAUSTIVE_INSTANCE_OR_ONTOLOGY_REVIEW',observation=n))
record['draft']=dict(original_id=r['original_id'],label=str(label.relative_to(root)),label_sha256=hashlib.sha256(label.read_bytes()).hexdigest(),boxes_xyxy=boxes,classes={'0':'PottedPlant','1':'CutArrangement'})
(out/'original-review.json').write_text(json.dumps(record,indent=2)+'\n')
