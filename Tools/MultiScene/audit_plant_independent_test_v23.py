"""Check reviewed TEST candidates against historical source and pixel reservations."""
import argparse
import hashlib
import json
from pathlib import Path
from stage_plant_flowering_v12 import walk
from audit_plant_cutflower_v11_candidates import norm
from source_author_identity import author_identity

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--review', default='Reports/plant-independent-test-v23-annotations/reviewed.json')
    parser.add_argument('--out', default='Reports/plant-independent-test-v23-clearance.json')
    args = parser.parse_args()
    review = ROOT / args.review
    selected = json.loads(review.read_text())['samples']
    assert len(selected) in (11, 12) and all(row['overlay_reviewed'] for row in selected)
    paths = set()
    for name in ['manifest.json', 'audit.json', 'selection.json']:
        paths.update((ROOT / 'Datasets').glob('*/' + name))
    paths.update(path for path in (ROOT / 'Reports').glob('*.json') if any(token in path.name for token in
        ['reservation', 'preacquisition', 'preannotation', 'evaluation-plan', 'selection']))
    inputs, hits = [], []
    for path in sorted(paths):
        value = json.loads(path.read_text(encoding='utf-8-sig'))
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
        for location, row in walk(value):
            values = {norm(value) for value in row.values() if isinstance(value, str)}
            author_values = {author_identity(value) for value in row.values() if isinstance(value, str)}
            for candidate in selected:
                matched = [key for key, value in [('id', candidate['original_id']),
                    ('sha256', candidate['image_sha256'])] if value in values]
                if author_identity(candidate['group']) in author_values:
                    matched.append('author')
                if matched:
                    hits.append(dict(candidate=candidate['original_id'], path=path.relative_to(ROOT).as_posix(),
                        location=location, matched=matched, split=row.get('split'), reserved_split=row.get('reserved_split')))
    pixel_inputs = []
    for kind, source in [('first', 'plant-independent-test-source-v23'),
                         ('container', 'plant-independent-test-container-source-v23'),
                         ('box', 'plant-independent-test-box-source-v23')]:
        path = ROOT / f'Reports/plant-independent-test-v23-{kind}-pixel-history.json'
        pixels = json.loads(path.read_text())
        assert pixels['source_manifest_sha256'] == sha(ROOT / 'Datasets' / source / 'manifest.json')
        assert pixels['scanned_paths'] == pixels['enumerated_paths'] and not pixels['errors']
        assert not pixels['matches'] and not pixels['internal_matches']
        pixel_inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path),
                                 scanned_paths=pixels['scanned_paths']))
    allowed = {
        'Datasets/' + source + '/' + name
        for source in ['plant-independent-test-source-v23', 'plant-independent-test-container-source-v23',
                       'plant-independent-test-box-source-v23'] for name in ['manifest.json', 'selection.json']}
    allowed.update('Reports/' + name for name in ['plant-independent-test-v23-selection.json',
        'plant-independent-test-v23-container-selection.json', 'plant-independent-test-v23-box-selection.json'])
    unexpected = [hit for hit in hits if hit['path'] not in allowed]
    result = dict(status='SOURCE_AND_PIXEL_CLEARANCE_PASSED' if not unexpected else 'BLOCKED_HISTORICAL_MATCH',
        review_sha256=sha(review), inputs=inputs, records=hits, unexpected_historical_hits=unexpected,
        pixel_reports=pixel_inputs, training_approved=False, evaluation_ready=False,
        scope='Current-file and enumerated metadata clearance only; no model evaluation or broad-domain acceptance.')
    out = ROOT / args.out
    assert not out.exists()
    out.write_text(json.dumps(result, indent=2))
    print(json.dumps(dict(status=result['status'], metadata_files=len(inputs), hits=len(hits),
                         unexpected=len(unexpected), pixel_reports=len(pixel_inputs))))


if __name__ == '__main__':
    main()
