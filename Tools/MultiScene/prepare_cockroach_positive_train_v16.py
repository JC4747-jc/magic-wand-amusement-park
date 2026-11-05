"""Append 23 independently sourced reviewed TRAIN positives; preserve all folds."""
import hashlib
import json
import shutil
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def load(p):
    return json.loads(p.read_text())


def main():
    base=ROOT/'Datasets/cockroach-diverse-v15'
    source=ROOT/'Datasets/cockroach-positive-train-v16-source'
    out=ROOT/'Datasets/cockroach-positive-train-v16'
    assert not out.exists()
    assert sha(base/'audit.json')=='1e46824a3e2abf55ae0ec7f1530e79e9a9662956facde7c6256cdf12085255e3'
    assert sha(source/'manifest.json')=='4bb3b4c05f299df34331dd9e147b1250c3bf840f710f3ce350774fcad0eac0f8'
    parent=load(base/'audit.json');manifest=load(source/'manifest.json')
    pixel=load(ROOT/'Reports/cockroach-positive-train-v16-pixel-history.json')
    assert pixel['source_manifest_sha256']==sha(source/'manifest.json')
    assert not any(pixel[k] for k in ['errors','matches','internal_matches'])
    annotations=[];evidence={}
    for batch in [1,2]:
        p=ROOT/f'Reports/cockroach-positive-train-v16-label-review-batch{batch}/reviewed.json'
        review=load(p);assert review['source_manifest_sha256']==sha(source/'manifest.json')
        annotations+=review['samples'];evidence[p.relative_to(ROOT).as_posix()]=sha(p)
    hold_path=ROOT/'Reports/cockroach-positive-train-v16-holds.json';hold=load(hold_path)
    assert hold['source_manifest_sha256']==sha(source/'manifest.json')
    assert len(annotations)==23 and len(hold['samples'])==1
    rows={r['original_id']:r for r in manifest['samples']}
    assert {r['original_id'] for r in annotations}|{r['original_id'] for r in hold['samples']}==set(rows)
    groups={r['group'] for r in parent['samples']};samples=[]
    for row in parent['samples']:
        assert sha(base/row['image'])==row['sha256'] and sha(base/row['label'])==row['label_sha256']
        for key in ['image','label']:
            target=out/row[key];target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(base/row[key],target)
        samples.append(dict(row))
    for annotation in annotations:
        row=rows[annotation['original_id']]
        assert row['group'] not in groups;groups.add(row['group'])
        assert annotation['training_approved'] and annotation['original_viewed'] and annotation['overlay_reviewed']
        assert row['reserved_split']==annotation['reserved_split']=='train'
        assert sha(source/row['image'])==row['sha256']==annotation['source_sha256']
        label=ROOT/annotation['label'];assert sha(label)==annotation['label_sha256']
        assert len(label.read_text().splitlines())==1 and label.read_text().split()[0]=='0'
        stem=Path(row['image']).stem;image_path=f'images/train/v16-positive-{stem}.jpg';label_path=f'labels/train/v16-positive-{stem}.txt'
        shutil.copy2(source/row['image'],out/image_path);shutil.copy2(label,out/label_path)
        samples.append(dict(row,image=image_path,label=label_path,label_sha256=sha(label),split='train',
            boxes_xyxy=annotation['boxes_xyxy'],training_approved=True,evaluation_approved=False))
    counts={s:sum(r['split']==s for r in samples) for s in ['train','val','test']}
    assert counts==dict(train=200,val=39,test=9)
    audit=dict(parent,samples=samples,unique_images=len(samples),counts=counts,
        parent_audit_sha256=sha(base/'audit.json'),annotation_reviews=evidence,
        source_manifest_sha256=sha(source/'manifest.json'),source_holds=hold['samples'],
        class_instance_counts=dict(train={'0':121,'1':38},val={'0':29,'1':13},test={'0':9}),
        split_policy='225 parent image/label pairs unchanged;23new TRAIN photos23GT;1source taxonomy HOLD;no fold reassignment',
        training_approved=True)
    (out/'audit.json').write_text(json.dumps(audit,indent=2))
    (out/'data.yaml').write_text(json.dumps(dict(path=out.as_posix(),train='images/train',val='images/val',test='images/test',names=['Cockroach','Beetle']),indent=2))
    report=dict(status='PASS',counts=counts,audit_sha256=sha(out/'audit.json'),added_images=23,added_instances=23,held_images=1,parent_pairs_unchanged=225,service_routed=False)
    (ROOT/'Reports/cockroach-positive-train-v16-assembly.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))


if __name__=='__main__':
    main()
