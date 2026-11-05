"""Apply three visually reviewed TRAIN-only figure-boundary corrections."""
import hashlib
import json
import shutil
from pathlib import Path
from dataset_paths import manifest_relative_path

ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
    parent=ROOT/'Datasets/statue-vr-negatives-v20'
    out=ROOT/'Datasets/statue-figure-labels-v23'
    review=ROOT/'Reports/statue-train-label-policy-v23/corrections-reviewed.json'
    d=read(review);a=read(parent/'audit.json')
    assert not out.exists()
    assert sha(parent/'audit.json')=='b42a570e527567225c760fe427201c74957ab1567e8dd1b69f66415222f5f328'
    assert d['training_approved'] and d['split']=='train'
    fixes={manifest_relative_path(s['image']).name:s for s in d['samples']}
    assert len(fixes)==3
    changed=[]
    for s in a['samples']:
        for k,digest in [('image','sha256'),('label','label_sha256')]:
            rel=manifest_relative_path(s[k])
            assert sha(parent/rel)==s[digest]
            p=out/rel;p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(parent/rel,p)
        key=manifest_relative_path(s['image']).name
        if key not in fixes:continue
        assert s['split']=='train'
        fix=fixes[key]
        assert fix['original_and_existing_overlay_reviewed'] and fix['new_overlay_reviewed']
        assert sha(ROOT/fix['new_overlay'])==fix['new_overlay_sha256']
        assert fix['label_sha256']==s['label_sha256'] and fix['image_sha256']==s['sha256']
        w,h=fix['dimensions'];lines=[]
        for x1,y1,x2,y2 in fix['new_boxes']:
            assert 0<=x1<x2<=w and 0<=y1<y2<=h
            lines.append(f'0 {(x1+x2)/(2*w):.8f} {(y1+y2)/(2*h):.8f} {(x2-x1)/w:.8f} {(y2-y1)/h:.8f}\n')
        old=s['label_sha256'];p=out/manifest_relative_path(s['label']);p.write_text(''.join(lines),encoding='utf-8')
        s.update(label_sha256=sha(p),review=review.relative_to(ROOT).as_posix(),annotation_status='TRAIN_FIGURE_BOUNDARY_CORRECTED')
        changed.append(dict(label=s['label'],old_sha256=old,new_sha256=sha(p),split=s['split']))
    assert len(changed)==3
    # Confirm all image bytes and every other label/fold stayed unchanged.
    old=read(parent/'audit.json')['samples']
    for before,after in zip(old,a['samples']):
        assert before['image']==after['image'] and before['split']==after['split'] and before['group']==after['group']
        assert sha(parent/manifest_relative_path(before['image']))==sha(out/manifest_relative_path(after['image']))
        if manifest_relative_path(before['image']).name not in fixes:
            assert sha(parent/manifest_relative_path(before['label']))==sha(out/manifest_relative_path(after['label']))
    a.update(parent_audit_sha256=sha(parent/'audit.json'),revision='Three TRAIN figure-boundary label corrections only; all86 photos/groups/folds and other83 labels unchanged. VAL and historical TEST unchanged.',
             label_corrections=changed,training_approved=True,deployment_accepted=False)
    a['context_evidence'].update({review.relative_to(ROOT).as_posix():sha(review)})
    (out/'audit.json').write_text(json.dumps(a,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='images/val',test='images/test',names=['Statue']),indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(status='PASS',photos=len(a['samples']),corrections=len(changed),audit_sha256=sha(out/'audit.json'))))
if __name__=='__main__':main()
