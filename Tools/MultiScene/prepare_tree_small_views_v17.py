"""Deterministic whole-frame TRAIN shrink augmentation; never independent new evidence."""
import hashlib
import json
import shutil
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    source=ROOT/'Datasets/tree-windswept-positives-v11'
    assert sha(source/'audit.json')=='9196b6a2fa8c22d34d808a3e1a7f903f4275121950c837bcabeab794514782fb'
    audit=json.loads((source/'audit.json').read_text(encoding='utf-8'))
    out=ROOT/'Datasets/tree-small-views-v17'
    if out.exists():
        raise FileExistsError(out)
    for row in audit['samples']:
        assert sha(source/row['image'])==row['sha256']
        assert sha(source/row['label'])==row['label_sha256']
    samples=[]
    for row in audit['samples']:
        for key in ['image','label']:
            dest=out/row[key];dest.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(source/row[key],dest)
        samples.append(dict(row))
        lines=(source/row['label']).read_text().splitlines()
        if row['split']!='train' or not lines:
            continue
        with Image.open(source/row['image']) as frame:
            frame=frame.convert('RGB')
            for scale in [.25,.5]:
                ratio=640*scale/max(frame.size)
                width=max(1,round(frame.width*ratio));height=max(1,round(frame.height*ratio))
                left=(640-width)//2;top=(640-height)//2
                canvas=Image.new('RGB',(640,640),(114,114,114))
                canvas.paste(frame.resize((width,height),Image.Resampling.BILINEAR),(left,top))
                name=Path(row['image']).stem+f'-small{int(scale*100)}'
                image=f'images/train/{name}.png';label=f'labels/train/{name}.txt'
                canvas.save(out/image)
                transformed=[]
                for line in lines:
                    cls,x,y,w,h=map(float,line.split())
                    transformed.append(f'{int(cls)} {(left+x*width)/640:.10f} {(top+y*height)/640:.10f} {w*width/640:.10f} {h*height/640:.10f}')
                (out/label).write_text('\n'.join(transformed)+'\n')
                samples.append(dict(row,image=image,label=label,sha256=sha(out/image),label_sha256=sha(out/label),
                    original_decoded_rgb_sha256=row.get('decoded_rgb_sha256'),
                    decoded_rgb_sha256=hashlib.sha256(b'RGB:640:640:'+canvas.tobytes()).hexdigest(),dimensions=[640,640],
                    original_image=row['image'],original_sha256=row['sha256'],augmentation='whole_frame_shrink_with_gray_padding',
                    requested_scale=scale,rendered_size=[width,height],offset=[left,top],independent_sample=False))
    result=dict(audit,samples=samples,parent_audit_sha256=sha(source/'audit.json'),
                training_approved=False,evaluation_approved=False,status='GENERATED_PENDING_REPRODUCTION_AND_VISUAL_REVIEW',
                counts=dict(train=sum(r['split']=='train' for r in samples),val=sum(r['split']=='val' for r in samples)),
                limitations=['Derived TRAIN views share original author/session groups; not new independent examples',
                             'Gray-padding artifacts and synthetic scale distribution may limit real-domain improvement',
                             'All 11 VAL payloads unchanged; TEST not accessed'])
    (out/'audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    (out/'data.yaml').write_text(json.dumps(dict(path=out.as_posix(),train='images/train',val='images/val',names=['Tree']),indent=2))
    print(json.dumps(dict(samples=len(samples),derived=len(samples)-len(audit['samples']),audit_sha256=sha(out/'audit.json'))))


if __name__=='__main__':
    main()
