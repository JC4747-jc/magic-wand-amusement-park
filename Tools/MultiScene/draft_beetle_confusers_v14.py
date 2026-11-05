"""Human-reviewed beetle TRAIN boxes for a proposed two-class experiment; no inference."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[2]
BATCHES={1:{
    '412932780':[[363,607,449,690]],
    '412881228':[[245,250,650,665]],
    '412878061':[[115,251,847,762]],
    '412874236':[[331,480,445,604]],
    '412873254':[[423,294,576,437]],
    '412872917':[[260,270,625,568]],
    '412869931':[[157,250,653,593]],
    '412868056':[[204,226,759,753]],
},2:{
    '412867256':[[215,292,532,654]],
    '412864850':[[320,190,681,566]],
    '412864476':[[174,470,391,612]],
    '412864147':[[101,324,326,597]],
},3:{
    '412862889':[[62,110,757,718]],
    '412862645':[[269,429,397,629]],
    '412860163':[[260,390,390,640]],
    '412858118':[[180,108,475,450]],
    '412852712':[[130,280,481,850]],
    '412845308':[[210,240,637,900]],
    '412843790':[[210,400,300,485]],
    '412841096':[[183,320,399,481]],
    '412839560':[[380,540,520,705]],
    '412832763':[[275,290,465,460]],
    '412830639':[[240,450,405,625]],
},4:{
    '412787120':[[260,140,700,565]],
    '412755696':[[300,188,780,645]],
    '412748930':[[140,385,640,940]],
    '412736781':[[175,300,560,800]],
    '412678067':[[360,290,625,510]],
    '412659497':[[285,335,775,705]],
    '412653578':[[450,310,570,435]],
    '412634846':[[295,410,705,825]],
    '412563331':[[275,160,570,650]],
    '412542104':[[240,260,975,625]],
    '412513016':[[170,445,400,600]],
    '412501140':[[375,50,860,495]],
    '412476767':[[225,170,660,440]],
    '412469855':[[130,500,640,885]],
    '412459108':[[175,195,780,895]],
    '412427553':[[280,260,640,570]],
},5:{
    '412827126':[[25,190,625,820]],
    '412824889':[[350,195,715,540]],
    '412819302':[[440,390,665,650]],
    '412813020':[[45,280,545,675]],
    '412810133':[[345,325,515,465]],
    '412805427':[[150,495,280,610],[140,635,310,770]],
    '412802464':[[250,170,835,685]],
    '412801341':[[365,310,560,675]],
    '412799738':[[80,370,360,620]],
    '412798974':[[300,395,490,650]],
    '412789394':[[250,190,720,470]],
    '412788983':[[0,175,725,580]],
}}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--batch',type=int,choices=sorted(BATCHES),required=True)
    args=parser.parse_args();recipes=BATCHES[args.batch]
    source=ROOT/('Datasets/cockroach-longhorn-train-v8-source' if args.batch==4 else 'Datasets/cockroach-beetle-train-v7-source')
    if args.batch==5:
        source=ROOT/'Datasets/cockroach-beetle-val-v7-source'
    manifest=json.loads((source/'manifest.json').read_text())
    expected='acdcc824d80195d97d6ef9eb428cf32a127aa9ac18e1a39c89d2f25310cd1ca6' if args.batch==4 else 'be66ff165f4c01d523f9b24b7f6308f3f7143e4bc3f9d94ba21ab0a6e1ffc547'
    split='val' if args.batch==5 else 'train'
    if args.batch==5:
        expected='3a254d415fd0518c0ee0ccfaebf4fec616042e7ec8b6c6321935450aa1262bdb'
    assert sha(source/'manifest.json')==expected
    rows={r['original_id']:r for r in manifest['samples']}
    out=ROOT/f'Reports/beetle-confusers-v14-label-review-batch{args.batch}'
    out.mkdir(exist_ok=False);samples=[]
    for ident,boxes in recipes.items():
        row=rows[ident];path=source/row['image']
        assert row['reserved_split']==split and sha(path)==row['sha256']
        with Image.open(path) as original:
            frame=original.convert('RGB');w,h=frame.size
        lines=[];draw=ImageDraw.Draw(frame)
        for a,b,c,d in boxes:
            assert 0<=a<c<=w and 0<=b<d<=h
            lines.append(f'1 {(a+c)/2/w:.10f} {(b+d)/2/h:.10f} {(c-a)/w:.10f} {(d-b)/h:.10f}')
            draw.rectangle([a,b,c,d],outline='red',width=4)
        label=out/f'{ident}.txt';label.write_text('\n'.join(lines)+'\n')
        overlay=out/f'{ident}-overlay.jpg';frame.save(overlay)
        samples.append(dict(original_id=ident,source_sha256=sha(path),boxes_xyxy=boxes,
            class_id=1,class_name='Beetle',original_viewed=True,overlay_reviewed=False,
            label=label.relative_to(ROOT).as_posix(),label_sha256=sha(label),overlay_sha256=sha(overlay),
            reserved_split=split,training_approved=False))
    payload=dict(source_manifest_sha256=sha(source/'manifest.json'),samples=samples,
        proposed_names=['Cockroach','Beetle'],unity_mapping={'Cockroach':'ParkBugs','Beetle':None},
        annotation_convention='Visible body, legs and unfolded wings; exclude long antennae and shadows',
        limitations=['Publisher taxonomy, no independent expert biological verification',
            'Visible instance extent reviewed; original source provenance and licenses retained in manifest',
            'This batch is not a released dataset or a trained model'],
        training_approved=False,inference_performed=False)
    (out/'draft.json').write_text(json.dumps(payload,indent=2))
    for page in range((len(samples)+3)//4):
        canvas=Image.new('RGB',(1280,1000),'white');draw=ImageDraw.Draw(canvas)
        for index,row in enumerate(samples[page*4:page*4+4]):
            with Image.open(out/f'{row["original_id"]}-overlay.jpg') as frame:
                frame.thumbnail((620,460));x=index%2*640;y=index//2*500;canvas.paste(frame,(x,y))
            draw.text((x,y+465),row['original_id'],fill='black')
        canvas.save(out/f'contact-{page+1}.jpg')
    print(json.dumps(dict(images=len(samples),boxes=sum(len(r['boxes_xyxy']) for r in samples),training_approved=False)))


if __name__=='__main__':
    main()
