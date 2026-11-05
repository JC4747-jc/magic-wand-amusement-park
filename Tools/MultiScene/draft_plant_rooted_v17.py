"""Manual rooted-container labels after full-original review."""
import json,hashlib
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 source=ROOT/'Datasets/plant-rooted-source-v17';m=json.loads((source/'manifest.json').read_text());out=ROOT/'Reports/plant-rooted-v17-review/annotations';out.mkdir(exist_ok=False);(out/'labels').mkdir();spec={'6fcf0206525bd64e':[(0,82,24,635,929)],'7105cc6829fcbb98':[(0,0,0,823,716)],'d99ff2185e8d5d7e':[(0,97,265,717,922)],'def587f4b52902f3':[(0,42,100,934,653)],'e87daca4b650f9c2':[(0,164,3,902,704)]};rows=[]
 for identifier,boxes in spec.items():
  r=next(r for r in m['samples'] if r['original_id']==identifier);ip=source/r['image'];assert sha(ip)==r['sha256'];im=Image.open(ip).convert('RGB');w,h=im.size;draw=ImageDraw.Draw(im);lines=[]
  for c,a,b,x,y in boxes:
   assert 0<=a<x<=w and 0<=b<y<=h;lines.append(f'{c} {(a+x)/2/w:.8f} {(b+y)/2/h:.8f} {(x-a)/w:.8f} {(y-b)/h:.8f}');draw.rectangle((a,b,x-1,y-1),outline='red',width=3)
  lp=out/'labels'/f'{identifier}.txt';lp.write_text('\n'.join(lines)+'\n');overlay=out/f'{identifier}.jpg';im.save(overlay);rows.append(dict(original_id=identifier,source_image=str(ip.relative_to(ROOT)),source_sha256=r['sha256'],group=r['group'],attribution=r['attribution'],label=str(lp.relative_to(ROOT)),label_sha256=sha(lp),overlay=str(overlay.relative_to(ROOT)),boxes_class_xyxy=boxes,negative=False,original_reviewed=True,overlay_reviewed=False,training_approved=False))
 (out/'draft.json').write_text(json.dumps(dict(samples=rows,names=['PottedPlant','CutArrangement'],training_approved=False,convention='Visible rooted foliage plus complete visible planted container; shared cactus vessel is one planted-container instance, ground-grown background excluded.'),indent=2)+'\n')
if __name__=='__main__':main()
