"""Reserve a bounded prospective TRAIN acquisition before downloading/inference."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    out = ROOT/'Reports/plant-market-train-v67-preacquisition.json'
    authors_path = ROOT/'Reports/plant-market-train-v67-known-artists.json'
    assert not out.exists() and not authors_path.exists()
    authors, inputs = set(), []
    def walk(value):
        if isinstance(value, dict):
            if isinstance(value.get('artist'), str) and value['artist'].strip():
                authors.add(value['artist'].strip())
            for child in value.values():
                walk(child)
        elif isinstance(value, list):
            for child in value:
                walk(child)
    for name in ('audit.json', 'manifest.json', 'selection.json'):
        for p in sorted((ROOT/'Datasets').glob('*/'+name)):
            walk(json.loads(p.read_text(encoding='utf-8-sig')))
            inputs.append(dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)))
    assert authors
    authors_path.write_text(json.dumps(dict(artists=sorted(authors), inputs=inputs), indent=2), encoding='utf-8')
    plan = dict(reserved_split='train', category='Category:Vegetable stalls', max_images=20,
                max_pages=2, max_per_artist=1, author_exclusion_sha256=sha(authors_path),
                source='https://commons.wikimedia.org/wiki/Category:Vegetable_stalls',
                scope='All acquired photographs including holds permanently reserved TRAIN before inference. Category does not prove target absence, licensing or authorship. Review entire frame for rooted plants and cut arrangements; no automatic empty labels.',
                allowed_licenses=['CC BY 2.0', 'CC BY 3.0', 'CC BY 4.0', 'CC0', 'CC BY-SA 4.0'],
                training_approved=False, deployment_accepted=False)
    out.write_text(json.dumps(plan, indent=2), encoding='utf-8')

    print(json.dumps(dict(status='TRAIN_MARKET_ACQUISITION_RESERVED', excluded_authors=len(authors), historical_inputs=len(inputs))))


if __name__ == '__main__':
    main()
