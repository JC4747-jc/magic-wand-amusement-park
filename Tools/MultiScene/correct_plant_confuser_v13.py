"""Separate correction artifacts after full-resolution v13 overlay inspection."""
import json,hashlib
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
FIX={0:[[30,65,740,1024]],1:[[0,120,681,1020]],2:[[0,0,1024,940]],5:[[35,35,683,680]],6:[[0,90,485,620],[530,45,1024,575]],7:[[35,90,350,420]],9:[[75,190,970,735]],11:[[140,35,850,684]],15:[[0,100,720,945]],16:[[120,90,810,700]]}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    out=ROOT/'Reports/plant-confuser-v13-review';d=json.loads((out/'draft.json').read_text());rows=[]
    for r in d['samples']:
        i=r['index'];r['full_resolution_viewed']=True;r['overlay_reviewed']=False
        if i in FIX:
            ip=ROOT/'Datasets/plant-native-v12'/r['image'];im=Image.open(ip).convert('RGB');w,h=im.size;draw=ImageDraw.Draw(im);lines=[]
            for a,b,c,e in FIX[i]:
                assert 0<=a<c<=w and 0<=b<e<=h
                lines.append(f'1 {(a+c)/(2*w):.9f} {(b+e)/(2*h):.9f} {(c-a)/w:.9f} {(e-b)/h:.9f}');draw.rectangle([a,b,c,e],outline='red',width=4)
            label=out/f"{r['original_id']}-corrected.txt";assert not label.exists();label.write_text('\n'.join(lines)+'\n');overlay=out/f"{r['original_id']}-corrected-overlay.jpg";im.save(overlay)
            r.update(label=str(label.relative_to(ROOT)),label_sha256=sha(label),overlay_sha256=sha(overlay),boxes=FIX[i],corrected_overlay=str(overlay.relative_to(ROOT)))
        rows.append(r)
    for page in range(3):
        sheet=Image.new('RGB',(1500,1700),'white');draw=ImageDraw.Draw(sheet)
        for pos,r in enumerate(rows[page*6:page*6+6]):
            path=ROOT/r['corrected_overlay'] if 'corrected_overlay' in r else out/f"{r['original_id']}-overlay.jpg";im=Image.open(path);im.thumbnail((740,520));x=pos%2*750;y=pos//2*565;sheet.paste(im,(x,y));draw.text((x,y+525),str(r['index']),fill='black')
        sheet.save(out/f'corrected-contact-{page}.jpg')
    (out/'corrected-draft.json').write_text(json.dumps(dict(samples=rows,names=['PottedPlant','CutArrangement'],definition='Decorative cut-flower or ornamental cut-branch arrangement; one visible vessel/assembled bouquet per instance; excludes mirror reflection, rooted plants, harvested herbs and animal fodder',training_approved=False,remaining='Review corrected overlays and all positive TRAIN / VAL completeness'),indent=2))
if __name__=='__main__':main()
