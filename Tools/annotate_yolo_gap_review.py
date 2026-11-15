"""Author reproducible draft boxes after inspecting the 29 original review photos."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.codex-tmp/yolo-training/gap-review/manifest.json'
TARGET = ROOT / 'TrainingData/public-gap-annotations-v1.json'
# Bounds are in pixels of the exact SHA-bound downloaded photograph, never model predictions.
BOXES = {
    'inat-414820062': [225, 510, 455, 885],
    'inat-414708372': [490, 382, 704, 516],
    'inat-414319613': [128, 345, 429, 710],
    'inat-414173185': [205, 257, 815, 650],
    'inat-414168220': [350, 288, 646, 528],
    'inat-413955771': [97, 140, 541, 645],
    'inat-413866317': [310, 435, 444, 600],
    'inat-413807289': [329, 350, 510, 570],
    'inat-414256437': [324, 313, 611, 481],
    'inat-413808525': [344, 345, 536, 789],
    'inat-413336029': [260, 270, 620, 714],
    'inat-412205156': [295, 116, 733, 790],
    'inat-410056471': [136, 329, 398, 487],
    'inat-407564596': [303, 150, 669, 589],
    'commons-177625644': [247, 43, 1022, 784],
    'commons-101302261': [220, 140, 1056, 730],
    'commons-98011103': [168, 263, 1113, 745],
    'commons-97859136': [215, 125, 1030, 822],
    'commons-4300068': [127, 38, 1033, 951],
    'commons-101302262': [104, 63, 1077, 803],
    'commons-97144425': [251, 42, 1104, 824],
    'commons-31216981': [27, 29, 1245, 815],
    'commons-20059983': [6, 0, 1267, 834],
}
QUARANTINE = {
    'inat-412469468': 'Damaged/dead specimen with uncertain limb extent; exclude initial positive set, not a negative.',
    'inat-412229588': 'Severely out of focus; species/body extent cannot be visually established, not a negative.',
    'commons-177812865': 'Flattened animal-decorated vessel; insufficient initial fit to freestanding three-dimensional beast scope.',
    'commons-98006765': 'Flat bird-shaped bronze ornament; outside initial three-dimensional beast scope.',
    'commons-175786493': 'Beast-shaped connector fragment with socket body; exclude initial full-animal set.',
    'commons-97144424': 'Head-only crop of the same Chu instrument stand as commons-97144425; not independent object evidence.',
}
ARTIFACT_GROUPS = {
    'commons-4300068': 'smithsonian-f1961-3',
    'commons-101302262': 'smithsonian-f1961-3',
    'commons-101302261': 'smithsonian-f1940-23',
    'commons-97144425': 'henan-chu-musical-instrument-stand',
    'commons-97144424': 'henan-chu-musical-instrument-stand',
    # The small bronze may reproduce the large Arezzo statue: conservatively keep together.
    'commons-31216981': 'arezzo-chimera-and-replicas',
    'commons-20059983': 'arezzo-chimera-and-replicas',
}


def main():
    raw = SOURCE.read_bytes()
    source = json.loads(raw)
    assert len(source['records']) == 29 and set(BOXES).isdisjoint(QUARANTINE)
    assert {r['source_key'] for r in source['records']} == set(BOXES) | set(QUARANTINE)
    records = []
    for r in source['records']:
        assert r['downloaded']
        image = ROOT / r['local_path']
        assert hashlib.sha256(image.read_bytes()).hexdigest() == r['sha256']
        key = r['source_key']
        result = {k: r[k] for k in ('source_key', 'canonical_candidate', 'image_url', 'local_path', 'sha256', 'size')}
        result['accepted_for_deployment'] = False
        result['split'] = None
        if key.startswith('inat-'):
            result.update(source_page=r['observation_url'], taxon_name=r['taxon_name'], photo_id=r['photo']['id'],
                photo_license_code=r['photo']['license_code'], attribution=r['photo']['attribution'],
                observer_id=r['observer_id'], observed_on=r['observed_on'], capture_group=r['capture_group'])
        else:
            m = r['image_info']['extmetadata']
            result.update(source_page=r['image_info']['descriptionurl'], title=r['title'],
                photo_license=m['LicenseShortName']['value'], photo_license_url=m.get('LicenseUrl', {}).get('value'),
                attribution_html=m.get('Artist', {}).get('value'), description_html=m.get('ImageDescription', {}).get('value'),
                capture_group=ARTIFACT_GROUPS.get(key, 'artifact-' + key))
        if key in QUARANTINE:
            result.update(status='QUARANTINE_NOT_NEGATIVE', reason=QUARANTINE[key], boxes=[])
        else:
            x0, y0, x1, y1 = BOXES[key]
            w, h = r['size']
            assert 0 <= x0 < x1 <= w and 0 <= y0 < y1 <= h
            result.update(status='DRAFT_BOX_NEEDS_OVERLAY_REVIEW', boxes=[{'class': r['canonical_candidate'],
                'xyxy_pixels': BOXES[key], 'xyxy_normalized': [x0/w, y0/h, x1/w, y1/h]}])
        records.append(result)
    report = {'version': 1, 'source_manifest_sha256': hashlib.sha256(raw).hexdigest(),
        'purpose': 'ASSISTANT_AUTHORED_DRAFT_ANNOTATIONS_NOT_MODEL_OR_HOLDOUT_ACCEPTANCE',
        'box_policy': {'Cockroach': 'Visible body, wings, egg case and legs; exclude long thin antennae and shadows. No amodal extent.',
                       'BronzeBeast': 'Freestanding three-dimensional bronze animal/mythical beast, including whole animal-shaped vessels; exclude plain vessels with motifs, flat ornaments and isolated fragments. Visible object extent, no pedestal/shadow.'},
        'method': 'Original JPEGs individually inspected; pixel boxes manually authored by assistant, not generated by a detector.',
        'grouping': 'iNaturalist observer/day; identified museum artifact and Arezzo replica family shared across photographs. Near-duplicate/component review still required.',
        'image_licenses': 'Retained per-image from public API metadata; no images included in the repository. License version not inferred from an iNaturalist short code.',
        'split_assigned': False, 'records': records}
    TARGET.parent.mkdir(exist_ok=True)
    if TARGET.exists():
        raise RuntimeError('Refusing to replace authored annotation file; make a reviewed versioned change instead')
    TARGET.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf8')
    print(json.dumps({'photos': len(records), 'draft_positive_boxes': len(BOXES), 'quarantined': len(QUARANTINE), 'split_assigned': False}))


if __name__ == '__main__':
    main()
