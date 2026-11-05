"""Expand VAL positives without changing TRAIN/TEST or using model predictions for labels."""
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
    base=ROOT/'Datasets/cockroach-longhorn-v8'
    source=ROOT/'Datasets/cockroach-positive-val-v10-source'
    out=ROOT/'Datasets/cockroach-positive-val-v10'
    assert not out.exists()
    assert sha(base/'audit.json')=='b4914edf7afe58ea50014bee863dcaee3bfd915cfb202470697e03ddc4ec4935'
    parent=load(base/'audit.json');manifest=load(source/'manifest.json')
    review_path=ROOT/'Reports/cockroach-positive-val-v10-label-review/reviewed.json';review=load(review_path)
    assert review['evaluation_approved'] and not review['training_approved']
    assert review['source_manifest_sha256']==sha(source/'manifest.json')
    pixel=load(ROOT/'Reports/cockroach-positive-val-v10-pixel-history.json')
    assert pixel['source_manifest_sha256']==sha(source/'manifest.json')
    assert not any(pixel[k] for k in ['errors','matches','internal_matches'])
    rows={r['original_id']:r for r in manifest['samples']};assert len(rows)==20
    assert {r['original_id'] for r in review['samples']}==set(rows)-{'393617508'}
    assert len(review['samples'])==19 and sum(len(r['boxes_xyxy']) for r in review['samples'])==21
    groups={r['group'] for r in parent['samples']};samples=[]
    for row in parent['samples']:
        assert sha(base/row['image'])==row['sha256'] and sha(base/row['label'])==row['label_sha256']
        for key in ['image','label']:
            p=out/row[key];p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(base/row[key],p)
        samples.append(dict(row))
    for annotation in review['samples']:
        row=rows[annotation['original_id']];assert row['group'] not in groups;groups.add(row['group'])
        assert row['reserved_split']=='val' and annotation['reserved_split']=='val'
        assert annotation['original_viewed'] and annotation['overlay_reviewed']
        assert sha(source/row['image'])==row['sha256']==annotation['source_sha256']
        label=ROOT/annotation['label'];assert sha(label)==annotation['label_sha256']
        stem=Path(row['image']).stem;image_path=f'images/val/new-positive-{stem}.jpg';label_path=f'labels/val/new-positive-{stem}.txt'
        shutil.copy2(source/row['image'],out/image_path);shutil.copy2(label,out/label_path)
        samples.append(dict(row,image=image_path,label=label_path,label_sha256=sha(label),split='val',
            boxes_xyxy=annotation['boxes_xyxy'],evaluation_approved=True,training_approved=False))
    counts={s:sum(r['split']==s for r in samples) for s in ['train','val','test']}
    assert counts==dict(train=137,val=39,test=9)
    audit=dict(parent,samples=samples,unique_images=len(samples),counts=counts,
        parent_audit_sha256=sha(base/'audit.json'),positive_review_sha256=sha(review_path),
        source_manifest_sha256=sha(source/'manifest.json'),
        split_policy='166 parent folds/payloads unchanged;19 new VAL photos21GT;393617508 held before inference; no TEST reassignment')
    (out/'audit.json').write_text(json.dumps(audit,indent=2))
    (out/'data.yaml').write_text(json.dumps(dict(path=out.as_posix(),train='images/train',val='images/val',test='images/test',names=['Cockroach']),indent=2))
    report=dict(status='PASS',counts=counts,audit_sha256=sha(out/'audit.json'),parent_pairs_unchanged=166,
        added_val_images=19,added_val_instances=21,held_val_images=1,test_reassigned=False)
    (ROOT/'Reports/cockroach-positive-val-v10-assembly.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))


if __name__=='__main__':
    main()
