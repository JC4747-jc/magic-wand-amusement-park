"""Reviewed original flowering-container drafts; no automatic training approval."""
import hashlib,json
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
BOXES={'a0893025c69a4ee8':[[60,0,948,768],[0,0,226,429]],'c343032a7e25c932':[[0,0,847,768],[916,153,1024,768]]}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    src=ROOT/'Datasets/openimages-plant-expansion';m=json.loads((src/'manifest.json').read_text());out=ROOT/'Reports/plant-flowering-v12-label-review';out.mkdir(exist_ok=False)
    rows=[]
    for r in m['samples']:
        ident=r['original_id']
        if ident not in BOXES:continue
        image=src/r['image'];frame=Image.open(image).convert('RGB');w,h=frame.size;draw=ImageDraw.Draw(frame);lines=[]
        for x1,y1,x2,y2 in BOXES[ident]:
            assert 0<=x1<x2<=w and 0<=y1<y2<=h
            lines.append(f'0 {(x1+x2)/(2*w):.9f} {(y1+y2)/(2*h):.9f} {(x2-x1)/w:.9f} {(y2-y1)/h:.9f}')
            draw.rectangle([x1,y1,x2,y2],outline='red',width=4)
        label=out/f'{ident}.txt';label.write_text('\n'.join(lines)+'\n');overlay=out/f'{ident}-overlay.jpg';frame.save(overlay)
        rows.append(dict(original_id=ident,source_image=str(image.relative_to(ROOT)),source_sha256=sha(image),group=r['group'],attribution=r['attribution'],label=str(label.relative_to(ROOT)),label_sha256=sha(label),boxes=BOXES[ident],overlay_sha256=sha(overlay),original_reviewed=True,overlay_reviewed=False,training_approved=False))
    (out/'draft.json').write_text(json.dumps(dict(samples=rows,source_manifest_sha256=sha(src/'manifest.json'),scope='Visible rooted-container instances including border-clipped pots; individual flowers are not separate targets',training_approved=False,reservation_clearance_pending=True),indent=2));print(len(rows))
if __name__=='__main__':main()
