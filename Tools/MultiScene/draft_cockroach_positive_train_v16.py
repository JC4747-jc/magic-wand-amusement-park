"""Stage visible-instance annotations from original-image review, never model boxes."""
import hashlib
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
BOXES = {
    '377165928': [[0,385,576,1024]],
    '377065225': [[160,194,725,595]],
    '376616084': [[115,350,335,555]],
    '376482057': [[230,200,640,710]],
    '374955641': [[130,400,405,625]],
    '374733411': [[345,235,422,326]],
    '372977730': [[155,135,630,765]],
}
SECOND_BATCH = {
    '372869306': [[180,320,420,620]],
    '372770647': [[195,120,400,500]],
    '372704090': [[355,435,485,590]],
    '372339698': [[60,0,760,576]],
    '371218251': [[0,25,950,556]],
    '370757940': [[130,100,680,680]],
    '370716756': [[240,225,845,710]],
    '370056242': [[80,460,405,925]],
    '369807348': [[125,55,555,635]],
    '369627114': [[430,260,770,510]],
    '369133343': [[170,495,595,760]],
    '368879683': [[215,310,570,770]],
    '368646996': [[225,110,425,500]],
    '367991669': [[340,350,620,560]],
    '367759807': [[205,40,720,500]],
    '367574489': [[290,110,930,585]],
}


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--batch', type=int, choices=[1,2], default=1)
    args=parser.parse_args()
    source = ROOT / 'Datasets/cockroach-positive-train-v16-source'
    assert sha(source / 'manifest.json') == '4bb3b4c05f299df34331dd9e147b1250c3bf840f710f3ce350774fcad0eac0f8'
    manifest = json.loads((source / 'manifest.json').read_text())
    lookup = {r['original_id']: r for r in manifest['samples']}
    out = ROOT / f'Reports/cockroach-positive-train-v16-label-review-batch{args.batch}'
    out.mkdir(exist_ok=False)
    samples = []
    for ident, boxes in (BOXES if args.batch==1 else SECOND_BATCH).items():
        row = lookup[ident]
        image = source / row['image']
        assert sha(image) == row['sha256'] and row['reserved_split'] == 'train'
        with Image.open(image) as im:
            frame = im.convert('RGB')
        w, h = frame.size
        lines = []
        for a,b,c,d in boxes:
            assert 0 <= a < c <= w and 0 <= b < d <= h
            lines.append(f'0 {(a+c)/2/w:.10f} {(b+d)/2/h:.10f} {(c-a)/w:.10f} {(d-b)/h:.10f}')
            ImageDraw.Draw(frame).rectangle([a,b,c,d], outline='red', width=4)
        label = out / f'{ident}.txt'
        label.write_text('\n'.join(lines)+'\n')
        overlay = out / f'{ident}-overlay.jpg'
        frame.save(overlay)
        samples.append(dict(original_id=ident, boxes_xyxy=boxes, source_sha256=sha(image),
            label=label.relative_to(ROOT).as_posix(), label_sha256=sha(label),
            overlay_sha256=sha(overlay), original_viewed=True, overlay_reviewed=False,
            reserved_split='train', training_approved=False))
    payload = dict(samples=samples, source_manifest_sha256=sha(source/'manifest.json'),
        annotation_convention='Visible body and legs excluding long antennae; clipped instances retain visible extent',
        training_approved=False, inference_performed=False)
    (out/'draft.json').write_text(json.dumps(payload, indent=2))
    for page in range((len(samples)+3)//4):
        canvas=Image.new('RGB',(1600,1300),'white');draw=ImageDraw.Draw(canvas)
        for i,row in enumerate(samples[page*4:page*4+4]):
            with Image.open(out/f'{row["original_id"]}-overlay.jpg') as im:
                im.thumbnail((790,600));x=i%2*800;y=i//2*650;canvas.paste(im,(x,y))
            draw.text((x,y+605),row['original_id'],fill='black')
        canvas.save(out/f'contact-{page}.jpg')
    print(json.dumps(dict(images=len(samples),training_approved=False)))


if __name__ == '__main__':
    main()
