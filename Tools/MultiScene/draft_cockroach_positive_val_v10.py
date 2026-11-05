"""Pre-inference annotations from reviewed originals; overlays require separate review."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
BOXES = {'394667350': [[215, 373, 354, 568]], '394596555': [[60, 383, 626, 884], [0, 340, 99, 408]], '394545119': [[159, 75, 460, 614]], '394189172': [[174, 276, 529, 942]], '394126699': [[246, 228, 498, 671]], '393829078': [[180, 174, 470, 590]], '393607957': [[175, 59, 991, 474]], '393079475': [[70, 20, 693, 855]], '392934945': [[162, 323, 521, 737], [497, 575, 576, 755]], '392804359': [[166, 210, 704, 936]], '392652023': [[143, 396, 269, 544]], '392623709': [[237, 415, 618, 851]], '391709224': [[167, 194, 577, 544]], '391650793': [[214, 308, 552, 735]], '391301148': [[142, 360, 597, 975]], '391239146': [[314, 101, 741, 637]], '391165463': [[438, 367, 735, 683]], '391134628': [[235, 119, 501, 536]], '391020436': [[79, 420, 467, 796]]}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    source = ROOT/'Datasets/cockroach-positive-val-v10-source'
    manifest = json.loads((source/'manifest.json').read_text())
    out = ROOT/'Reports/cockroach-positive-val-v10-label-review'
    out.mkdir(exist_ok=False)
    records = []
    for row in manifest['samples']:
        ident = row['original_id']
        if ident not in BOXES:
            assert ident == '393617508'
            continue
        boxes = BOXES[ident]
        path = source/row['image']
        assert sha(path) == row['sha256'] and row['reserved_split'] == 'val'
        with Image.open(path) as im:
            frame = im.convert('RGB'); w,h = frame.size
        lines=[]; draw=ImageDraw.Draw(frame)
        for a,b,c,d in boxes:
            assert 0<=a<c<=w and 0<=b<d<=h, (ident,frame.size)
            lines.append(f'0 {(a+c)/2/w:.10f} {(b+d)/2/h:.10f} {(c-a)/w:.10f} {(d-b)/h:.10f}')
            draw.rectangle([a,b,c,d],outline='red',width=4)
        label=out/f'{ident}.txt';label.write_text('\n'.join(lines)+'\n')
        overlay=out/f'{ident}-overlay.jpg';frame.save(overlay)
        records.append(dict(original_id=ident,source_sha256=sha(path),boxes_xyxy=boxes,
            label=label.relative_to(ROOT).as_posix(),label_sha256=sha(label),
            overlay_sha256=sha(overlay),original_viewed=True,overlay_reviewed=False,
            reserved_split='val',training_approved=False))
    payload=dict(samples=records,source_manifest_sha256=sha(source/'manifest.json'),
        annotation_convention='Visible body and legs; exclude long antennae, no inferred occluded extent',
        limitations=['Publisher taxonomy not independently biologically verified',
            'All20originals viewed before inference;393617508 held for unresolved overlapping instance count; two clipped partial instances explicitly labelled',
            'Includes small, blurred, ventral, low-light, grass-occluded and multiple-instance challenge images'],
        evaluation_approved=False,training_approved=False,inference_performed=False)
    (out/'draft.json').write_text(json.dumps(payload,indent=2))
    for page in range((len(records)+3)//4):
        canvas=Image.new('RGB',(1280,1000),'white');draw=ImageDraw.Draw(canvas)
        for i,row in enumerate(records[page*4:page*4+4]):
            with Image.open(out/f'{row["original_id"]}-overlay.jpg') as frame:
                frame.thumbnail((620,460));x=i%2*640;y=i//2*500;canvas.paste(frame,(x,y))
            draw.text((x,y+465),row['original_id'],fill='black')
        canvas.save(out/f'contact-{page+1}.jpg')
    print(json.dumps(dict(images=len(records),instances=sum(len(r['boxes_xyxy']) for r in records))))


if __name__ == '__main__':
    main()
