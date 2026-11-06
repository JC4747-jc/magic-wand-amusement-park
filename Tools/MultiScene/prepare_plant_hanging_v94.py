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
    parent = ROOT/'Datasets/plant-hanging-v88'
    out = ROOT/'Datasets/plant-hanging-v94'
    source = ROOT/'Datasets/plant-hanging-train-source-v92'
    reviewpath = ROOT/'Reports/plant-hanging-v93-boxes/reviewed-two.json'
    assert not out.exists(), 'Preserve existing output'
    assert sha(parent/'audit.json') == 'efee464040393a27bc61eb94eb3de4a1abf90506f0f2d5faf3b43cd31c2dc8c1'
    assert sha(reviewpath) == '5150292bb9a71867cec9441b26f4f7112ef6ec20f802f47cc70c6d6f800b527c'
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
    assert len(old) == 200
    ids = {r['page_id'] for r in review['samples']}
    assert ids == {90151588, 97729076}
    for row in old:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            path = parent/rel(row[key])
            assert sha(path) == row[digest]
            destination = out/rel(row[key])
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, destination)
    for box in review['samples']:
        assert all(box[k] for k in ('original_reviewed', 'overlay_reviewed', 'annotation_approved'))
        row = next(r for r in manifest['samples'] if r['page_id'] == box['page_id'])
        for key in ('image', 'label', 'overlay'):
            assert sha(ROOT/rel(box[key])) == box[key+'_sha256']
        assert row['sha256'] == box['image_sha256']
        assert row['sha256'] not in {r['sha256'] for r in old}
        image = f"images/train/{row['sha256'][:24]}.jpg"
        label = f"labels/train/{row['sha256'][:24]}.txt"
        shutil.copy2(ROOT/rel(box['image']), out/image)
        shutil.copy2(ROOT/rel(box['label']), out/label)
        audit['samples'].append(dict(
            image=image, label=label, sha256=row['sha256'],
            label_sha256=box['label_sha256'], decoded_rgb_sha256=decoded_pixel_digest(out/image),
            split='train', source='commons', group=row['group'],
            page_id=row['page_id'], original_id=str(row['page_id']), negative=False,
            auxiliary_instances=0, artist=row['artist'], publisher_metadata=row['publisher_metadata'],
            attribution=dict(Author=row['artist'], License=row['license_url'],
                             OriginalLandingURL=row['description_url'], Title=row['title'],
                             ChangeNotice='Original photograph unchanged; manual detection annotations added.'),
            review=reviewpath.relative_to(ROOT).as_posix()))
    assert audit['samples'][:200] == old and audit['held_train_samples'] == old_holds
    for held in read(ROOT/'Reports/plant-hanging-train-v92-original-review.json')['samples']:
        if held['page_id'] not in ids:
            evidence=next(r for r in manifest['samples'] if r['page_id']==held['page_id'])
            audit['held_train_samples'].append(dict(page_id=evidence['page_id'],artist=evidence['artist'],group=evidence['group'],reserved_split='train',reason=held['observation'],source_manifest='Datasets/plant-hanging-train-source-v92/manifest.json',annotation_approved=False))
    assert audit['held_train_samples'][:len(old_holds)]==old_holds
    audit.setdefault('context_evidence', {}).update({reviewpath.relative_to(ROOT).as_posix(): sha(reviewpath),
                                                   Path(__file__).resolve().relative_to(ROOT).as_posix(): sha(Path(__file__))})
    audit.update(unique_images=202, counts=dict(train=168, val=30, test=4),
                 parent_audit_sha256=sha(parent/'audit.json'),
                 scope='Binary v88 plus two reviewed original TRAIN photographs with five rooted hanging-container instances. All200 parent rows/payloads/folds and prior holds unchanged. Ten other v92 sources held, never empty negative labels.',
                 training_approved=False, deployment_accepted=False)
    audit['hanging_addition_v94'] = dict(review_sha256=sha(reviewpath), new_images=2, new_class0_instances=5,
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
    print(json.dumps(dict(images=202, counts=audit['counts'], parent_rows_exact=200,
                          full_frame_holds_unchanged=True, dataset_sha256=sha(out/'audit.json'),
                          training_approved=False, deployment_accepted=False)))


if __name__ == '__main__':
    main()
