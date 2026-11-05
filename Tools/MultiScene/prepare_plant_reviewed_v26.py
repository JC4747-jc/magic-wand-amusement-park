"""Merge six reviewed TRAIN additions; preserve every v24 image, label and fold."""
import hashlib
import json
import shutil
from pathlib import Path
from dataset_paths import manifest_relative_path
from dataset_pipeline import decoded_pixel_digest
from source_author_identity import reservation_author_keys

ROOT = Path(__file__).resolve().parents[2]
STAGES = [
 ('plant-container-train-v26-dense-staging','plant-container-train-v26-dense-annotations','plant-container-train-v24-batch2-source-reservation-audit','plant-container-train-v24-batch2-pixel-history'),
 ('plant-container-train-v26-paired-staging','plant-container-train-v26-paired-annotations','plant-container-train-v24-paired-source-reservation-audit','plant-container-train-v24-paired-pixel-history'),
 ('plant-container-train-v26-staging','plant-container-train-v26-annotations','plant-container-train-v26-source-reservation-audit','plant-container-train-v26-pixel-history'),
]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    parent, output = ROOT / 'Datasets/plant-reviewed-v24', ROOT / 'Datasets/plant-reviewed-v26'
    assert not output.exists()
    assert sha(parent / 'audit.json') == 'bfc4e62a80af6ea1c5f755283c9306cda3baf3aa24f1ab9ca39b88442cdcae1a'
    audit = read(parent / 'audit.json')
    additions, lineage = [], []
    for stage_name, review_name, metadata_name, pixel_name in STAGES:
        stage = ROOT / 'Datasets' / stage_name
        manifest_path = stage / 'manifest.json'
        manifest = read(manifest_path)
        review_path = ROOT / 'Reports' / review_name / 'reviewed.json'
        metadata_path, pixel_path = [ROOT / 'Reports' / (name + '.json') for name in [metadata_name, pixel_name]]
        assert manifest['review_sha256'] == sha(review_path)
        assert manifest['metadata_audit_sha256'] == sha(metadata_path)
        assert manifest['pixel_audit_sha256'] == sha(pixel_path)
        reviews = {row['original_id']: row for row in read(review_path)['samples']}
        for item in read(metadata_path)['inputs']:
            assert sha(ROOT / manifest_relative_path(item['path'])) == item['sha256']
        pixels = read(pixel_path)
        assert not pixels['errors'] and not pixels['matches'] and not pixels['internal_matches']
        assert pixels['scanned_paths'] == pixels['enumerated_paths']
        for row in manifest['samples']:
            review = reviews[row['original_id']]
            assert row['review_approved'] and review['original_reviewed'] and review['overlay_reviewed']
            assert row['reserved_split'] == 'train'
            image, label = [stage / manifest_relative_path(row[key]) for key in ['image', 'label']]
            assert sha(image) == row['sha256'] == review['image_sha256']
            assert sha(label) == row['label_sha256'] == review['label_sha256']
            assert sha(ROOT / review['overlay']) == review['overlay_sha256']
            additions.append((row, image, label))
        lineage.append(dict(stage=stage_name, manifest_sha256=sha(manifest_path), review_sha256=sha(review_path),
                            metadata_sha256=sha(metadata_path), pixels_sha256=sha(pixel_path)))
    assert len(additions) == 6
    added_ids = {row['original_id'] for row, _, _ in additions}
    assert len(added_ids) == 6
    added_authors = set().union(*(reservation_author_keys(row) for row, _, _ in additions))
    parent_authors = set().union(*(reservation_author_keys(row) for row in audit['samples']))
    assert len(added_authors) == 6 and not (added_authors & parent_authors)
    # Include all historical released and source-only VAL/TEST reservations, not just v22.
    for name in ['manifest.json', 'audit.json', 'selection.json']:
        for path in (ROOT / 'Datasets').glob('*/' + name):
            data = read(path)
            default = data.get('project_reserved_split') or data.get('reserved_split')
            for row in data.get('samples', []) + data.get('held_samples', []):
                split = row.get('split') or row.get('reserved_split') or default
                if split in ('val', 'test'):
                    assert row.get('original_id') not in added_ids
                    assert not (reservation_author_keys(row) & added_authors)
    hashes, pixels_seen, rows = set(), set(), []
    for old in audit['samples']:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            original = parent / manifest_relative_path(old[key])
            assert sha(original) == old[digest]
            destination = output / manifest_relative_path(old[key])
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(original, destination)
            assert sha(destination) == old[digest]
        hashes.add(old['sha256'])
        pixels_seen.add(decoded_pixel_digest(parent / manifest_relative_path(old['image'])))
        rows.append(dict(old))
    counts = [0, 0]
    for row, image, label in additions:
        digest, decoded = sha(image), decoded_pixel_digest(image)
        assert digest not in hashes and decoded not in pixels_seen
        hashes.add(digest)
        pixels_seen.add(decoded)
        lines = label.read_text().splitlines()
        for line in lines:
            values = line.split()
            assert len(values) == 5 and values[0] in ('0', '1') and all(0 <= float(v) <= 1 for v in values[1:])
            counts[int(values[0])] += 1
        relative_image, relative_label = f'images/train/{digest[:24]}.jpg', f'labels/train/{digest[:24]}.txt'
        shutil.copy2(image, output / relative_image)
        shutil.copy2(label, output / relative_label)
        rows.append(dict(original_id=row['original_id'], image=relative_image, label=relative_label,
            sha256=digest, label_sha256=sha(label), decoded_rgb_sha256=decoded, source=row['source'],
            group=row['group'], attribution=row['attribution'], split='train', negative=not lines,
            auxiliary_instances=sum(line.startswith('1 ') for line in lines),
            source_image=image.relative_to(ROOT).as_posix(), source_label=label.relative_to(ROOT).as_posix()))
    assert counts == [14, 1]
    pending = [row for row in audit['pending_train_samples'] if row['original_id'] not in added_ids]
    assert len(pending) == 20
    review_path = ROOT / 'Reports/plant-container-train-v26-annotations/reviewed.json'
    for row in read(review_path)['held_samples']:
        pending.append(dict(original_id=row['original_id'], group=row['group'], reserved_split='train',
            decision=row['notes'], review=review_path.relative_to(ROOT).as_posix(), review_sha256=sha(review_path)))
    assert len(pending) == 28
    shutil.copy2(parent / 'native-val.txt', output / 'native-val.txt')
    audit.update(samples=rows, unique_images=len(rows), counts=dict(train=117, val=13, test=4),
        parent_audit_sha256=sha(parent / 'audit.json'), added_staging_lineage=lineage, pending_train_samples=pending,
        training_approved=True, deployment_accepted=False,
        scope='v24 unchanged plus six reviewed TRAIN images, fourteen target and one cut auxiliary instance. Existing VAL/observed TEST bytes and reservations unchanged.')
    assert len(rows) == 134
    (output / 'audit.json').write_text(json.dumps(audit, indent=2))
    (output / 'data.yaml').write_text(json.dumps(dict(path=str(output), train='images/train', val='native-val.txt',
                                                     test='images/test', names=audit['names']), indent=2))
    result = dict(status='PASS_REVIEWED_TRAIN_ONLY_MERGE', dataset_sha256=sha(output / 'audit.json'),
        counts=audit['counts'], added_images=6, added_target_instances=14, added_auxiliary_instances=1,
        pending_source_images=len(pending), all128_parent_image_label_bytes_and_folds_unchanged=True,
        native_val_list_unchanged=True, training_started=False, deployment_accepted=False)
    (ROOT / 'Reports/plant-reviewed-v26-release-integrity.json').write_text(json.dumps(result, indent=2))
    print(json.dumps(result))


if __name__ == '__main__':
    main()
