"""Stage reviewed TRAIN labels only after source and pixel evidence checks."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path
from dataset_paths import manifest_relative_path

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'review', 'metadata', 'pixels', 'out']:
        parser.add_argument('--' + name, required=True)
    parser.add_argument('--own-evidence', nargs='*', default=[], help='Earlier metadata audits bound to this exact source manifest')
    args = parser.parse_args()
    source, output = (ROOT / args.source).resolve(), (ROOT / args.out).resolve()
    assert source.is_relative_to(ROOT / 'Datasets') and output.is_relative_to(ROOT / 'Datasets')
    assert not output.exists()
    review_path, metadata_path, pixel_path = [ROOT / value for value in [args.review, args.metadata, args.pixels]]
    review, metadata, pixels = [json.loads(path.read_text()) for path in [review_path, metadata_path, pixel_path]]
    manifest_path = source / 'manifest.json'
    manifest = json.loads(manifest_path.read_text())
    assert metadata['source_manifest_sha256'] == pixels['source_manifest_sha256'] == sha(manifest_path)
    assert not pixels['errors'] and not pixels['matches'] and not pixels['internal_matches']
    assert pixels['scanned_paths'] == pixels['enumerated_paths']
    for item in metadata['inputs']:
        assert sha(ROOT / manifest_relative_path(item['path'])) == item['sha256']
    own = {manifest_path.relative_to(ROOT).as_posix(), (source / 'selection.json').relative_to(ROOT).as_posix()}
    for relative in args.own_evidence:
        evidence_path = ROOT / manifest_relative_path(relative)
        assert evidence_path.is_relative_to(ROOT / 'Reports')
        evidence = json.loads(evidence_path.read_text())
        assert evidence['source_manifest_sha256'] == sha(manifest_path)
        own.add(evidence_path.relative_to(ROOT).as_posix())
    selected_ids = {row['original_id'] for row in review['samples']}
    for hit in metadata['records']:
        if hit['candidate'] not in selected_ids:
            continue
        assert hit.get('split') not in ('val', 'test') and hit.get('reserved_split') not in ('val', 'test')
        path = ROOT / manifest_relative_path(hit['path'])
        assert hit['path'] in own or (path.is_relative_to(ROOT / 'Reports') and sha(path) == sha(source / 'selection.json'))
    original_rows = {row['original_id']: row for row in manifest['samples']}
    rows = []
    for row in review['samples']:
        assert row['original_reviewed'] and row['overlay_reviewed'] and row['review_approved']
        assert row['reserved_split'] == 'train'
        original = original_rows[row['original_id']]
        image, label, overlay = [ROOT / manifest_relative_path(row[key]) for key in ['image', 'label', 'overlay']]
        assert sha(image) == row['image_sha256'] == original['sha256']
        assert sha(label) == row['label_sha256'] and sha(overlay) == row['overlay_sha256']
        relative_image, relative_label = 'images/' + image.name, 'labels/' + row['original_id'] + '.txt'
        for path, relative in [(image, relative_image), (label, relative_label)]:
            destination = output / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, destination)
            assert sha(destination) == sha(path)
        rows.append(dict(original_id=row['original_id'], image=relative_image, label=relative_label,
            sha256=sha(image), label_sha256=sha(label), group=original['group'], attribution=original['attribution'],
            source='openimages-v7', reserved_split='train', negative=not label.read_text().strip(), review_approved=True))
    result = dict(samples=rows, names=review['names'], sources=manifest['sources'],
        source_manifest_sha256=sha(manifest_path), review_sha256=sha(review_path),
        metadata_audit_sha256=sha(metadata_path), pixel_audit_sha256=sha(pixel_path),
        training_approved=False, scope='Reviewed TRAIN staging only. No automatic release, training or model acceptance.')
    (output / 'manifest.json').write_text(json.dumps(result, indent=2))
    print(json.dumps(dict(staged_images=len(rows), training_approved=False)))


if __name__ == '__main__':
    main()
