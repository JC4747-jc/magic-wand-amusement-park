"""Draft explicit cut-flower confuser labels from reviewed complete-image sheets."""
import json,hashlib
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
# Complete visible arrangement extent, including vessel when visible; one arrangement per vessel.
BOXES={0:[[45,0,740,1024]],1:[[0,30,670,1024]],2:[[0,0,1024,1024]],3:[[0,90,1024,768]],4:[[0,80,685,1024]],5:[[35,30,665,710]],6:[[0,0,485,600],[530,0,1024,575]],7:[[35,90,385,420]],8:[[0,0,1024,768]],9:[[60,180,990,760]],11:[[80,35,1000,684]],14:[[0,0,1024,1024]],15:[[0,65,745,945]],16:[[120,70,810,735]],17:[[0,20,440,710],[385,0,825,735]]}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    out=ROOT/'Reports/plant-confuser-v13-review';rows=json.loads((out/'index.json').read_text());records=[]
    for r in rows:
        i=r['index']
        if i not in BOXES:continue
        ip=ROOT/'Datasets/plant-native-v12'/r['image'];im=Image.open(ip).convert('RGB');w,h=im.size;draw=ImageDraw.Draw(im);lines=[]
        for a,b,c,d in BOXES[i]:
            assert 0<=a<c<=w and 0<=b<d<=h,(i,im.size)
            lines.append(f'1 {(a+c)/(2*w):.9f} {(b+d)/(2*h):.9f} {(c-a)/w:.9f} {(d-b)/h:.9f}');draw.rectangle([a,b,c,d],outline='red',width=4)
        lp=out/f"{r['original_id']}.txt";assert not lp.exists();lp.write_text('\n'.join(lines)+'\n');overlay=out/f"{r['original_id']}-overlay.jpg";im.save(overlay)
        records.append(dict(original_id=r['original_id'],index=i,image=r['image'],image_sha256=sha(ip),label=str(lp.relative_to(ROOT)),label_sha256=sha(lp),overlay_sha256=sha(overlay),boxes=BOXES[i],split='train',overlay_reviewed=False))
    (out/'draft.json').write_text(json.dumps(dict(samples=records,names=['PottedPlant','CutFlowerArrangement'],mapping={'PottedPlant':'ParkPlant','CutFlowerArrangement':None},scope='Training-only auxiliary labels. Positive/VAL background completeness not yet reviewed; cannot release dataset',training_approved=False),indent=2))
    for page in range(3):
        sheet=Image.new('RGB',(1500,1700),'white');draw=ImageDraw.Draw(sheet)
        for pos,r in enumerate(records[page*6:page*6+6]):
            im=Image.open(out/f"{r['original_id']}-overlay.jpg");im.thumbnail((740,520));x=pos%2*750;y=pos//2*565;sheet.paste(im,(x,y));draw.text((x,y+525),f"{r['index']}: {r['original_id']}",fill='black')
        sheet.save(out/f'confuser-overlays-{page}.jpg')
    print(json.dumps(dict(images=len(records),instances=sum(len(r['boxes']) for r in records))))
if __name__=='__main__':main()
