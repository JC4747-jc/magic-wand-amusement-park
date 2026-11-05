"""Release inherited TRAIN negatives with provenance and preserve parent folds."""
import csv
import hashlib
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    source = ROOT / 'Datasets/cockroach-diverse-negative-v15-source'
    parent = ROOT / 'Datasets/cockroach-beetle-v14'
    out = ROOT / 'Datasets/cockroach-diverse-v15'
    assert not out.exists()
    manifest = json.loads((source / 'manifest.json').read_text())
    pixel_path = ROOT / 'Reports/cockroach-diverse-negative-v15-pixel-history.json'
    pixel = json.loads(pixel_path.read_text())
    assert pixel['source_manifest_sha256'] == sha(source / 'manifest.json')
    assert not pixel['errors'] and not pixel['internal_matches']
    assert all(r['encoded_exact'] and r['decoded_exact'] for r in pixel['matches'])
    records = []
    evidence_files = sorted(set((ROOT / 'Datasets').rglob('audit.json')) |
                            set((ROOT / 'Datasets').rglob('manifest.json')))
    for path in evidence_files:
        document = json.loads(path.read_text(encoding='utf-8-sig'))
        for row in document.get('samples', []):
            records.append((path, row))
    metadata_path = ROOT / 'Datasets/openimages-v7-pilot/metadata.csv'
    metadata = {r['ImageID']: r for r in csv.DictReader(metadata_path.open(encoding='utf-8-sig'))}
    lineage = []
    for row in manifest['samples']:
        matches = []
        for path, historical in records:
            keys = [k for k in ['group', 'original_id', 'sha256', 'decoded_rgb_sha256']
                    if row.get(k) and row.get(k) == historical.get(k)]
            if keys:
                split = historical.get('reserved_split', historical.get('split'))
                assert split not in ('val', 'test'), (row['original_id'], path, split)
                matches.append(dict(document=path.relative_to(ROOT).as_posix(), split=split, keys=keys))
        meta = metadata[row['original_id']]
        assert meta['License'] == 'https://creativecommons.org/licenses/by/2.0/'
        assert row['group'] == 'openimages-v7:' + meta['AuthorProfileURL']
        lineage.append(dict(original_id=row['original_id'], matches=matches, attribution=meta))
    assert len(lineage) == 18
    base = json.loads((parent / 'audit.json').read_text())
    assert sha(parent / 'audit.json') == 'a64c75fa07304b27fac7ea87f0a1f39bbe8c466387b8ea02335838ea7abc560b'
    samples = []
    for row in base['samples']:
        assert sha(parent / row['image']) == row['sha256']
        assert sha(parent / row['label']) == row['label_sha256']
        for key in ['image', 'label']:
            dest = out / row[key]
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(parent / row[key], dest)
        samples.append(dict(row))
    hashes = {r['sha256'] for r in samples}
    for row, provenance in zip(manifest['samples'], lineage):
        assert row['reserved_split'] == 'train' and row['sha256'] not in hashes
        hashes.add(row['sha256'])
        image = source / row['image']
        assert sha(image) == row['sha256']
        target = Path('images/train') / ('diverse-' + image.name)
        label = Path('labels/train') / (target.stem + '.txt')
        shutil.copy2(image, out / target)
        (out / label).write_text('')
        samples.append(dict(row, image=target.as_posix(), label=label.as_posix(),
            label_sha256=sha(out / label), split='train', training_approved=True,
            attribution=provenance['attribution'], annotation_status='REVIEWED_ZERO_COCKROACH_AND_BEETLE'))
    groups = {}
    for row in samples:
        assert groups.setdefault(row['group'], row['split']) == row['split']
    counts = {s: sum(r['split'] == s for r in samples) for s in ['train', 'val', 'test']}
    assert counts == dict(train=177, val=39, test=9)
    review = dict(source_manifest_sha256=sha(source / 'manifest.json'),
        metadata_sha256=sha(metadata_path), pixel_report_sha256=sha(pixel_path),
        metadata_documents_scanned=len(evidence_files), lineage=lineage,
        pixel_matches=len(pixel['matches']), training_approved=True,
        limitations=['Public OpenImages original subset differs from project author-grouped folds; preserve project folds',
                     'Exact identity checks do not prove absence of unknown aliases',
                     'Images reused from TRAIN source banks; not fresh independent validation'])
    rp = ROOT / 'Reports/cockroach-diverse-negative-v15-release.json'
    assert not rp.exists()
    rp.write_text(json.dumps(review, indent=2))
    audit = dict(base, samples=samples, counts=counts, unique_images=len(samples),
        parent_audit_sha256=sha(parent / 'audit.json'), training_approved=True,
        background_review_pending=[], background_release_sha256=sha(rp),
        split_policy='207 parent image/label pairs unchanged;18 inherited TRAIN-source negatives added; no fold reassignments')
    (out / 'audit.json').write_text(json.dumps(audit, indent=2))
    (out / 'data.yaml').write_text(json.dumps(dict(path=out.as_posix(), train='images/train',
        val='images/val', test='images/test', names=['Cockroach', 'Beetle']), indent=2))
    print(json.dumps(dict(counts=counts, audit_sha256=sha(out / 'audit.json'), metadata_documents=len(evidence_files))))


if __name__ == '__main__':
    main()
