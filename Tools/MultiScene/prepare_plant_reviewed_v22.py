"""Release six reviewed TRAIN additions after metadata and actual pixel clearance."""
import hashlib
import json
import shutil
from pathlib import Path
from dataset_paths import manifest_relative_path
from dataset_pipeline import decoded_pixel_digest

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parent = ROOT / 'Datasets/plant-reviewed-v20'
    output = ROOT / 'Datasets/plant-reviewed-v22'
    assert not output.exists()
    parent_hash = sha(parent / 'audit.json')
    assert parent_hash == 'e15191e0e50572c0f02206b14f11416e91df08192322fb02e06987e9f215de31'
    audit = json.loads((parent / 'audit.json').read_text())
    review_path = ROOT / 'Reports/plant-cut-arrangement-v22-review/reviewed.json'
    review = json.loads(review_path.read_text())
    source = ROOT / 'Datasets/plant-cut-arrangement-source-v22/manifest.json'
    metadata_path = ROOT / 'Reports/plant-cut-arrangement-v22-source-reservation-audit.json'
    pixel_path = ROOT / 'Reports/plant-cut-arrangement-v22-pixel-history.json'
    metadata, pixels = [json.loads(path.read_text()) for path in [metadata_path, pixel_path]]
    assert metadata['review_sha256'] == sha(review_path)
    assert pixels['source_manifest_sha256'] == sha(source)
    assert not pixels['errors'] and not pixels['matches'] and not pixels['internal_matches']
    assert pixels['scanned_paths'] == pixels['enumerated_paths'] and pixels['candidates'] == 11
    assert not metadata['errors']
    allowed_matches = {'Datasets/plant-cut-arrangement-source-v22/manifest.json',
                       'Datasets/plant-cut-arrangement-source-v22/selection.json',
                       'Reports/plant-cut-arrangement-v22-selection.json'}
    assert {hit['path'] for hit in metadata['records']} == allowed_matches
    for item in metadata['inputs']:
        assert sha(ROOT / item['path']) == item['sha256']
    selected = [row for row in review['samples'] if 'label' in row]
    assert len(selected) == 6
    rows = []
    for row in audit['samples']:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            relative = manifest_relative_path(row[key])
            original, destination = parent / relative, output / relative
            assert sha(original) == row[digest]
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(original, destination)
            assert sha(destination) == row[digest]
        rows.append(dict(row))
    for row in selected:
        assert row['original_reviewed'] and row['overlay_reviewed'] and row['reserved_split'] == 'train'
        image, label = ROOT / row['image'], ROOT / row['label']
        assert sha(image) == row['image_sha256'] and sha(label) == row['label_sha256']
        assert sha(ROOT / row['overlay']) == row['overlay_sha256']
        lines = label.read_text().splitlines()
        assert all(line.split()[0] == '1' for line in lines)
        identifier = sha(image)[:24]
        image_relative, label_relative = f'images/train/{identifier}.jpg', f'labels/train/{identifier}.txt'
        assert not (output / image_relative).exists()
        shutil.copy2(image, output / image_relative)
        shutil.copy2(label, output / label_relative)
        rows.append(dict(original_id=row['original_id'], image=image_relative, label=label_relative,
                         sha256=sha(image), label_sha256=sha(label), decoded_rgb_sha256=decoded_pixel_digest(image),
                         split='train', source='openimages-v7', group=row['group'], attribution=row['attribution'],
                         negative=not lines, auxiliary_instances=len(lines), source_image=row['image'],
                         source_label=row['label'], review_approved=True))
    shutil.copy2(parent / 'native-val.txt', output / 'native-val.txt')
    audit.update(samples=rows, unique_images=len(rows), parent_audit_sha256=parent_hash,
                 counts={split: sum(row['split'] == split for row in rows) for split in ['train', 'val', 'test']},
                 added_review_sha256=sha(review_path), added_metadata_clearance_sha256=sha(metadata_path),
                 added_pixel_clearance_sha256=sha(pixel_path), training_approved=True, deployment_accepted=False,
                 scope='v20 unchanged plus TRAIN-only two cut arrangements and four empty-vase negatives. Full13VAL and4observedTEST unchanged; artificial display held separately.')
    assert audit['counts'] == dict(train=97, val=13, test=4)
    (output / 'audit.json').write_text(json.dumps(audit, indent=2))
    (output / 'data.yaml').write_text(json.dumps(dict(path=str(output), train='images/train', val='native-val.txt',
                                                     test='images/test', names=audit['names']), indent=2))
    report = dict(status='PASS_TRAIN_ONLY_REVIEWED_ADDITIONS', dataset_sha256=sha(output / 'audit.json'),
                  counts=audit['counts'], added_images=6, added_auxiliary_instances=2, added_empty_negatives=4,
                  parent_images_labels_folds_unchanged=True, native_val_list_unchanged=True,
                  historical_pixel_paths=pixels['scanned_paths'], metadata_inputs=len(metadata['inputs']),
                  ambiguous_parent_train_hold_preserved=audit['held_train_samples'],
                  training_started=False, deployment_accepted=False)
    (ROOT / 'Reports/plant-reviewed-v22-release-integrity.json').write_text(json.dumps(report, indent=2))
    print(json.dumps(report))


if __name__ == '__main__':
    main()
