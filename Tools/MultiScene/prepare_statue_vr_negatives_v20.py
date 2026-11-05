"""Add two reviewed VR-user TRAIN negatives, preserving inherited fold bytes."""
import hashlib
import json
import shutil
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    parent = ROOT / 'Datasets/statue-rear-context-v17'
    source = ROOT / 'Datasets/statue-vr-confuser-train-source-v20'
    output = ROOT / 'Datasets/statue-vr-negatives-v20'
    assert not output.exists()
    manifest = source / 'manifest.json'
    review = ROOT / 'Reports/statue-vr-confuser-train-v20-empty-label-review.json'
    provenance = ROOT / 'Reports/statue-vr-confuser-train-v20-publisher-linkage.json'
    pixels = ROOT / 'Reports/statue-vr-confuser-train-v20-pixel-history.json'
    reservation = source / 'train-reservation.json'
    p, d, b, m = map(read, [provenance, pixels, review, manifest])
    assert p['status'] == 'PASS' and not d['errors'] and not d['matches'] and not d['internal_matches']
    assert d['enumerated_paths'] == d['scanned_paths']
    assert p['source_manifest_sha256'] == d['source_manifest_sha256'] == b['source_manifest_sha256'] == sha(manifest)
    for item in p['inputs']:
        assert sha(ROOT / item['path']) == item['sha256']
    reservations = read(reservation)
    assert reservations['reserved_split'] == 'train'
    assert reservations['source_manifest_sha256'] == sha(manifest)
    originals = {Path(s['image']).name: s for s in m['samples']}
    audit = read(parent / 'audit.json')
    assert sha(parent / 'audit.json') == '280a95cdd1508a371b52fee5f5977ad837c0213b10dbcb7409df117ea25dbaca'
    rows = b['samples']
    assert len(rows) == 2
    inherited = list(audit['samples'])
    for row in inherited:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            assert sha(parent / row[key]) == row[digest]
    for row in rows:
        s = originals[Path(row['image']).name]
        assert s['license'] in ['CC BY 2.0','CC BY 3.0','CC BY 4.0','CC BY-SA 4.0','CC0'] and s['artist']
        assert row['original_reviewed'] and row['overlay_reviewed']
        assert row['status'] == 'ORIGINAL_AND_OVERLAY_REVIEWED_NEGATIVE_APPROVED'
        assert sha(ROOT / row['image']) == row['sha256'] == s['sha256']
        assert sha(ROOT / row['overlay']) == row['overlay_sha256']
        assert all(s['group'] != old['group'] for old in inherited)
    identity = ROOT/'Reports/statue-vr-confuser-train-v20-identity-history.json'
    creators = ROOT/'Reports/statue-vr-confuser-train-v20-creator-links.json'
    history = read(identity)
    assert not history['errors'] and all(not item['historical_hits'] for item in history['rows'])
    assert history['source_manifest_sha256'] == sha(manifest)
    # Creator audit is retained as evidence, not a proof of every unknown alias.
    creator_audit = read(creators)
    assert creator_audit['source_sha256'] == sha(manifest)
    assert not creator_audit['collisions']
    selected_ids = {row['page_id'] for row in rows}
    assert not any(m['samples'][i]['page_id'] in selected_ids for i in creator_audit['missing_creator_link_indices'])
    output.mkdir()
    for row in inherited:
        for key in ['image', 'label']:
            dest = output / row[key]
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(parent / row[key], dest)
    for row in rows:
        s = originals[Path(row['image']).name]
        image = output / 'images/train' / Path(row['image']).name
        label = output / 'labels/train' / (image.stem + '.txt')
        assert not image.exists() and not label.exists()
        shutil.copy2(ROOT / row['image'], image)
        label.write_text('')
        audit['samples'].append(dict(s, image=image.relative_to(output).as_posix(),
            label=label.relative_to(output).as_posix(), label_sha256=sha(label), split='train',
            review_approved=True, training_approved=True, annotation_status='REVIEWED',
            review=review.relative_to(ROOT).as_posix()))
    audit['sources'].append(dict(id='commons-statue-vr-negatives-v20', reviewed=True, license='Per-photo CC BY 2/4, CC BY-SA4 or CC0; credit and ShareAlike retained', url='https://commons.wikimedia.org/wiki/Category:People_wearing_virtual_reality_headsets'))
    audit.update(parent_audit_sha256=sha(parent / 'audit.json'), training_approved=True,
        deployment_accepted=False, unique_images=len(audit['samples']),
        split_counts=dict(Counter(s['split'] for s in audit['samples'])),
        revision='Two reviewed VR-user TRAIN negatives; inherited images, labels and folds unchanged.',
        context_evidence={p.relative_to(ROOT).as_posix(): sha(p) for p in [review, provenance, pixels, manifest, reservation, identity, creators]})
    (output / 'audit.json').write_text(json.dumps(audit, indent=2))
    (output / 'data.yaml').write_text(json.dumps(dict(path=str(output), train='images/train', val='images/val', test='images/test', names=['Statue']), indent=2))
    for row in inherited:
        for key in ['image', 'label']:
            assert sha(output / row[key]) == sha(parent / row[key])
    print(json.dumps(dict(counts=audit['split_counts'], audit_sha256=sha(output / 'audit.json'), inherited_verified=len(inherited))))


if __name__ == '__main__':
    main()
