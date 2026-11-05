"""Append pre-reserved reviewed beetle negatives without changing existing folds."""
import hashlib
import json
import shutil
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def load(p):
    return json.loads(p.read_text(encoding='utf-8'))


def main():
    base=ROOT/'Datasets/cockroach-expanded-v5';out=ROOT/'Datasets/cockroach-confusers-v7'
    assert not out.exists()
    assert sha(base/'audit.json')=='d5ca262af780deba4969794c8ca60ca6eb2e9d07047e3e02e11a66c821eaf572'
    parent=load(base/'audit.json');samples=[];groups={r['group'] for r in parent['samples']};bindings={}
    additions=[]
    for fold,count in [('train',24),('val',12)]:
        source=ROOT/f'Datasets/cockroach-beetle-{fold}-v7-source'
        review_path=ROOT/f'Reports/cockroach-beetle-{fold}-v7-review.json'
        review=load(review_path);manifest=load(source/'manifest.json')
        assert review['review_approved'] and review['source_manifest_sha256']==sha(source/'manifest.json')
        pixel=load(ROOT/f'Reports/cockroach-beetle-{fold}-v7-pixel-history.json')
        assert pixel['source_manifest_sha256']==sha(source/'manifest.json')
        assert not any(pixel[k] for k in ['errors','matches','internal_matches'])
        rows={r['original_id']:r for r in manifest['samples']};assert len(rows)==len(review['samples'])==count
        bindings[review_path.relative_to(ROOT).as_posix()]=sha(review_path)
        for annotation in review['samples']:
            row=rows[annotation['original_id']]
            assert row['group'] not in groups;groups.add(row['group'])
            assert annotation['original_viewed'] and not annotation['boxes_xyxy']
            assert annotation['annotation_status']=='REVIEWED_NO_VISIBLE_COCKROACH'
            assert row['reserved_split']==annotation['reserved_split']==fold
            assert sha(source/row['image'])==row['sha256']==annotation['image_sha256']
            additions.append((fold,source,row))
    for row in parent['samples']:
        assert sha(base/row['image'])==row['sha256'] and sha(base/row['label'])==row['label_sha256']
        for key in ['image','label']:
            target=out/row[key];target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(base/row[key],target)
        samples.append(dict(row))
    for fold,source,row in additions:
        stem=Path(row['image']).stem
        image=f'images/{fold}/beetle-{stem}.jpg';label=f'labels/{fold}/beetle-{stem}.txt'
        shutil.copy2(source/row['image'],out/image);(out/label).write_bytes(b'')
        samples.append(dict(row,image=image,label=label,label_sha256=sha(out/label),split=fold,
            negative=True,original_reviewed=True,training_approved=fold=='train',evaluation_approved=True))
    counts={s:sum(r['split']==s for r in samples) for s in ['train','val','test']}
    assert counts==dict(train=121,val=20,test=9)
    audit=dict(parent,samples=samples,counts=counts,unique_images=len(samples),parent_audit_sha256=sha(base/'audit.json'),
        review_bindings=bindings,split_policy='114 parent folds unchanged; 24 new TRAIN and12 new VAL beetle photos; no v6 TEST reuse',
        limitations=parent['limitations']+['New negative VAL cohort is development data; fresh independent acceptance still needed'])
    (out/'audit.json').write_text(json.dumps(audit,indent=2))
    (out/'data.yaml').write_text(json.dumps(dict(path=out.as_posix(),train='images/train',val='images/val',test='images/test',names=['Cockroach']),indent=2))
    for row in samples:
        assert sha(out/row['image'])==row['sha256'] and sha(out/row['label'])==row['label_sha256']
    report=dict(status='PASS',counts=counts,parent_pairs_unchanged=114,new_train_negative=24,new_val_negative=12,
        audit_sha256=sha(out/'audit.json'),test_reassigned=False)
    (ROOT/'Reports/cockroach-confusers-v7-assembly.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))


if __name__=='__main__':
    main()
