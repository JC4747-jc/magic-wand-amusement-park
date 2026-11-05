"""Append 39 pre-reserved TRAIN photos; retain every existing fold and payload."""
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
    base=ROOT/'Datasets/inaturalist-cockroach-expanded-v3'
    source=ROOT/'Datasets/cockroach-training-expansion-v5-source'
    out=ROOT/'Datasets/cockroach-expanded-v5'
    assert sha(base/'audit.json')=='ab24a4c016a24a1f71c471e33e9ac3b7fe5b7a3f56b9c3c148975ec0dc11d700'
    parent=load(base/'audit.json');manifest=load(source/'manifest.json')
    if out.exists():
        expected={r[key]:base/r[key] for r in parent['samples'] for key in ['image','label']}
        for path in out.rglob('*'):
            if path.is_file():
                relative=path.relative_to(out).as_posix()
                if relative not in expected or sha(path)!=sha(expected[relative]):
                    raise FileExistsError('Only verified identical partial parent copies may be recovered')
    rows={r['original_id']:r for r in manifest['samples']}
    original_groups={r['group'] for r in parent['samples']}
    pixel=load(ROOT/'Reports/cockroach-training-v5-pixel-history.json')
    assert pixel['source_manifest_sha256']==sha(source/'manifest.json')
    assert not any(pixel[k] for k in ['errors','matches','internal_matches'])
    additions=[];bindings={}
    for batch in range(1,5):
        review_path=ROOT/f'Reports/cockroach-training-v5-label-review-batch{batch}/reviewed.json'
        review=load(review_path);bindings[review_path.relative_to(ROOT).as_posix()]=sha(review_path)
        assert review['source_manifest_sha256']==sha(source/'manifest.json')
        for annotation in review['samples']:
            row=rows[annotation['original_id']]
            assert row['group'] not in original_groups and row['reserved_split']=='train'
            assert annotation['original_viewed'] and annotation['overlay_reviewed']
            assert sha(source/row['image'])==row['sha256']==annotation['source_sha256']
            label=ROOT/annotation['label'];assert sha(label)==annotation['label_sha256']
            additions.append((row,annotation,label))
    assert len(additions)==39 and len({r['group'] for r,_,_ in additions})==39
    assert not any(r['original_id']=='406783444' for r,_,_ in additions)
    samples=[]
    for row in parent['samples']:
        assert sha(base/row['image'])==row['sha256']
        label_digest=row.get('label_sha256')
        if label_digest is None:
            assert row.get('negative') is True and not (base/row['label']).read_bytes().strip()
            label_digest=sha(base/row['label'])
        assert sha(base/row['label'])==label_digest
        for key in ['image','label']:
            dest=out/row[key];dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(base/row[key],dest)
        samples.append(dict(row,label_sha256=label_digest))
    for row,annotation,label in additions:
        stem=Path(row['image']).stem
        image_dest=f'images/train/new-{stem}.jpg';label_dest=f'labels/train/new-{stem}.txt'
        shutil.copy2(source/row['image'],out/image_dest);shutil.copy2(label,out/label_dest)
        samples.append(dict(row,image=image_dest,label=label_dest,label_sha256=sha(label),split='train',
            training_approved=True,original_reviewed=True,overlay_reviewed=True,boxes_xyxy=annotation['boxes_xyxy']))
    counts={split:sum(r['split']==split for r in samples) for split in ['train','val','test']}
    for row in samples:
        assert sha(out/row['image'])==row['sha256'] and sha(out/row['label'])==row['label_sha256']
    audit=dict(parent,samples=samples,unique_images=len(samples),counts=counts,
        parent_audit_sha256=sha(base/'audit.json'),source_manifest_sha256=sha(source/'manifest.json'),
        review_bindings=bindings,training_approved=True,split_policy='Existing75 folds/payloads unchanged;39 new observer photos TRAIN-only; held406783444 excluded',
        limitations=parent['limitations']+['39 new training photos do not add independent validation/test evidence','Research-grade publisher species labels, not independent expert verification'])
    (out/'audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
    (out/'data.yaml').write_text(json.dumps(dict(path=out.as_posix(),train='images/train',val='images/val',test='images/test',names=['Cockroach']),indent=2))
    report=dict(status='PASS',original_pairs_unchanged=75,new_train_images=39,new_train_boxes=40,counts=counts,
        source_group_overlap=0,audit_sha256=sha(out/'audit.json'),test_inference_performed=False)
    (ROOT/'Reports/cockroach-expanded-v5-assembly.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))


if __name__=='__main__':
    main()
