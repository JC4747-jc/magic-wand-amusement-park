"""Reserve a bounded prospective TRAIN acquisition before downloading/inference."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    out = ROOT/'Reports/plant-food-train-v59-preacquisition.json'
    authors_path = ROOT/'Reports/plant-food-train-v59-known-artists.json'
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
    plan = dict(reserved_split='train', category='Category:Lettuce-based salads', max_images=12,
                max_pages=2, max_per_artist=1, author_exclusion_sha256=sha(authors_path),
                source='https://commons.wikimedia.org/wiki/Category:Lettuce-based_salads',
                scope='All acquired photographs including holds permanently reserved TRAIN before inference. Category does not prove absence of plants, license fitness or authorship. Review entire image; no automatic empty labels.',
                allowed_licenses=['CC BY 2.0', 'CC BY 3.0', 'CC BY 4.0', 'CC0', 'CC BY-SA 4.0'],
                training_approved=False, deployment_accepted=False)
    out.write_text(json.dumps(plan, indent=2), encoding='utf-8')
    # Previously held originals were re-opened this turn; preserve old reviews.
    source = ROOT/'Datasets/plant-hanging-train-source-v49/manifest.json'
    manifest = json.loads(source.read_text(encoding='utf-8'))
    notes = {
        106792794: 'Sparse soil pot with long thin stems crossing background grass; distinguishing rooted stems and excluding supports needs boundary review.',
        99427707: 'Dense greenhouse with numerous overlapping hanging baskets and trays of small ground pots; incomplete instance census.',
        132263778: 'Overlapping purple-leaf baskets plus white background pot and other containers; leaf-to-container ownership unresolved.',
        89955956: 'Castle has foreground hanging baskets, arch baskets and planted windowbox; overlaps and distant container grouping need explicit census.',
        112116808: 'Main hanging succulent plus numerous separate cactus and succulent pots, including clipped top/side/bottom containers; one-box annotation would leave targets unlabeled.',
        92697132: 'Two doorway baskets plus visible white ground planter and dense garden; plant-to-container boundaries and obscured containers unresolved.',
    }
    review = []
    for row in manifest['samples']:
        if row['page_id'] not in notes:
            continue
        p = source.parent/row['image']
        assert sha(p) == row['sha256']
        review.append(dict(page_id=row['page_id'], image_sha256=sha(p), original_actually_viewed=True,
                           observation=notes[row['page_id']], reserved_split='train', annotation_approved=False))
    assert len(review) == 6
    review_path = ROOT/'Reports/plant-hanging-v49-reinspection-v59.json'
    assert not review_path.exists()
    review_path.write_text(json.dumps(dict(source_manifest_sha256=sha(source), samples=review,
                                         labels_changed=False, training_approved=False), indent=2), encoding='utf-8')
    print(json.dumps(dict(status='TRAIN_ACQUISITION_RESERVED', excluded_authors=len(authors), historical_inputs=len(inputs), rechecked_held_images=6)))


if __name__ == '__main__':
    main()
