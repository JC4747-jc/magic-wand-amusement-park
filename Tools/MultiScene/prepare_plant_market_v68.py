"""Add twelve reviewed market negatives to TRAIN, preserving all v65 payloads/folds."""
import hashlib
import json
import shutil
from pathlib import Path
from dataset_paths import manifest_relative_path as rel
from source_author_identity import reservation_author_keys

ROOT = Path(__file__).resolve().parents[2]
SELECTED = {181871142,71372286,100373042,100283802,22876877,25649961,51294815,74919738,76938812,73606065,66561624,71360318}


def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))


def main():
    parent = ROOT/'Datasets/plant-planters-v65'
    source = ROOT/'Datasets/plant-market-train-source-v67'
    out = ROOT/'Datasets/plant-market-v68'
    assert not out.exists(), 'Preserve immutable or partial output for inspection'
    assert sha(parent/'audit.json') == '12f67a5fcf7f54537ac200cf37e80d75132532ca0e98ee2163f3b6358197a886'
    audit, manifest = read(parent/'audit.json'), read(source/'manifest.json')
    review_path = ROOT/'Reports/plant-market-train-v67-original-review.json'
    review = read(review_path)
    pixel_path = ROOT/'Reports/plant-market-train-v67-pixel-history.json'
    pixel = read(pixel_path)
    assert pixel['source_manifest_sha256'] == review['source_manifest_sha256'] == sha(source/'manifest.json')
    assert pixel['enumerated_paths'] == pixel['scanned_paths'] == 20150
    assert not any(pixel[k] for k in ('errors', 'matches', 'internal_matches'))
    reservation = read(source/'train-reservation.json')
    assert reservation['reserved_split'] == 'train' and reservation['source_manifest_sha256'] == sha(source/'manifest.json')
    identity_path = ROOT/'Reports/plant-market-train-v67-identity-history-v2.json'
    creator_path = ROOT/'Reports/plant-market-train-v67-creator-links.json'
    identity, creators = read(identity_path), read(creator_path)
    rows = [r for r in manifest['samples'] if r['page_id'] in SELECTED]
    assert len(rows) == 12
    groups = {r['group'] for r in rows}
    authors = set().union(*(reservation_author_keys(r) for r in rows))
    hits, history = [], []
    def walk(v, split=None):
        if isinstance(v, dict):
            split = v.get('split') or v.get('reserved_split') or v.get('project_reserved_split') or split
            if split in ('val', 'test') and (v.get('page_id') in SELECTED or v.get('group') in groups or reservation_author_keys(v)&authors):
                hits.append(dict(page_id=v.get('page_id'), group=v.get('group'), split=split))
            for child in v.values(): walk(child, split)
        elif isinstance(v, list):
            for child in v: walk(child, split)
    for name in ('audit.json', 'manifest.json', 'selection.json'):
        for p in sorted((ROOT/'Datasets').glob('*/'+name)):
            history.append(dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)))
            walk(read(p))
    assert not hits, hits
    for row in rows:
        check = next(r for r in review['samples'] if r['page_id'] == row['page_id'])
        assert check['original_actually_viewed'] and check['candidate_negative'] and not check['creator_history_hold'] and not check['visual_census_hold']
        assert not next(r for r in identity['rows'] if r['page_id'] == row['page_id'])['historical_hits']
        index = next(i for i, r in enumerate(manifest['samples']) if r['page_id'] == row['page_id'])
        assert index not in creators['missing_creator_link_indices']
        assert row['license'] in {'CC BY-SA 4.0', 'CC BY 4.0', 'CC BY 3.0', 'CC BY 2.0', 'CC0'}
        assert row['publisher_metadata']['Artist']['value'] and row['publisher_metadata']['Credit']['value']
        assert sha(source/rel(row['image'])) == row['sha256']
        assert row['sha256'] not in {r['sha256'] for r in audit['samples']}
    old = list(audit['samples'])
    assert len(old) == 174
    for row in old:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            p = parent/rel(row[key]); assert sha(p) == row[digest]
            target = out/rel(row[key]); target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(p, target); assert sha(target) == row[digest]
    for row in rows:
        ip = f"images/train/{row['sha256'][:24]}.jpg"
        lp = f"labels/train/{row['sha256'][:24]}.txt"
        shutil.copy2(source/rel(row['image']), out/ip)
        (out/lp).write_bytes(b'')
        audit['samples'].append(dict(image=ip, label=lp, sha256=row['sha256'], label_sha256=sha(out/lp),
            decoded_rgb_sha256=row['decoded_rgb_sha256'], split='train', source='commons', group=row['group'],
            page_id=row['page_id'], original_id=str(row['page_id']), negative=True, auxiliary_instances=0,
            artist=row['artist'], publisher_metadata=row['publisher_metadata'],
            attribution=dict(Author=row['artist'], License=row['license_url'], OriginalLandingURL=row['description_url'], Title=row['title']),
            review=review_path.relative_to(ROOT).as_posix()))
    assert audit['samples'][:174] == old
    from ultralytics.data.utils import img2label_paths
    for row in audit['samples']:
        actual = Path(img2label_paths([str(out/rel(row['image']))])[0])
        assert actual.resolve() == (out/rel(row['label'])).resolve()
        assert sha(actual) == row['label_sha256'] and sha(out/rel(row['image'])) == row['sha256']
    inputs = [source/'manifest.json', source/'train-reservation.json', review_path, pixel_path, identity_path, creator_path,
              ROOT/'Reports/plant-market-train-v67-preacquisition.json', Path(__file__).resolve()]
    audit.setdefault('context_evidence', {}).update({p.relative_to(ROOT).as_posix(): sha(p) for p in inputs})
    audit.setdefault('held_train_samples', []).extend(dict(r, reserved_split='train', reason='Creator/history evidence hold; not adopted as empty label') for r in manifest['samples'] if r['page_id'] not in SELECTED)
    audit.update(unique_images=186, counts=dict(train=152, val=30, test=4), parent_audit_sha256=sha(parent/'audit.json'),
        scope='v65 plus twelve visually reviewed TRAIN market negatives; all174 inherited rows/payloads/folds unchanged. No VAL or TEST relabeling.', training_approved=False, deployment_accepted=False)
    audit['food_addition_clearance'] = dict(cross_val_test_hits=hits, historical_inputs=history,
        limitations='Recorded source identities and current pixel comparisons cannot exclude unknown aliases, physical-instance overlap or pretraining exposure.')
    (out/'audit.json').write_text(json.dumps(audit, indent=2), encoding='utf-8')
    config = read(parent/'data.yaml'); config['path'] = str(out)
    (out/'data.yaml').write_text(json.dumps(config, indent=2), encoding='utf-8')
    hold = read(parent/'review_hold.json'); hold['dataset_audit_sha256'] = sha(out/'audit.json')
    (out/'review_hold.json').write_text(json.dumps(hold, indent=2), encoding='utf-8')
    shutil.copy2(parent/'legacy-native-val.txt', out/'legacy-native-val.txt')
    result = dict(status='PREPARED_WITH_OPEN_ANNOTATION_HOLD', images=186, counts=audit['counts'],
        new_market_negative_train_photos=12, inherited_rows_and_payloads_verified=174, actual_yolo_label_paths_verified=186,
        dataset_sha256=sha(out/'audit.json'), training_approved=False, deployment_accepted=False)
    (ROOT/'Reports/plant-market-v68-integrity.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__': main()
