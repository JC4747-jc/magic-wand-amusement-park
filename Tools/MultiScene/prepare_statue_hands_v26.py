"""Append two reviewed public real-hand TRAIN negatives; preserve all inherited folds."""
import hashlib
import json
import shutil
from pathlib import Path
from collections import Counter
from dataset_paths import manifest_relative_path as rel

ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
    parent=ROOT/'Datasets/statue-groups-v25'
    source=ROOT/'Datasets/statue-hand-train-source-v26'
    out=ROOT/'Datasets/statue-hands-v26'
    review=ROOT/'Reports/statue-hand-train-v26-empty-label-review.json'
    assert not out.exists()
    assert sha(parent/'audit.json')=='d22daf1ab9afaf19ac7a6e74a9b2c5964111d3a54e1c62e92a7a157098072562'
    a=read(parent/'audit.json');d=read(review);m=read(source/'manifest.json')
    assert d['training_approved'] and d['split']=='train'
    assert d['source_manifest_sha256']==sha(source/'manifest.json')
    for item in d['inputs']:assert sha(ROOT/rel(item['path']))==item['sha256']
    ids=read(ROOT/'Reports/statue-hand-train-v26-identity-history.json')
    cr=read(ROOT/'Reports/statue-hand-train-v26-creator-links.json')
    px=read(ROOT/'Reports/statue-hand-train-v26-pixel-history.json')
    assert not ids['errors'] and not cr['collisions']
    assert not px['errors'] and not px['matches'] and not px['internal_matches']
    assert px['scanned_paths']==px['enumerated_paths']
    assert ids['source_manifest_sha256']==px['source_manifest_sha256']==cr['source_sha256']==sha(source/'manifest.json')
    reservation=read(source/'train-reservation.json')
    assert reservation['reserved_split']=='train' and reservation['source_manifest_sha256']==sha(source/'manifest.json')
    assert {x['page_id'] for x in d['samples']}=={151301401,133904055}
    old=list(a['samples'])
    for s in old:
        for k,digest in [('image','sha256'),('label','label_sha256')]:
            assert sha(parent/rel(s[k]))==s[digest]
            p=out/rel(s[k]);p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(parent/rel(s[k]),p)
    for b in d['samples']:
        index,s=next((i,s) for i,s in enumerate(m['samples']) if s['page_id']==b['page_id'])
        assert index not in cr['missing_creator_link_indices']
        assert not next(x for x in ids['rows'] if x['page_id']==s['page_id'])['historical_hits']
        assert b['original_actually_viewed'] and b['physical_statue_instances']==0 and b['empty_label_approved'] and b['publisher_linkage_verified']
        assert sha(source/rel(s['image']))==b['image_sha256']==s['sha256']
        assert s['license']=='CC BY-SA 4.0' and s['license_url']=='https://creativecommons.org/licenses/by-sa/4.0'
        assert all(s['group']!=x['group'] and s['sha256']!=x['sha256'] for x in old)
        image=out/'images/train'/rel(s['image']).name;label=out/'labels/train'/(image.stem+'.txt')
        shutil.copy2(source/rel(s['image']),image);label.write_text('',encoding='utf-8')
        a['samples'].append(dict(s,image=image.relative_to(out).as_posix(),label=label.relative_to(out).as_posix(),label_sha256=sha(label),split='train',training_approved=True,annotation_status='REAL_HAND_NEGATIVE_VISUALLY_REVIEWED',review=review.relative_to(ROOT).as_posix()))
    for s in old:
        for k in ['image','label']:assert sha(parent/rel(s[k]))==sha(out/rel(s[k]))
    a.update(parent_audit_sha256=sha(parent/'audit.json'),unique_images=len(a['samples']),split_counts=dict(Counter(s['split'] for s in a['samples'])),revision='Two independent real-hand TRAIN negatives; all88 inherited image/label/group/fold bytes unchanged.',training_approved=True,deployment_accepted=False)
    a['context_evidence'].update({review.relative_to(ROOT).as_posix():sha(review),**{x['path']:x['sha256'] for x in d['inputs']}})
    a['sources'].append(dict(id='commons-statue-hand-negatives-v26',url='https://commons.wikimedia.org/wiki/Category:Male_hands',license='CC BY-SA4, per-image author and license retained; ShareAlike applies to distributed adaptations',reviewed=True))
    (out/'audit.json').write_text(json.dumps(a,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='images/val',test='images/test',names=['Statue']),indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(status='PASS',photos=len(a['samples']),splits=a['split_counts'],new_train_negatives=2,audit_sha256=sha(out/'audit.json'))))
if __name__=='__main__':main()
