"""Manual rooted-container labels after full-original review."""
import json,hashlib
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 source=ROOT/'Datasets/plant-rooted-source-v16';m=json.loads((source/'manifest.json').read_text());out=ROOT/'Reports/plant-rooted-v16-review/annotations';out.mkdir(exist_ok=False);(out/'labels').mkdir();spec={'aa3bfefd96c831a9':[(0,38,0,994,677)],'e79382f6150d550d':[(0,176,55,778,708)],'73aa40ea11e5aa92':[(0,515,392,802,683)]};rows=[]
 for identifier,boxes in spec.items():
  r=next(r for r in m['samples'] if r['original_id']==identifier);ip=source/r['image'];assert sha(ip)==r['sha256'];im=Image.open(ip).convert('RGB');w,h=im.size;draw=ImageDraw.Draw(im);lines=[]
  for c,a,b,x,y in boxes:
   assert 0<=a<x<=w and 0<=b<y<=h;lines.append(f'{c} {(a+x)/2/w:.8f} {(b+y)/2/h:.8f} {(x-a)/w:.8f} {(y-b)/h:.8f}');draw.rectangle((a,b,x-1,y-1),outline='red',width=3)
  lp=out/'labels'/f'{identifier}.txt';lp.write_text('\n'.join(lines)+'\n');overlay=out/f'{identifier}.jpg';im.save(overlay);rows.append(dict(original_id=identifier,source_image=str(ip.relative_to(ROOT)),source_sha256=r['sha256'],group=r['group'],attribution=r['attribution'],label=str(lp.relative_to(ROOT)),label_sha256=sha(lp),overlay=str(overlay.relative_to(ROOT)),boxes_class_xyxy=boxes,negative=False,original_reviewed=True,overlay_reviewed=False,training_approved=False))
 (out/'draft.json').write_text(json.dumps(dict(samples=rows,names=['PottedPlant','CutArrangement'],training_approved=False,convention='Visible rooted foliage plus complete visible planted container; shared cactus vessel is one planted-container instance, ground-grown background excluded.'),indent=2)+'\n')
if __name__=='__main__':main()
