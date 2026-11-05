"""Pre-inference annotations from reviewed originals; overlays require separate review."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
BOXES = {'384754727': [[235, 396, 491, 588]], '384552781': [[226, 220, 443, 548]], '384251639': [[284, 416, 721, 797]], '384135961': [[282, 506, 491, 664]], '383865118': [[150, 107, 920, 858]], '383785519': [[169, 157, 725, 530]], '383730444': [[175, 435, 445, 692]], '383512089': [[374, 250, 544, 433]], '383498870': [[369, 210, 775, 1001]], '383367087': [[429, 294, 698, 454]], '382520138': [[192, 274, 531, 778]], '381824452': [[169, 376, 542, 822]], '379972774': [[276, 211, 738, 568]], '379797254': [[268, 449, 467, 677]], '379319524': [[382, 271, 850, 592]], '377369329': [[312, 381, 556, 598]]}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    source = ROOT/'Datasets/cockroach-fresh-positive-v12-source'
    manifest = json.loads((source/'manifest.json').read_text())
    plan = json.loads((ROOT/'Reports/cockroach-v11-fresh-v12-preannotation-plan.json').read_text())
    assert sha(source/'manifest.json') == plan['positive_manifest_sha256']
    out = ROOT/'Reports/cockroach-fresh-v12-positive-label-review'
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
            'All16positiveoriginals viewed before inference; includes ant-covered/altered, incomplete body, handheld, sideview and lowlight cases; all retained',
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
