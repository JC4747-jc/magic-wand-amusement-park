"""Freeze a limited, source-reviewed tree TEST pilot; never authorize deployment."""
import hashlib
import json
import shutil
from pathlib import Path
from dataset_paths import manifest_relative_path
from dataset_pipeline import decoded_pixel_digest

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    out = ROOT / 'Datasets/tree-independent-test-v23'
    plan_path = ROOT / 'Reports/tree-independent-v23-evaluation-plan.json'
    assert not out.exists() and not plan_path.exists()
    selected, held, evidence, own_dirs = [], [], [], set()
    for suffix, source_name in [('', 'commons-tree-independent-source-v23'),
                                ('-negative', 'commons-tree-negative-independent-source-v23')]:
        source = ROOT / 'Datasets' / source_name
        own_dirs.add(source)
        manifest_path = source / 'manifest.json'
        review_path = ROOT / f'Reports/tree-independent-v23{suffix}-annotations/reviewed.json'
        identity_path = ROOT / f'Reports/tree-independent-v23{suffix}-identity-history.json'
        pixels_path = ROOT / f'Reports/tree-independent-v23{suffix}-pixel-history.json'
        manifest, review, identity, pixels = map(read, [manifest_path, review_path, identity_path, pixels_path])
        assert not review['model_predictions_used']
        assert review['source_manifest_sha256'] == identity['source_manifest_sha256'] == pixels['source_manifest_sha256'] == sha(manifest_path)
        assert not identity['errors'] and all(not r['historical_hits'] for r in identity['rows'])
        assert pixels['scanned_paths'] == pixels['enumerated_paths']
        assert not any(pixels[k] for k in ['errors', 'matches', 'internal_matches'])
        originals = {r['page_id']: r for r in manifest['samples']}
        for row in review['samples']:
            assert row['original_reviewed'] and row['overlay_reviewed'] and row['review_approved']
            assert row['reserved_split'] == originals[row['page_id']]['reserved_split'] == 'test'
            image = ROOT / manifest_relative_path(row['source_image'])
            assert sha(image) == row['sha256'] == originals[row['page_id']]['sha256']
            for key in ['label', 'overlay']:
                assert sha(ROOT / manifest_relative_path(row[key])) == row[key + '_sha256']
            selected.append(row)
        held.extend(review['held_samples'])
        evidence.extend([manifest_path, source / 'reservation.json', review_path, identity_path, pixels_path])
    assert len(selected) == 5 and len(held) == 8
    groups, pages = {r['group'] for r in selected}, {r['page_id'] for r in selected}
    assert len(groups) == len(pages) == 5
    snapshots = []
    for name in ['manifest.json', 'audit.json', 'selection.json', 'reservation.json']:
        for path in sorted((ROOT / 'Datasets').glob('*/' + name)):
            data = read(path)
            snapshots.append(path)
            if path.parent in own_dirs:
                continue
            for row in data.get('samples', []) + data.get('held_samples', []):
                assert row.get('page_id') not in pages and row.get('group') not in groups, str(path)
    physical_path = ROOT / 'Reports/tree-independent-v23-physical-subject-metadata-review.json'
    physical = read(physical_path)
    assert all(not r['historical_matches'] for r in physical['samples'])
    for row in physical['inputs']:
        assert sha(ROOT / row['path']) == row['sha256']
    evidence.append(physical_path)
    model = ROOT / 'object-detection-demo/weights/candidates/tree-runtime-selection-v22/best.pt'
    expected = '2c5db942db9491a803796a5b87556243b79059937e7c88b3ade6cb1d355f1ae6'
    assert sha(model) == expected
    pre = ROOT / 'Reports/tree-independent-v23-preacquisition.json'
    assert read(pre)['weight_sha256'] == expected
    rows, seen, positives = [], set(), 0
    for row in selected:
        image, label = [ROOT / manifest_relative_path(row[key]) for key in ['source_image', 'label']]
        digest = decoded_pixel_digest(image)
        assert digest not in seen
        seen.add(digest)
        lines = label.read_text().splitlines()
        for line in lines:
            values = line.split()
            assert len(values) == 5 and values[0] == '0' and all(0 <= float(v) <= 1 for v in values[1:])
        positives += len(lines)
        rel_image, rel_label = f"images/test/{row['page_id']}.jpg", f"labels/test/{row['page_id']}.txt"
        for original, relative in [(image, rel_image), (label, rel_label)]:
            target = out / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(original, target)
            assert sha(original) == sha(target)
        rows.append(dict(row, image=rel_image, label=rel_label, split='test',
                         label_sha256=sha(label), decoded_rgb_sha256=digest, negative=not lines))
    assert positives == 3
    limitations = ['Only three positive and two shrub-negative photos; limited whole-individual public pilot.',
        'Eight ambiguous originals remain TEST-reserved, unscored; source-content decisions precede predictions.',
        'No GPS for positive photos; unknown cross-author physical-tree aliases cannot be excluded completely.',
        'No PICO images, small/partial/forest reliability or end-to-end acceptance established.']
    audit = dict(names=['Tree'], samples=rows, held_samples=held, unique_images=5,
        sources=[dict(id='commons', license='Per-image CC BY/CC BY-SA retained in each sample')],
        counts=dict(train=0, val=0, test=5), training_approved=False, evaluation_ready=True,
        limitations=limitations, scope='Limited TEST-only pilot, not broad application acceptance.')
    (out / 'audit.json').write_text(json.dumps(audit, indent=2))
    (out / 'data.yaml').write_text(json.dumps(dict(path=out.as_posix(), test='images/test', names=['Tree']), indent=2))
    paths = set(evidence + snapshots + [pre, model, out / 'audit.json', out / 'data.yaml', Path(__file__).resolve(),
        ROOT / 'Tools/MultiScene/evaluate_mapped.py', ROOT / 'Tools/MultiScene/dataset_paths.py'])
    paths.update(out / row[key] for row in rows for key in ['image', 'label'])
    plan = dict(model=model.relative_to(ROOT).as_posix(), weight_sha256=expected,
        data=out.relative_to(ROOT).as_posix(), dataset_sha256=sha(out / 'audit.json'),
        frozen_inputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in sorted(paths)],
        split='test', images=5, ground_truth_instances=3, operating_confidence=.65, imgsz=640,
        rect=True, nms_iou=.7, max_det=8, allowed_classes=['Tree'], gates=dict(precision_min=.9, recall_min=.8),
        limitations=limitations, deployment_accepted=False,
        policy='One frozen model/profile; no TEST-based checkpoint/threshold/label/image changes.')
    plan_path.write_text(json.dumps(plan, indent=2))
    print(json.dumps(dict(status='FROZEN_LIMITED_TEST_READY', images=5, instances=3, frozen_inputs=len(paths),
                          dataset_sha256=plan['dataset_sha256'])))


if __name__ == '__main__':
    main()
