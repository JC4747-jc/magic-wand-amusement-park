"""Pre-inference annotations from reviewed originals; overlays require separate review."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
BOXES = {'364760827': [[250, 60, 760, 655]], '364691237': [[360, 200, 850, 490]], '363856984': [[385, 370, 655, 780]], '363681808': [[340, 220, 695, 570]], '363572209': [[140, 270, 525, 700]], '363264793': [[310, 150, 650, 615]], '363181216': [[300, 350, 485, 590]], '363068501': [[210, 245, 460, 565]], '362209002': [[355, 370, 655, 640]], '362125988': [[310, 350, 525, 565]], '361917678': [[210, 35, 510, 515]], '361587133': [[160, 420, 545, 655]], '360865787': [[70, 330, 640, 670]], '360135616': [[250, 180, 565, 450]], '360117949': [[430, 355, 565, 430]], '359518271': [[600, 435, 840, 640]]}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    source = ROOT/'Datasets/cockroach-fresh-positive-v17-source'
    manifest = json.loads((source/'manifest.json').read_text())
    plan = json.loads((ROOT/'Reports/cockroach-v16-fresh-v17-preannotation-plan.json').read_text())
    assert sha(source/'manifest.json') == plan['positive_manifest_sha256']
    out = ROOT/'Reports/cockroach-fresh-v17-positive-label-review'
    out.mkdir(exist_ok=False)
    records = []
    for row in manifest['samples']:
        ident = row['original_id']; boxes = BOXES[ident]
        path = source/row['image']
        assert sha(path) == row['sha256'] and row['reserved_split'] == 'test'
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
            reserved_split='test',training_approved=False))
    payload=dict(samples=records,source_manifest_sha256=sha(source/'manifest.json'),
        annotation_convention='Visible body and legs; exclude long antennae, no inferred occluded extent',
        limitations=['Publisher taxonomy not independently biologically verified',
            'All16positiveoriginals viewed before inference; blurred, lowlight, nymph, altered and small targets retained',
            'Includes small, blurred, ventral, low-light, grass-occluded and multiple-instance challenge images'],
        evaluation_approved=False,training_approved=False,inference_performed=False)
    (out/'draft.json').write_text(json.dumps(payload,indent=2))
    for page in range(4):
        canvas=Image.new('RGB',(1280,1000),'white');draw=ImageDraw.Draw(canvas)
        for i,row in enumerate(records[page*4:page*4+4]):
            with Image.open(out/f'{row["original_id"]}-overlay.jpg') as frame:
                frame.thumbnail((620,460));x=i%2*640;y=i//2*500;canvas.paste(frame,(x,y))
            draw.text((x,y+465),row['original_id'],fill='black')
        canvas.save(out/f'contact-{page+1}.jpg')
    print(json.dumps(dict(images=len(records),instances=sum(len(r['boxes_xyxy']) for r in records))))


if __name__ == '__main__':
    main()
