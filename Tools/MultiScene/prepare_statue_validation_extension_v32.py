"""Build a held-for-review VAL extension without changing inherited TRAIN/TEST."""
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
    parent=ROOT/'Datasets/statue-hands-v26'
    source=ROOT/'Datasets/statue-validation-source-v29'
    out=ROOT/'Datasets/statue-validation-extension-v32'
    review=ROOT/'Reports/statue-validation-v29-boxes/reviewed-twelve.json'
    assert not out.exists()
    assert sha(parent/'audit.json')=='5c7a826d47ce7873f7b723a0ada898e5a67abdfb43a48be7330b79b7ad2d35f4'
    a=read(parent/'audit.json');b=read(review);m=read(source/'manifest.json')
    assert b['annotation_approved'] and not b['model_inference_performed']
    for x in b['evidence']:assert sha(ROOT/rel(x['path']))==x['sha256']
    reservation=read(source/'val-reservation.json')
    assert reservation['reserved_split']=='val' and reservation['source_manifest_sha256']==sha(source/'manifest.json')
    px=read(ROOT/'Reports/statue-validation-v29-pixel-history.json')
    cr=read(ROOT/'Reports/statue-validation-v29-creator-links.json')
    ids=read(ROOT/'Reports/statue-validation-v29-identity-history.json')
    assert not px['errors'] and not px['matches'] and not px['internal_matches']
    assert px['scanned_paths']==px['enumerated_paths'] and not cr['collisions'] and not ids['errors']
    assert px['source_manifest_sha256']==ids['source_manifest_sha256']==cr['source_sha256']==sha(source/'manifest.json')
    selected={s['page_id']:s for s in b['samples']}
    assert len(selected)==12
    resolution=read(ROOT/'Reports/statue-validation-v29-identity-resolutions.json')
    assert resolution['raw_history_sha256']==sha(ROOT/'Reports/statue-validation-v29-identity-history.json')
    old=list(a['samples'])
    for s in old:
        for k,h in [('image','sha256'),('label','label_sha256')]:
            p=parent/rel(s[k]);assert sha(p)==s[h]
            q=out/rel(s[k]);q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q)
    held=[]
    for i,s in enumerate(m['samples']):
        if s['page_id'] not in selected:
            held.append(dict(s,reserved_split='val',status='IDENTITY_OR_DOMAIN_REVIEW_HOLD',dataset_adopted=False))
            continue
        b=selected[s['page_id']]
        assert b['original_reviewed'] and b['overlay_reviewed']
        if i in cr['missing_creator_link_indices']:
            assert (s['page_id']==46776234 and 'Photo Mark Ostrander' in s['publisher_metadata']['Credit']['value']) or (s['page_id']==115921045 and b.get('creator_group_override')=='commons:institution:city-of-edinburgh-council')
        assert sha(ROOT/rel(b['overlay']))==b['overlay_sha256']
        assert sha(source/rel(s['image']))==b['sha256']==s['sha256']
        hits=next(x for x in ids['rows'] if x['page_id']==s['page_id'])['historical_hits']
        if hits:assert s['page_id']==18393147 and resolution['identity_resolution']['page_id']==18393147
        assert all(s['group']!=x['group'] and s['sha256']!=x['sha256'] for x in old)
        image=out/'images/val'/rel(s['image']).name;label=out/'labels/val'/(image.stem+'.txt')
        shutil.copy2(source/rel(s['image']),image);w,h=b['dimensions'];lines=[]
        for x1,y1,x2,y2 in b['boxes']:
            assert 0<=x1<x2<=w and 0<=y1<y2<=h
            lines.append(f'0 {(x1+x2)/(2*w):.8f} {(y1+y2)/(2*h):.8f} {(x2-x1)/w:.8f} {(y2-y1)/h:.8f}\n')
        label.write_text(''.join(lines),encoding='utf-8')
        a['samples'].append(dict(s,image=image.relative_to(out).as_posix(),label=label.relative_to(out).as_posix(),label_sha256=sha(label),split='val',group=b.get('creator_group_override',s['group']),annotation_status='ORIGINAL_OVERLAY_REVIEWED',training_approved=False,review=review.relative_to(ROOT).as_posix()))
    for s in old:
        for k in ['image','label']:assert sha(parent/rel(s[k]))==sha(out/rel(s[k]))
    assert len(held)==0
    a.update(parent_audit_sha256=sha(parent/'audit.json'),unique_images=len(a['samples']),split_counts=dict(Counter(s['split'] for s in a['samples'])),held_samples=held,training_approved=True,deployment_accepted=False,revision='All12 new VAL photos reviewed before inference:12physicalfigure instances and1flat-cutout negative photo. All90 inherited image/label/group/fold bytes unchanged. VAL annotations released; model deployment not accepted.')
    a['validation_release_scope']='12newVALphotos,12figureGTand1negative;7priorVAL21GT preserved. Unknown individual archive/institution photographers grouped conservatively by source.'
    a['sources'].append(dict(id='commons-statue-validation-v29',url='https://commons.wikimedia.org/wiki/Category:Statues_of_men',license='Per-photo recorded CC licenses; photographer and sculptor roles kept separate',reviewed=True))
    a['context_evidence'].update({review.relative_to(ROOT).as_posix():sha(review),**{x['path']:x['sha256'] for x in read(review)['evidence']}})
    (out/'audit.json').write_text(json.dumps(a,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='images/val',test='images/test',names=['Statue']),indent=2)+'\n',encoding='utf-8')
    (out/'annotation_release.json').write_text(json.dumps(dict(status='VAL_ANNOTATIONS_RELEASED_NO_MODEL_ACCEPTANCE',dataset_audit_sha256=sha(out/'audit.json'),source_photos=12,positive_instances=12,negative_images=1,training_photos_unchanged=80,prior_test_unchanged=True,model_inference_performed=False,review_sha256=sha(review),limitations='Single assistant annotations and recorded source-group checks; no PICO target-positive validation or proof of pretraining independence.'),indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(status='ANNOTATIONS_RELEASED',images=len(a['samples']),splits=a['split_counts'],held=0,audit_sha256=sha(out/'audit.json'))))
if __name__=='__main__':main()
