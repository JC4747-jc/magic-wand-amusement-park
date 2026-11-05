"""Assemble reviewed confuser labels without changing folds or Cockroach labels."""
import hashlib
import json
import shutil
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    base = ROOT / 'Datasets/cockroach-positive-train-v11'
    out = ROOT / 'Datasets/cockroach-beetle-v14'
    assert not out.exists()
    assert sha(base / 'audit.json') == '7a9ad7fc58abf6de5ed9c9f7ef72fbae37eb374a06a40dc0cd0693a590a73837'
    parent = json.loads((base / 'audit.json').read_text())
    annotations, reviews = {}, []
    for batch in range(1, 6):
        path = ROOT / f'Reports/beetle-confusers-v14-label-review-batch{batch}/reviewed.json'
        review = json.loads(path.read_text())
        reviews.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
        for row in review['samples']:
            if not row.get('training_approved') and not row.get('evaluation_approved'):
                continue
            assert row['original_viewed'] and row['overlay_reviewed']
            assert row['original_id'] not in annotations
            annotations[row['original_id']] = row
    assert len(annotations) == 50
    held = {'412872917': 'HOLD_INSTANCE_EXTENT', '412835378': 'HOLD_INSTANCE_COUNT'}
    samples, exclusions, pending = [], [], []
    for old in parent['samples']:
        image, label = base / old['image'], base / old['label']
        assert sha(image) == old['sha256'] and sha(label) == old['label_sha256']
        ident = str(old.get('original_id', ''))
        if ident in held:
            assert old['split'] == 'train' and not label.read_text().strip()
            exclusions.append(dict(original_id=ident, reserved_split='train', reason=held[ident], source_sha256=sha(image)))
            continue
        row = dict(old)
        annotation = annotations.get(ident)
        chosen = label
        if annotation:
            assert not label.read_text().strip()
            assert annotation['reserved_split'] == old['split']
            assert annotation['source_sha256'] == sha(image)
            chosen = ROOT / annotation['label']
            assert sha(chosen) == annotation['label_sha256']
            row.update(label_sha256=sha(chosen), boxes_xyxy=annotation['boxes_xyxy'],
                       annotation_status='VISUALLY_REVIEWED_BEETLE_V14', class_id=1)
        elif old['split'] == 'train' and not label.read_text().strip():
            pending.append(dict(image=old['image'], original_id=ident, source_sha256=sha(image)))
        for key, source in [('image', image), ('label', chosen)]:
            target = out / old[key]
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
        # Every pre-existing Cockroach label stays byte-identical.
        if label.read_text().strip():
            assert sha(out / row['label']) == sha(label)
        samples.append(row)
    counts = dict(Counter(r['split'] for r in samples))
    assert counts == dict(train=159, val=39, test=9) and len(exclusions) == 2
    class_counts = {s: Counter() for s in counts}
    for row in samples:
        for line in (out / row['label']).read_text().splitlines():
            values = line.split()
            assert len(values) == 5 and values[0] in ('0', '1')
            class_counts[row['split']][values[0]] += 1
    assert class_counts['train']['1'] == 38 and class_counts['val']['1'] == 13
    groups = {}
    for row in samples:
        group = row['group']
        assert groups.setdefault(group, row['split']) == row['split']
    audit = dict(parent, names=['Cockroach', 'Beetle'], samples=samples, counts=counts,
        unique_images=len(samples), parent_audit_sha256=sha(base / 'audit.json'),
        annotation_reviews=reviews, exclusions=exclusions, class_instance_counts=class_counts,
        background_review_pending=pending, training_approved=False,
        unity_mapping={'Cockroach': 'ParkBugs', 'Beetle': None},
        split_policy='No fold reassignments; 2 ambiguous TRAIN-only confusers held; Cockroach labels unchanged',
        limitations=['24 old TRAIN background images require explicit review for Beetle absence',
                     'Historical TEST labels remain Cockroach-only; not valid for Beetle AP',
                     'Public data scope is not PICO device acceptance'])
    (out / 'audit.json').write_text(json.dumps(audit, indent=2))
    (out / 'data.yaml').write_text(json.dumps(dict(path=out.as_posix(), train='images/train',
        val='images/val', test='images/test', names=['Cockroach', 'Beetle']), indent=2))
    report = dict(status='ASSEMBLED_PENDING_BACKGROUND_REVIEW', counts=counts,
        class_instance_counts=class_counts, background_review_pending=len(pending),
        audit_sha256=sha(out / 'audit.json'), training_approved=False)
    (ROOT / 'Reports/cockroach-beetle-v14-assembly.json').write_text(json.dumps(report, indent=2))
    print(json.dumps(report))


if __name__ == '__main__':
    main()
