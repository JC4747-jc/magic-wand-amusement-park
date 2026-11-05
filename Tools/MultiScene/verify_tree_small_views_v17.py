"""Independently reconstruct generated TRAIN pixels/coordinates and original VAL payloads."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    base=ROOT/'Datasets/tree-windswept-positives-v11'
    data=ROOT/'Datasets/tree-small-views-v17'
    audit=json.loads((data/'audit.json').read_text(encoding='utf-8'))
    original=json.loads((base/'audit.json').read_text(encoding='utf-8'))
    parents={r['image']:r for r in original['samples']}
    derived=[];unchanged=[]
    for row in audit['samples']:
        assert sha(data/row['image'])==row['sha256'] and sha(data/row['label'])==row['label_sha256']
        if row.get('independent_sample') is not False:
            parent=parents[row['image']]
            assert sha(data/row['image'])==sha(base/parent['image'])
            assert sha(data/row['label'])==sha(base/parent['label'])
            unchanged.append(row)
            continue
        parent=parents[row['original_image']]
        assert parent['split']==row['split']=='train' and parent['group']==row['group']
        with Image.open(base/parent['image']) as image:
            image=image.convert('RGB');ratio=640*row['requested_scale']/max(image.size)
            size=tuple(max(1,round(v*ratio)) for v in image.size)
            offset=tuple((640-v)//2 for v in size)
            expected=Image.new('RGB',(640,640),(114,114,114))
            expected.paste(image.resize(size,Image.Resampling.BILINEAR),offset)
        with Image.open(data/row['image']) as actual:
            assert actual.size==(640,640) and expected.tobytes()==actual.convert('RGB').tobytes()
        before=(base/parent['label']).read_text().splitlines()
        after=(data/row['label']).read_text().splitlines()
        assert len(before)==len(after)
        draw=ImageDraw.Draw(expected)
        for source_line,target_line in zip(before,after):
            c,x,y,w,h=map(float,source_line.split());tc,tx,ty,tw,th=map(float,target_line.split())
            values=[(offset[0]+x*size[0])/640,(offset[1]+y*size[1])/640,w*size[0]/640,h*size[1]/640]
            assert c==tc==0 and all(abs(a-b)<1e-9 for a,b in zip(values,[tx,ty,tw,th]))
            assert tw>0 and th>0 and min(tx-tw/2,ty-th/2)>=-1e-8 and max(tx+tw/2,ty+th/2)<=1+1e-8
            draw.rectangle(((tx-tw/2)*640,(ty-th/2)*640,(tx+tw/2)*640,(ty+th/2)*640),outline='red',width=3)
        derived.append((row,expected))
    assert len(unchanged)==48 and len(derived)==54
    assert sum(r['split']=='val' for r in unchanged)==11
    for page in range(2):
        contact=Image.new('RGB',(1080,1080),'white');draw=ImageDraw.Draw(contact)
        for i,(row,frame) in enumerate(derived[page*27:(page+1)*27]):
            x=(i%6)*180;y=(i//6)*210
            contact.paste(frame.resize((175,175)),(x,y))
            draw.text((x,y+177),Path(row['image']).stem[-19:],fill='black')
        contact.save(ROOT/f'Reports/tree-small-views-v17-overlays-{page+1}.jpg')
    report=dict(status='PAYLOAD_REPRODUCTION_PASS_PENDING_VISUAL_REVIEW',derived=54,unchanged=48,unchanged_val=11,
                dataset_sha256=sha(data/'audit.json'),independent_new_photos=0,test_accessed=False)
    (ROOT/'Reports/tree-small-views-v17-verification.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report))


if __name__=='__main__':
    main()
