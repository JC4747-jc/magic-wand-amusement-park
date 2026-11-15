"""Record the visual semantic screening of all 60 ambiguous source photographs."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'TrainingData/openimages-semantic-review-v1.json'
STATUE_CANDIDATES = set('2834df29044b96a1 76de7db03ced37ae 07251bb443093202 2ccf2343d8d6b899 13b7206961bca9c6 4ecc85eb4b742f62 8a1acbd94021367c 7d1cb6eec3e2d13f 37ad4393b55ce83f 7b3c99d0ae458835 6dbcd715a2aae68c 6bb7e70c38657568'.split())
POTTED_CANDIDATES = set('133667cd04b760e7 3f26f78e96430d20 1d6468e90f600660 326c062e0b66f5af 53f5816d58a9159d ddbb872ebdedb99a 9f6c11b60796e451 571e87b93aecab33'.split())
REASONS = {
 '2644292432654db6': 'Architectural column detail rather than a clear independent statue target.',
 '199146e00b05846b': 'Only an isolated sculpted hand is visible.',
 '7506c71127e786b2': 'Costume/armour display; source Statue box covers the background, not the main object.',
 '8edb19a248373836': 'Fashion display/mannequin ambiguity; sculpture identity not established.',
 '1568dfe2beeda804': 'Only the feet of a larger statue are visible.',
 '7d9c6a1fabd2e1ee': 'Stone animal relief; initial mapping/target extent unresolved. Not a bronze beast.',
 '563cd442d7a8fe61': 'Doll display; do not silently equate dolls with the scene statue target.',
 '7953df9f00a3d067': 'Animal fountain; material and intended Statue/BronzeBeast mapping unresolved.',
 '319a174180a93dc5': 'Many tiny or incompletely scoped pots; per-container box completeness needs reannotation.',
 '269191f83be6c348': 'Small outdoor planters; source boxes appear to omit container extent.',
 '00d962525f1ea4e7': 'Flower close-up with no clear container/base extent.',
 '2250c417f8fdede6': 'Flower close-up across multiple partially visible plants; per-container extent ambiguous.',
 '2a0feb84d28eec15': 'Dark/occluded foliage; living potted-plant identity and container extent unresolved.',
 '115ef722923602a8': 'Small/occluded balcony plants; container-aware completeness not yet verified.',
 '0fc1771518abb67c': 'Rhubarb growing directly in a garden bed, not a potted plant.',
 '4019e570109201cc': 'Cactus foliage close-up with no visible pot/base.',
 '2ce47ae6d1cd0c63': 'Orchid flower close-up with no clear container/base.',
 '2260e855a2c63167': 'Tulip close-up with no clear pot/base.',
 '1cadca0d3ff6e185': 'Cut-flower arrangement; do not map cut flowers to a growing potted plant.',
 '2eb40861eb6097a1': 'Main fern box omits pot bottom; second window-container label uncertain; needs explicit reannotation.',
 '2a934341e5fce116': 'Cut flowers in a vase rather than a growing potted plant.',
 '20b1acb2c4566634': 'Loose air plants for sale in a bowl; per-container instance scope ambiguous.',
 '0867ed7b739f753a': 'Cactus close-up with no visible pot/base.',
 '1a65ac537f92de02': 'Garden urn decoration; source label does not establish a growing potted plant.',
 '948794bfee784412': 'Multiple partly occluded pots grouped into one box; per-container labels incomplete.',
 '8dc48310cd96cac3': 'Cut flowers with no growing plant/container identity.',
 'd29f095f98aec84f': 'Flower/container partly cropped; source box omits most of the container; needs reannotation.',
 'b499182ee39701e0': 'Cut flowers in a vase; not a growing potted plant.',
 '48b2d1bc4e911898': 'Foliage close-up; no clear pot/base.',
 'dc031a77f70f0593': 'Foliage behind glass; container extent not established.',
 '64685fb20bec41db': 'Nested planters and several small containers; source boxes omit pot extents and need per-container reannotation.',
 '90a059259cefdb3a': 'Flowers near siding; no clear pot/base.',
 'da627065ef5a9ce2': 'Foliage close-up; container extent not established.',
 'dbdcc799cfaf617d': 'Flower close-up; pots belong to surrounding plants, instance extent unresolved.',
 '888d409664a38e78': 'Foliage close-up with unclear pot/base.',
 'a0893025c69a4ee8': 'Mixed flower arrangement; growing-container and per-instance scope unresolved.',
 '4c8d29126f5f6154': 'Sunflower appears to grow from a chimney masonry crack; the object behind is a chimney pot, not clear planted-container evidence.',
 '83b8fb35cbf7ffa1': 'Orchid flowers close-up with no clear pot/base.',
 '64bffc40f0d073fe': 'Decorative floral arrangement with toy; growing potted-plant identity unresolved.',
 'c7a4c2afc29aa9e3': 'Cut tulips/bouquet; no growing potted plant.',
}


def main():
    sources = []
    all_records = {}
    for name in ('candidate-p20.json', 'potted-p40.json'):
        path = ROOT / '.codex-tmp/yolo-training/openimages-review' / name
        raw = path.read_bytes()
        sources.append({'manifest': path.relative_to(ROOT).as_posix(), 'sha256': hashlib.sha256(raw).hexdigest()})
        for r in json.loads(raw)['records']:
            all_records[r['image_id']] = r
    records = []
    for key, r in all_records.items():
        categories = {a['canonical_candidate'] for a in r['annotations']}
        if not categories & {'Statue', 'PottedPlant'}:
            continue
        candidate = key in STATUE_CANDIDATES | POTTED_CANDIDATES
        assert candidate or key in REASONS, 'Unrecorded visual decision: ' + key
        assert hashlib.sha256((ROOT / r['local_path']).read_bytes()).hexdigest() == r['sha256']
        meta = r['official_metadata']
        entry = {'image_id': key, 'image_sha256': r['sha256'], 'download_url': r['download_url'],
            'source_page': meta['OriginalLandingURL'], 'author': meta['Author'], 'license': meta['License'],
            'local_path': r['local_path'], 'size': r['size'], 'source_annotations': r['annotations'],
            'decision': 'SEMANTIC_CANDIDATE_BOX_COMPLETENESS_PENDING' if candidate else 'QUARANTINE_NOT_NEGATIVE',
            'reason': 'Visible sculpture target or growing potted plant; initial candidate, not a complete domain sample.' if candidate else REASONS[key],
            'training_split_assigned': False, 'accepted_for_deployment': False}
        if key == '133667cd04b760e7':
            entry['required_correction'] = 'Bonsai in a visible pot: PottedPlant only; remove competing Tree label for the same object.'
        records.append(entry)
    assert len(records) == 60
    reviewed_sample_issues = {
        '06bdb5ea9ec0e963': 'Indoor artificial Christmas trees; living-tree scene domain is not established.',
        '00f3dbbbf5bdf2cf': 'Photo includes large unannotated pallet chairs beside the labeled couch; do not train them as background.'}
    report = {'version': 1, 'purpose': 'SEMANTIC_SCREENING_NOT_TRAINING_SPLIT_OR_MODEL_ACCEPTANCE',
        'sources': sources, 'review_method': 'All 60 ambiguous photos inspected in original-RGB annotation QA plots; selected difficult photos also inspected individually.',
        'reviewed_ambiguous_photos': len(records), 'semantic_candidates': sum(r['decision'].startswith('SEMANTIC') for r in records),
        'quarantined_not_negatives': sum(r['decision'].startswith('QUARANTINE') for r in records),
        'exact_class_sample': '15 distinct photos covering the first two image IDs per exact class were inspected; two issues recorded. This is not exhaustive verification of other photos.',
        'additional_sample_issues': reviewed_sample_issues,
        'limitations': ['Statue candidates emphasize clear human/carved figures; broader sculpture targets remain in scope and require suitable real examples.',
            'Some source boxes exclude pot extents or other target instances; semantic suitability alone does not authorize their use as complete YOLO labels.',
            'Do not silently use quarantined photos as negatives or turn Sculpture/Houseplant mappings into accepted target classes.'], 'records': records}
    if OUT.exists():
        raise RuntimeError('Refusing to overwrite versioned semantic review')
    OUT.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf8')
    print(json.dumps({k: report[k] for k in ('reviewed_ambiguous_photos', 'semantic_candidates', 'quarantined_not_negatives')}))


if __name__ == '__main__':
    main()
