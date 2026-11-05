"""Enumerate source/author/hash history for a candidate manifest without releasing it."""
import argparse
import hashlib
import json
from pathlib import Path
from source_author_identity import author_identity, reservation_author_keys
from stage_plant_flowering_v12 import walk

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    source, output = (ROOT / args.source).resolve(), (ROOT / args.out).resolve()
    assert source.is_relative_to(ROOT / 'Datasets') and output.is_relative_to(ROOT / 'Reports')
    assert not output.exists()
    manifest = source / 'manifest.json'
    candidates = json.loads(manifest.read_text())['samples']
    paths = set()
    for name in ['manifest.json', 'audit.json', 'selection.json']:
        paths.update((ROOT / 'Datasets').glob('*/' + name))
    paths.update(path for path in (ROOT / 'Reports').glob('*.json') if any(token in path.name for token in
        ['reservation', 'preacquisition', 'preannotation', 'evaluation-plan', 'selection']))
    inputs, matches = [], []
    for path in sorted(paths):
        data = json.loads(path.read_text(encoding='utf-8-sig'))
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
        for location, row in walk(data):
            values = {str(value).strip().lower() for value in row.values() if isinstance(value, str)}
            authors = {author_identity(value) for value in row.values() if isinstance(value, str)}
            for candidate in candidates:
                reasons = []
                if candidate['original_id'].lower() in values:
                    reasons.append('id')
                if candidate['sha256'].lower() in values:
                    reasons.append('encoded_sha256')
                if reservation_author_keys(candidate) & authors:
                    reasons.append('author')
                if reasons:
                    matches.append(dict(candidate=candidate['original_id'], path=path.relative_to(ROOT).as_posix(),
                        location=location, matched=reasons, split=row.get('split'),
                        reserved_split=row.get('reserved_split')))
    result = dict(source_manifest_sha256=sha(manifest), tool_sha256=sha(Path(__file__)),
        identity_helper_sha256=sha(ROOT / 'Tools/MultiScene/source_author_identity.py'),
        inputs=inputs, records=matches, training_approved=False,
        scope='Historical metadata evidence only. Own candidate reservations are expected; other matches require interpretation. No automatic training approval.')
    output.write_text(json.dumps(result, indent=2))
    print(json.dumps(dict(candidates=len(candidates), metadata_files=len(inputs), matches=len(matches),
                         matched_files=sorted({row['path'] for row in matches}))))


if __name__ == '__main__':
    main()
