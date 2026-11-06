"""Append three reviewed TRAIN crop derivatives; preserve v85 and full-frame holds."""
import hashlib
import json
import shutil
from pathlib import Path
from dataset_paths import manifest_relative_path as rel
from dataset_pipeline import decoded_pixel_digest

ROOT = Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def main():
    parent = ROOT/'Datasets/plant-hanging-v85'
    out = ROOT/'Datasets/plant-hanging-v88'
    source = ROOT/'Datasets/plant-hanging-crop-source-v86'
    reviewpath = ROOT/'Reports/plant-hanging-crops-v87-boxes/reviewed-three.json'
    assert not out.exists(), 'Preserve existing output'
    assert sha(parent/'audit.json') == '887a056dbdeb50865aba59ff93bd743289d8396cf379f1dbd20e92c1d6fcfab8'
    assert sha(reviewpath) == '976913682f31263a89aa856fbc874db5b01e104539192e86f2177098ba463219'
    review = read(reviewpath)
    assert not review['cross_val_test_hits']
    for name, digest in review['proofs'].items():
        assert sha(ROOT/rel(name)) == digest
    # Recheck the complete historical scan snapshot before adopting derivatives.
    for proof in review['historical_inputs']:
        assert sha(ROOT/rel(proof['path'])) == proof['sha256']
    manifest = read(source/'manifest.json')
    assert sha(source/'manifest.json') == review['source_manifest_sha256']
    audit = read(parent/'audit.json')
    old = list(audit['samples'])
    old_holds = list(audit['held_train_samples'])
    assert len(old) == 197
    ids = {r['page_id'] for r in review['samples']}
    assert ids == {200088542, 60784237, 134655765}
    assert ids.issubset({r.get('page_id') for r in old_holds})
    for row in old:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            path = parent/rel(row[key])
            assert sha(path) == row[digest]
            destination = out/rel(row[key])
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, destination)
    for box in review['samples']:
        assert all(box[k] for k in ('original_reviewed', 'overlay_reviewed', 'annotation_approved', 'lossless_crop_verified'))
        row = next(r for r in manifest['samples'] if r['page_id'] == box['page_id'])
        for key in ('image', 'label', 'overlay'):
            assert sha(ROOT/rel(box[key])) == box[key+'_sha256']
        assert row['sha256'] == box['image_sha256']
        assert row['sha256'] not in {r['sha256'] for r in old}
        image = f"images/train/{row['sha256'][:24]}.png"
        label = f"labels/train/{row['sha256'][:24]}.txt"
        shutil.copy2(ROOT/rel(box['image']), out/image)
        shutil.copy2(ROOT/rel(box['label']), out/label)
        audit['samples'].append(dict(
            image=image, label=label, sha256=row['sha256'],
            label_sha256=box['label_sha256'], decoded_rgb_sha256=decoded_pixel_digest(out/image),
            split='train', source='commons-derived-crop', group=row['group'],
            page_id=row['page_id'], original_id=str(row['page_id']), negative=False,
            auxiliary_instances=0, artist=row['artist'], publisher_metadata=row['publisher_metadata'],
            derivation=row['derivation'],
            attribution=dict(Author=row['artist'], License=row['license_url'],
                             OriginalLandingURL=row['description_url'], Title=row['title'],
                             ChangeNotice='Lossless crop of original photograph; crop coordinates and original SHA-256 recorded in derivation.'),
            review=reviewpath.relative_to(ROOT).as_posix()))
    assert audit['samples'][:197] == old and audit['held_train_samples'] == old_holds
    audit.setdefault('context_evidence', {}).update({reviewpath.relative_to(ROOT).as_posix(): sha(reviewpath),
                                                   Path(__file__).resolve().relative_to(ROOT).as_posix(): sha(Path(__file__))})
    audit.update(unique_images=200, counts=dict(train=166, val=30, test=4),
                 parent_audit_sha256=sha(parent/'audit.json'),
                 scope='Binary v85 plus three reviewed lossless TRAIN crops. All197 parent rows, payloads and folds unchanged; original full-frame holds retained. Crops share original source groups and are not independent sources.',
                 training_approved=False, deployment_accepted=False)
    audit['crop_addition_v88'] = dict(review_sha256=sha(reviewpath), new_images=3, new_class0_instances=3,
                                    original_full_frame_holds_retained=True,
                                    limitation='Underside/dense multi-basket coverage remains incomplete; no independent TEST or PICO acceptance.')
    (out/'audit.json').write_text(json.dumps(audit, indent=2), encoding='utf-8')
    config = read(parent/'data.yaml')
    config['path'] = str(out)
    (out/'data.yaml').write_text(json.dumps(config, indent=2), encoding='utf-8')
    hold = read(parent/'review_hold.json')
    hold['dataset_audit_sha256'] = sha(out/'audit.json')
    (out/'review_hold.json').write_text(json.dumps(hold, indent=2), encoding='utf-8')
    shutil.copy2(parent/'legacy-native-val.txt', out/'legacy-native-val.txt')
    print(json.dumps(dict(images=200, counts=audit['counts'], parent_rows_exact=197,
                          full_frame_holds_unchanged=True, dataset_sha256=sha(out/'audit.json'),
                          training_approved=False, deployment_accepted=False)))


if __name__ == '__main__':
    main()
