"""Pre-inference annotations from reviewed originals; overlays require separate review."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
BOXES = {'391014790': [[203, 402, 503, 876]], '390761080': [[333, 483, 469, 625]], '390406012': [[165, 492, 250, 565]], '390370068': [[306, 233, 720, 704]], '390367155': [[0, 28, 768, 850]], '390141759': [[306, 0, 674, 704]], '389910643': [[82, 454, 370, 615]], '389711616': [[255, 364, 540, 780]], '389103795': [[219, 432, 505, 685]], '388511711': [[143, 513, 547, 843]], '388442060': [[0, 166, 522, 735]], '388380786': [[281, 425, 585, 729]], '388359058': [[113, 469, 527, 822]], '387424508': [[407, 354, 834, 630]], '387208486': [[218, 475, 522, 662]], '387168090': [[218, 208, 544, 538]], '387131913': [[277, 342, 520, 631]], '387028177': [[71, 100, 567, 487]], '386594944': [[208, 373, 923, 767]], '386483452': [[141, 400, 366, 561]], '386405931': [[304, 26, 791, 599]], '386153790': [[231, 121, 582, 589]], '385845425': [[100, 93, 860, 986]], '385054435': [[95, 32, 874, 808]]}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    source = ROOT/'Datasets/cockroach-positive-train-v11-source'
    manifest = json.loads((source/'manifest.json').read_text())
    out = ROOT/'Reports/cockroach-positive-train-v11-label-review'
    out.mkdir(exist_ok=False)
    records = []
    for row in manifest['samples']:
        ident = row['original_id']; boxes = BOXES[ident]
        path = source/row['image']
        assert sha(path) == row['sha256'] and row['reserved_split'] == 'train'
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
            reserved_split='train',training_approved=False))
    payload=dict(samples=records,source_manifest_sha256=sha(source/'manifest.json'),
        annotation_convention='Visible body and legs; exclude long antennae, no inferred occluded extent',
        limitations=['Publisher taxonomy not independently biologically verified',
            'All24 TRAIN originals viewed before inference; includes altered/occluded, blurred, small and hand-held examples; species from publisher not independently expert verified',
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
