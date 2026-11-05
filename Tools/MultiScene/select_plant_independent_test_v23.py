"""Reserve a blinded, author-separated plant TEST candidate pool before inference."""
import argparse
import csv
import hashlib
import json
import re
from pathlib import Path
from audit_plant_cutflower_v11_candidates import norm
from source_author_identity import author_identity

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', default='Reports/plant-independent-test-v23-selection.json')
    parser.add_argument('--keywords', nargs='+', default=['cactus', 'orchid', 'geranium', 'begonia', 'vase', 'centerpiece'])
    parser.add_argument('--container-annotations', action='store_true')
    parser.add_argument('--per-keyword-limit', type=int, default=2)
    args = parser.parse_args()
    out = (ROOT / args.out).resolve()
    assert out.is_relative_to(ROOT / 'Reports')
    assert not out.exists()
    assert 1 <= args.per_keyword_limit <= 12
    metadata = ROOT / 'Datasets/openimages-plant-fresh-general-source-v4/metadata.csv'
    paths = set()
    for name in ['manifest.json', 'audit.json', 'selection.json']:
        paths.update((ROOT / 'Datasets').glob('*/' + name))
    paths.update((ROOT / 'Reports').glob('*selection*.json'))
    ids, authors, inputs = set(), set(), []
    for path in sorted(paths):
        raw = path.read_bytes()
        value = json.loads(raw.decode('utf-8-sig'))
        if not isinstance(value, dict):
            continue
        for row in value.get('samples', []) + value.get('held_train_samples', []):
            if row.get('original_id'):
                ids.add(row['original_id'])
            for author in [row.get('group'), (row.get('attribution') or {}).get('AuthorProfileURL')]:
                if author:
                    authors.add(author_identity(author))
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(raw).hexdigest()))
    annotation_path = ROOT / 'Datasets/openimages-plant-fresh-general-source-v4/boxes.csv'
    container_ids = set()
    if args.container_annotations:
        with annotation_path.open(encoding='utf-8') as stream:
            for row in csv.DictReader(stream):
                if row['LabelName'] in {'/m/03fp41', '/m/0fm3zh'} and row['IsGroupOf'] == '0' and row['IsDepiction'] == '0':
                    container_ids.add(row['ImageID'])
    keywords = ['upstream-container-annotation'] if args.container_annotations else args.keywords
    assert 1 <= len(keywords) <= 8 and len(set(keywords)) == len(keywords)
    buckets = {key: [] for key in keywords}
    with metadata.open(encoding='utf-8') as stream:
        for row in csv.DictReader(stream):
            identifier, author = row['ImageID'], row.get('AuthorProfileURL', '')
            if identifier in ids or not author or author_identity(author) in authors:
                continue
            if row.get('License', '').replace('http:', 'https:').rstrip('/') != 'https://creativecommons.org/licenses/by/2.0':
                continue
            if row.get('Rotation') not in ('0', '0.0'):
                continue
            key = ('upstream-container-annotation' if identifier in container_ids else None) if args.container_annotations else next((key for key in keywords if re.search(r'\b' + re.escape(key) + r's?\b', row.get('Title', '').lower())), None)
            if key:
                buckets[key].append(row)
    selected, used = [], set()
    for key in keywords:
        count = 0
        for row in sorted(buckets[key], key=lambda item: item['ImageID']):
            author = author_identity(row['AuthorProfileURL'])
            if author in used:
                continue
            used.add(author)
            selected.append(dict(original_id=row['ImageID'], group=row['AuthorProfileURL'],
                attribution={name: row.get(name) for name in ['License', 'Author', 'AuthorProfileURL', 'OriginalLandingURL', 'Title']},
                download_url=f'https://open-images-dataset.s3.amazonaws.com/test/{row["ImageID"]}.jpg',
                reserved_split='test', selection_keyword=key, annotation_status='NOT_REVIEWED'))
            count += 1
            if count == args.per_keyword_limit:
                break
    report = dict(scope='Blinded TEST-only candidate reservation; no predictions used. Titles are not labels.',
        metadata_sha256=hashlib.sha256(metadata.read_bytes()).hexdigest(), reservation_snapshot=inputs,
        samples=selected, eligible_counts={key: len(rows) for key, rows in buckets.items()},
        keywords=keywords, per_keyword_limit=args.per_keyword_limit, training_approved=False,
        container_annotation_sha256=hashlib.sha256(annotation_path.read_bytes()).hexdigest() if args.container_annotations else None,
        upstream_annotation_scope='Candidate discovery only; neither class nor box is adopted without original review.',
        evaluation_ready=False, reserved_split='test',
        policy='Never move these authors/images into TRAIN or VAL. Manual semantic/instance review and duplicate clearance precede test release. Reject unsuitable images using image content only, never model predictions. Freeze candidate model and threshold before inference.')
    out.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(dict(candidates=len(selected), eligible=report['eligible_counts'], reservation_files=len(inputs))))


if __name__ == '__main__':
    main()
