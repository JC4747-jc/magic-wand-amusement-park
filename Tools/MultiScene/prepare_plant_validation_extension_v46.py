"""Add four pre-inference, reviewed harvested-leaf VAL negatives; preserve parent."""
import hashlib
import json
import shutil
from pathlib import Path
from dataset_paths import manifest_relative_path as rel
from source_author_identity import reservation_author_keys
from dataset_pipeline import decoded_pixel_digest

ROOT = Path(__file__).resolve().parents[2]
IDS = {101386677, 77153479, 75729987, 147571749}


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def main():
    parent = ROOT / 'Datasets/plant-validation-extension-v44'
    source = ROOT / 'Datasets/plant-leafy-val-source-v45'
    out = ROOT / 'Datasets/plant-validation-extension-v46'
    review = ROOT / 'Reports/plant-leafy-val-v45-reviewed-negatives.json'
    assert not out.exists() and not review.exists()
    assert sha(parent / 'audit.json') == 'dc89d225420ccb20176cd5e35bbee853e02aa903697cf2232efb2ec97330d075'
    manifest = read(source / 'manifest.json')
    rows = [r for r in manifest['samples'] if r['page_id'] in IDS]
    assert len(rows) == 4
    proofs = [ROOT / f'Reports/plant-leafy-val-v45-{suffix}.json' for suffix in
              ['identity-history-v2', 'creator-links', 'pixel-history', 'original-review']]
    identity, links, pixel, original = map(read, proofs)
    assert not identity['errors'] and all(not r['historical_hits'] for r in identity['rows'] if r['page_id'] in IDS)
    assert not links['collisions'] and not links['missing_creator_link_indices']
    assert pixel['scanned_paths'] == pixel['enumerated_paths'] == 19112
    assert not pixel['errors'] and not pixel['matches']
    assert all(p['first'] not in IDS and p['second'] not in IDS for p in pixel['internal_matches'])
    for proof in (identity, pixel, original):
        assert proof['source_manifest_sha256'] == sha(source / 'manifest.json')
    assert read(source / 'val-reservation.json')['reserved_split'] == 'val'
    authors = set().union(*(reservation_author_keys(r) for r in rows))
    groups = {r['group'] for r in rows}
    history, hits = [], []

    def walk(value, split=None):
        if isinstance(value, dict):
            split = value.get('split') or value.get('reserved_split') or value.get('project_reserved_split') or split
            if split in ('train', 'test') and (value.get('page_id') in IDS or value.get('group') in groups or reservation_author_keys(value) & authors):
                hits.append(dict(split=split, page_id=value.get('page_id'), group=value.get('group')))
            for child in value.values():
                walk(child, split)
        elif isinstance(value, list):
            for child in value:
                walk(child, split)

    for name in ('audit.json', 'manifest.json', 'selection.json'):
        for path in (ROOT / 'Datasets').glob('*/' + name):
            history.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
            walk(read(path))
    assert not hits, hits
    audit = read(parent / 'audit.json')
    old = list(audit['samples'])
    assert len(old) == 155
    for row in old:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            src, dst = parent / rel(row[key]), out / rel(row[key])
            assert sha(src) == row[digest]
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
            assert sha(dst) == row[digest]
    reviewed = []
    for row in rows:
        image = source / rel(row['image'])
        assert sha(image) == row['sha256']
        assert row['license'] == 'CC BY-SA 4.0'
        assert 'Own work' in row['publisher_metadata']['Credit']['value']
        assert row['sha256'] not in {r['sha256'] for r in old}
        ip = f"images/val/{row['sha256'][:24]}.jpg"
        lp = f"labels/val/{row['sha256'][:24]}.txt"
        shutil.copy2(image, out / ip)
        (out / lp).write_bytes(b'')
        reviewed.append(dict(page_id=row['page_id'], original_actually_reviewed=True,
                             image_sha256=sha(image), label_sha256=sha(out / lp),
                             annotation_approved=True, reserved_split='val',
                             observation='Harvested food leaves/produce; full-frame review found no PottedPlant or CutArrangement.'))
        audit['samples'].append(dict(image=ip, label=lp, sha256=sha(image), label_sha256=sha(out / lp),
            decoded_rgb_sha256=decoded_pixel_digest(image), split='val', source='commons',
            group=row['group'], original_id=str(row['page_id']), page_id=row['page_id'], negative=True,
            auxiliary_instances=0, artist=row['artist'], publisher_metadata=row['publisher_metadata'],
            attribution=dict(Author=row['artist'], License=row['license_url'], OriginalLandingURL=row['description_url'], Title=row['title']),
            review=review.relative_to(ROOT).as_posix(), validation_cohort='harvested-leaf-v45'))
    review.write_text(json.dumps(dict(samples=reviewed, evidence={p.relative_to(ROOT).as_posix(): sha(p) for p in proofs},
        limitation='Single-assistant review, not independent double review; VAL only, no inference performed.'), indent=2), encoding='utf-8')
    audit.setdefault('held_val_samples', []).extend(dict(r, reserved_split='val', reason='Shared physical scene or outside harvested-leaf domain; not adopted') for r in manifest['samples'] if r['page_id'] not in IDS)
    audit.update(unique_images=159, counts=dict(train=125, val=30, test=4), parent_audit_sha256=sha(parent / 'audit.json'),
                 target_runtime_val_images=30, training_approved=False, deployment_accepted=False,
                 scope='Prospective validation pilot: four harvested-leaf negatives added; all155parent rows and payloads unchanged. No new model evaluation.')
    audit['leafy_val_extension_clearance'] = dict(cross_train_test_hits=hits, historical_inputs=history,
        evidence={p.relative_to(ROOT).as_posix(): sha(p) for p in proofs + [review, Path(__file__).resolve()]})
    shutil.copy2(parent / 'legacy-native-val.txt', out / 'legacy-native-val.txt')
    (out / 'audit.json').write_text(json.dumps(audit, indent=2), encoding='utf-8')
    (out / 'data.yaml').write_text(json.dumps(dict(path=str(out), train='images/train', val='images/val', test='images/test', names=audit['names']), indent=2), encoding='utf-8')
    hold = dict(status='OPEN', dataset_audit_sha256=sha(out / 'audit.json'), training_approved=False,
                reason='Small prospective pilot; seventeen source holds retained, single-assistant annotations not independently double reviewed. Future evaluation/selection protocol not frozen.')
    (out / 'review_hold.json').write_text(json.dumps(hold, indent=2), encoding='utf-8')
    result = dict(status='PREPARED_WITH_OPEN_HOLD', counts=audit['counts'], inherited_payloads_verified=155,
                  new_val_negative_images=4, dataset_sha256=sha(out / 'audit.json'), training_approved=False)
    (ROOT / 'Reports/plant-validation-extension-v46-integrity.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
