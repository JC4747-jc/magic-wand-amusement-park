"""Stage reviewed source-reserved tree confusers; do not train or alter held-out folds."""
import hashlib
import json
import shutil
from pathlib import Path

from dataset_paths import manifest_relative_path
from source_author_identity import reservation_author_keys

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    review_path = ROOT / 'Reports/tree-depiction-v19-annotations/reviewed.json'
    review = read(review_path)
    assert not review['model_predictions_used']
    selected = review['samples']
    assert len(selected) == 3
    out = ROOT / 'Datasets/tree-confuser-reviewed-bank-v20'
    assert not out.exists()
    evidence = []
    for source_name, prefix in [('tree-train-source-v18', 'tree-train-v18'),
                                ('tree-whole-train-source-v19', 'tree-whole-train-v19')]:
        source = ROOT / 'Datasets' / source_name
        metadata_path = ROOT / 'Reports' / (prefix + '-source-reservation-audit.json')
        pixel_path = ROOT / 'Reports' / (prefix + '-pixel-history.json')
        metadata, pixels = read(metadata_path), read(pixel_path)
        assert metadata['source_manifest_sha256'] == pixels['source_manifest_sha256'] == sha(source / 'manifest.json')
        for item in metadata['inputs']:
            assert sha(ROOT / manifest_relative_path(item['path'])) == item['sha256']
        own = {f'Datasets/{source_name}/manifest.json', f'Datasets/{source_name}/selection.json',
               f'Reports/{prefix}-selection.json'}
        assert all(row['path'] in own for row in metadata['records'])
        assert pixels['scanned_paths'] == pixels['enumerated_paths']
        assert not any(pixels[key] for key in ['errors', 'matches', 'internal_matches'])
        evidence.extend([metadata_path, pixel_path])
    ids = {row['original_id'] for row in selected}
    authors = set().union(*(reservation_author_keys(row) for row in selected))
    assert len(ids) == len(authors) == 3
    for name in ['manifest.json', 'audit.json', 'selection.json', 'reservation.json']:
        for path in (ROOT / 'Datasets').glob('*/' + name):
            data = read(path)
            default = data.get('project_reserved_split') or data.get('reserved_split')
            for row in data.get('samples', []) + data.get('held_samples', []):
                if (row.get('split') or row.get('reserved_split') or default) in ('val', 'test'):
                    assert row.get('original_id') not in ids
                    assert not reservation_author_keys(row) & authors
    rows = []
    for row in selected:
        assert row['reserved_split'] == 'train' and all(row[key] for key in
            ['original_reviewed', 'overlay_reviewed', 'review_approved'])
        for key in ['image', 'label', 'overlay']:
            assert sha(ROOT / manifest_relative_path(row[key])) == row[key + '_sha256']
        assert sha(ROOT / row['source_manifest']) == row['source_manifest_sha256']
        source_manifest = read(ROOT / row['source_manifest'])
        original = next(r for r in source_manifest['samples'] if r['original_id'] == row['original_id'])
        assert original['sha256'] == row['image_sha256'] and original['reserved_split'] == 'train'
        assert not (ROOT / row['label']).read_bytes()
        image, label = f"images/{row['original_id']}.jpg", f"labels/{row['original_id']}.txt"
        for relative, key in [(image, 'image'), (label, 'label')]:
            target = out / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(ROOT / manifest_relative_path(row[key]), target)
            assert sha(target) == row[key + '_sha256']
        rows.append(dict(row, image=image, label=label, sha256=row['image_sha256'],
                         source='openimages-v7', negative=True))
    result = dict(names=['Tree'], samples=rows, training_approved=False,
        status='REVIEWED_BANK_PENDING_COMBINED_RELEASE', review_sha256=sha(review_path),
        evidence=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in evidence],
        limitations=['Three TRAIN negatives only, no new positive or independent evaluation evidence.',
                     'Unknown cross-platform creator aliases remain possible; no deployment acceptance.'])
    (out / 'audit.json').write_text(json.dumps(result, indent=2))
    print(json.dumps(dict(staged_negatives=3, training_started=False, deployment_accepted=False)))


if __name__ == '__main__':
    main()
