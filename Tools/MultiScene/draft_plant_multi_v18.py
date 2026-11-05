"""Manual rooted-container labels after full-original review."""
import json,hashlib
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 source=ROOT/'Datasets/openimages-plant-fresh-general-source-v4';m=json.loads((source/'manifest.json').read_text());out=ROOT/'Reports/plant-multi-source-v18-review/annotations';out.mkdir(exist_ok=False);(out/'labels').mkdir();spec={'51feff5533afd1a2':[(0,354,344,501,525),(0,553,317,659,530),(0,651,359,945,529),(0,850,346,1024,529)]};rows=[]
 for identifier,boxes in spec.items():
  r=next(r for r in m['samples'] if r['original_id']==identifier);ip=source/r['image'];r['sha256']=sha(ip);im=Image.open(ip).convert('RGB');w,h=im.size;draw=ImageDraw.Draw(im);lines=[]
  for c,a,b,x,y in boxes:
   assert 0<=a<x<=w and 0<=b<y<=h;lines.append(f'{c} {(a+x)/2/w:.8f} {(b+y)/2/h:.8f} {(x-a)/w:.8f} {(y-b)/h:.8f}');draw.rectangle((a,b,x-1,y-1),outline='red',width=3)
  lp=out/'labels'/f'{identifier}.txt';lp.write_text('\n'.join(lines)+'\n');overlay=out/f'{identifier}.jpg';im.save(overlay);rows.append(dict(original_id=identifier,source_image=str(ip.relative_to(ROOT)),source_sha256=r['sha256'],group=r['group'],attribution=r['attribution'],label=str(lp.relative_to(ROOT)),label_sha256=sha(lp),overlay=str(overlay.relative_to(ROOT)),boxes_class_xyxy=boxes,negative=False,original_reviewed=True,overlay_reviewed=False,training_approved=False))
 (out/'draft.json').write_text(json.dumps(dict(samples=rows,names=['PottedPlant','CutArrangement'],training_approved=False,convention='Three rooted geranium containers plus one clipped orchid cachepot; include visible outer wicker cachepot. Empty left basket and exterior ground vegetation excluded. Overlapping visible extents retain distinct container identity.'),indent=2)+'\n')
if __name__=='__main__':main()
