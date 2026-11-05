"""Propose TRAIN-only instance fixes after original review; preserve released datasets."""
from pathlib import Path
import json,hashlib
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 data=ROOT/'Datasets/plant-rooted-v18';audit=data/'audit.json';a=json.loads(audit.read_text());assert sha(audit)=='f3eefa042dd1ea24acc10213dfb4eb15aeab4887ee5f52e21b71e91a486f6616'
 out=ROOT/'Reports/plant-train-instance-audit-v19/corrections';out.mkdir(exist_ok=False);(out/'labels').mkdir();rows=[]
 for identifier in ['6f30db47a5308f81','bf66f91af557ad59']:
  r=next(r for r in a['samples'] if r['original_id']==identifier);assert r['split']=='train';ip=data/r['image'];lp=data/r['label'];assert sha(ip)==r['sha256'] and sha(lp)==r['label_sha256'];im=Image.open(ip).convert('RGB');w,h=im.size;labels=[list(map(float,l.split())) for l in lp.read_text().splitlines()];boxes=[]
  for c,x,y,bw,bh in labels:boxes.append([int(c),(x-bw/2)*w,(y-bh/2)*h,(x+bw/2)*w,(y+bh/2)*h])
  if identifier=='6f30db47a5308f81':boxes[1]=[0,814,115,1024,490];reason='Right planted maple visible container lower edge omitted from old box; retain main bonsai box.'
  else:boxes=boxes[:1]+[[0,374,311,436,348],[0,377,347,437,379],[0,375,377,447,414]]+boxes[2:];reason='Three independent planted baskets in vertical metal rack must be three instances, not one group box; retain four large vine pots.'
  lines=[];draw=ImageDraw.Draw(im)
  for i,(c,x0,y0,x1,y1) in enumerate(boxes):
   assert 0<=x0<x1<=w and 0<=y0<y1<=h;lines.append(f'{c} {(x0+x1)/2/w:.8f} {(y0+y1)/2/h:.8f} {(x1-x0)/w:.8f} {(y1-y0)/h:.8f}');draw.rectangle((x0,y0,x1-1,y1-1),outline=['red','blue','yellow','lime','magenta'][i%5],width=2)
  target=out/'labels'/lp.name;target.write_text(chr(10).join(lines)+chr(10));overlay=out/f'{identifier}.jpg';im.save(overlay);rows.append(dict(original_id=identifier,split='train',source_image=str(ip.relative_to(ROOT)),source_sha256=sha(ip),parent_label=str(lp.relative_to(ROOT)),parent_label_sha256=sha(lp),label=str(target.relative_to(ROOT)),label_sha256=sha(target),overlay=str(overlay.relative_to(ROOT)),overlay_sha256=sha(overlay),old_instances=len(labels),new_instances=len(boxes),reason=reason,original_reviewed=True,overlay_reviewed=False,training_approved=False))
 (out/'draft.json').write_text(json.dumps(dict(parent_dataset_sha256=sha(audit),samples=rows,scope='Proposed TRAIN-only fixes; no old dataset mutation, no held-out label changes',training_approved=False),indent=2));print('Drafted two TRAIN corrections, instance count +2')
if __name__=='__main__':main()
