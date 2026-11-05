"""Reserve new TRAIN review candidates; titles never generate detection labels."""
import collections
import csv
import hashlib
import json
import re
from pathlib import Path

from audit_plant_cutflower_v11_candidates import norm

ROOT = Path(__file__).resolve().parents[2]
KEYWORDS = ['ikebana', 'centerpiece', 'vase', 'floral', 'cut flower', 'bouquet']


def main():
    output = ROOT / 'Reports/plant-cut-arrangement-v22-selection.json'
    if output.exists():
        raise FileExistsError(output)
    metadata = ROOT / 'Datasets/openimages-plant-fresh-general-source-v4/metadata.csv'
    paths = set()
    for filename in ['manifest.json', 'audit.json', 'selection.json']:
        paths.update((ROOT / 'Datasets').glob('*/' + filename))
    paths.update((ROOT / 'Reports').glob('*selection*.json'))
    identifiers, authors, evidence = set(), set(), []
    for path in sorted(paths):
        raw = path.read_bytes()
        record = json.loads(raw.decode('utf-8-sig'))  # Fail closed on unreadable reservations.
        if not isinstance(record, dict):
            continue
        for row in record.get('samples', []) + record.get('held_train_samples', []):
            if row.get('original_id'):
                identifiers.add(row['original_id'])
            author = row.get('group') or (row.get('attribution') or {}).get('AuthorProfileURL')
            if author:
                authors.add(norm(author))
        evidence.append(dict(path=path.relative_to(ROOT).as_posix(),
                             sha256=hashlib.sha256(raw).hexdigest()))
    buckets = collections.defaultdict(list)
    with metadata.open(encoding='utf-8') as stream:
        for row in csv.DictReader(stream):
            title = row.get('Title', '').lower()
            keyword = next((key for key in KEYWORDS
                            if re.search(r'\b' + re.escape(key) + r's?\b', title)), None)
            author = row.get('AuthorProfileURL', '')
            identifier = row['ImageID']
            if not keyword or identifier in identifiers or not author or norm(author) in authors:
                continue
            license_url = row.get('License', '').replace('http:', 'https:').rstrip('/')
            if license_url != 'https://creativecommons.org/licenses/by/2.0':
                continue
            if row.get('Rotation') not in ('0', '0.0'):
                continue
            buckets[keyword].append(dict(
                original_id=identifier, group=author,
                attribution={key: row.get(key) for key in
                             ['License', 'Author', 'AuthorProfileURL', 'OriginalLandingURL', 'Title']},
                download_url=f'https://open-images-dataset.s3.amazonaws.com/test/{identifier}.jpg',
                reserved_split='train', selection_keyword=keyword, annotation_status='NOT_REVIEWED'))
    selected, selected_authors = [], set()
    for keyword in KEYWORDS:
        count = 0
        for row in sorted(buckets[keyword], key=lambda item: item['original_id']):
            author = norm(row['group'])
            if author in selected_authors:
                continue
            selected.append(row)
            selected_authors.add(author)
            count += 1
            if count == 4 or len(selected) == 16:
                break
        if len(selected) == 16:
            break
    report = dict(
        scope='Unreviewed TRAIN source candidates only; broad lexical matching is not a class label.',
        metadata_sha256=hashlib.sha256(metadata.read_bytes()).hexdigest(),
        reservation_snapshot=evidence, excluded_ids=len(identifiers), excluded_authors=len(authors),
        keywords=KEYWORDS, eligible_counts={key: len(buckets[key]) for key in KEYWORDS},
        selected_counts=dict(collections.Counter(row['selection_keyword'] for row in selected)),
        samples=selected, training_approved=False,
        remaining_checks=['Original-resolution semantic and instance review',
                          'Encoded and decoded-pixel duplicate clearance against all reserved folds',
                          'Recheck source/author reservations before release'],
        validation_and_test_unchanged=True)
    output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(dict(selected=len(selected), eligible=report['eligible_counts'],
                         reservation_files=len(evidence), training_approved=False)))


if __name__ == '__main__':
    main()
