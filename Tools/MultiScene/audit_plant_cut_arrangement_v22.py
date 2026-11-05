"""Enumerate historical source reservations for visually reviewed v22 candidates."""
import hashlib
import json
from pathlib import Path
from stage_plant_flowering_v12 import walk
from audit_plant_cutflower_v11_candidates import norm

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    review = ROOT / 'Reports/plant-cut-arrangement-v22-review/reviewed.json'
    selected = [row for row in json.loads(review.read_text())['samples'] if 'label' in row]
    assert len(selected) == 6 and all(row['original_reviewed'] and row['overlay_reviewed'] for row in selected)
    paths = set()
    for name in ['manifest.json', 'audit.json', 'selection.json']:
        paths.update((ROOT / 'Datasets').glob('*/' + name))
    paths.update(path for path in (ROOT / 'Reports').glob('*.json') if any(
        token in path.name for token in ['reservation', 'preacquisition', 'preannotation', 'evaluation-plan', 'selection']))
    inputs, hits = [], []
    for path in sorted(paths):
        record = json.loads(path.read_text(encoding='utf-8-sig'))
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
        for location, row in walk(record):
            values = {norm(value) for value in row.values() if isinstance(value, str)}
            for candidate in selected:
                matched = [key for key, value in [
                    ('id', candidate['original_id']), ('author', norm(candidate['group'])),
                    ('sha256', candidate['image_sha256'])] if value in values]
                if matched:
                    hits.append(dict(candidate=candidate['original_id'], path=path.relative_to(ROOT).as_posix(),
                                     location=location, matched=matched, split=row.get('split'),
                                     reserved_split=row.get('reserved_split')))
    output = ROOT / 'Reports/plant-cut-arrangement-v22-source-reservation-audit.json'
    assert not output.exists()
    output.write_text(json.dumps(dict(inputs=inputs, records=hits, review_sha256=sha(review),
        errors=[], training_approved=False,
        scope='Complete enumerated metadata match evidence; hits must be interpreted before release.'), indent=2))
    print(json.dumps(dict(metadata_files=len(inputs), hits=len(hits),
                          matched_files=sorted({hit['path'] for hit in hits}), training_approved=False)))


if __name__ == '__main__':
    main()
