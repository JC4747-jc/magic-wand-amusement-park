"""Append two complete reviewed TRAIN group photos, preserve inherited bytes/folds."""
import hashlib
import json
import shutil
from collections import Counter
from pathlib import Path
from dataset_paths import manifest_relative_path as rel

ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
    parent=ROOT/'Datasets/statue-figure-labels-v23'
    source=ROOT/'Datasets/statue-group-train-source-v25'
    out=ROOT/'Datasets/statue-groups-v25'
    review=ROOT/'Reports/statue-group-train-v25-boxes/reviewed.json'
    assert not out.exists()
    assert sha(parent/'audit.json')=='281fc8b941cdbbc24e12d08cc6539278777a0618c46285af7374af087076b775'
    d=read(review);m=read(source/'manifest.json');a=read(parent/'audit.json')
    assert d['source_manifest_sha256']==sha(source/'manifest.json') and d['training_approved']
    identity=ROOT/'Reports/statue-group-train-v25-identity-history.json'
    creators=ROOT/'Reports/statue-group-train-v25-creator-links.json'
    pixels=ROOT/'Reports/statue-group-train-v25-pixel-history.json'
    ids,cr,px=map(read,[identity,creators,pixels])
    assert not ids['errors'] and ids['source_manifest_sha256']==sha(source/'manifest.json')
    assert cr['source_sha256']==sha(source/'manifest.json') and not cr['collisions'] and not cr['missing_creator_link_indices']
    assert px['source_manifest_sha256']==sha(source/'manifest.json') and not px['errors'] and not px['matches'] and not px['internal_matches']
    assert px['scanned_paths']==px['enumerated_paths']
    assert read(source/'train-reservation.json')['reserved_split']=='train'
    samples={s['page_id']:s for s in m['samples']}
    assert {s['page_id'] for s in d['samples']}=={185483057,66956474}
    old=list(a['samples'])
    for s in old:
        for k,digest in [('image','sha256'),('label','label_sha256')]:
            assert sha(parent/rel(s[k]))==s[digest]
            p=out/rel(s[k]);p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(parent/rel(s[k]),p)
    for b in d['samples']:
        s=samples[b['page_id']]
        assert b['original_reviewed'] and b['overlay_reviewed'] and b['training_approved']
        assert b['status']=='ORIGINAL_AND_OVERLAY_REVIEWED_BOX_APPROVED'
        assert sha(ROOT/rel(b['overlay']))==b['overlay_sha256']
        assert sha(source/rel(s['image']))==b['sha256']==s['sha256']
        assert not next(x for x in ids['rows'] if x['page_id']==s['page_id'])['historical_hits']
        assert s['license'] in ['CC BY 2.0','CC BY 3.0','CC BY 4.0','CC BY-SA 4.0','CC0'] and s['license_url']
        assert all(s['group']!=x['group'] and s['sha256']!=x['sha256'] for x in old)
        image=out/'images/train'/Path(s['image']).name
        label=out/'labels/train'/(image.stem+'.txt')
        shutil.copy2(source/rel(s['image']),image)
        w,h=b['dimensions'];lines=[]
        for x1,y1,x2,y2 in b['boxes']:
            assert 0<=x1<x2<=w and 0<=y1<y2<=h
            lines.append(f'0 {(x1+x2)/(2*w):.8f} {(y1+y2)/(2*h):.8f} {(x2-x1)/w:.8f} {(y2-y1)/h:.8f}\n')
        label.write_text(''.join(lines),encoding='utf-8')
        a['samples'].append(dict(s,image=image.relative_to(out).as_posix(),label=label.relative_to(out).as_posix(),label_sha256=sha(label),split='train',training_approved=True,annotation_status='REVIEWED_COMPLETE_VISIBLE_FIGURES',review=review.relative_to(ROOT).as_posix()))
    for s in old:
        for k in ['image','label']:assert sha(parent/rel(s[k]))==sha(out/rel(s[k]))
    a.update(parent_audit_sha256=sha(parent/'audit.json'),unique_images=len(a['samples']),split_counts=dict(Counter(s['split'] for s in a['samples'])),revision='Two new TRAIN group photos, six figure boxes; all86 inherited image/label/group/fold bytes unchanged.',deployment_accepted=False)
    a['context_evidence'].update({p.relative_to(ROOT).as_posix():sha(p) for p in [review,source/'manifest.json',source/'train-reservation.json',identity,creators,pixels]})
    a['sources'].append(dict(id='commons-statue-groups-v25',url='https://commons.wikimedia.org/wiki/Category:Sculptural_groups',license='Per-photo CC BY / CC BY-SA metadata; retain individual attribution and ShareAlike',reviewed=True))
    (out/'audit.json').write_text(json.dumps(a,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='images/val',test='images/test',names=['Statue']),indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(status='PASS',photos=len(a['samples']),splits=a['split_counts'],new_photos=2,new_GT=6,audit_sha256=sha(out/'audit.json'))))
if __name__=='__main__':main()
