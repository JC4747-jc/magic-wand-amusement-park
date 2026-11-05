"""Release a reviewed TEST-only pilot and freeze one model/evaluation contract."""
import hashlib
import json
import shutil
from pathlib import Path
from source_author_identity import author_identity
from dataset_pipeline import decoded_pixel_digest

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    review_path = ROOT / 'Reports/plant-independent-test-v23-annotations/reviewed-author-separated.json'
    clearance_path = ROOT / 'Reports/plant-independent-test-v23-author-separated-clearance.json'
    review, clearance = [json.loads(path.read_text()) for path in [review_path, clearance_path]]
    assert clearance['status'] == 'SOURCE_AND_PIXEL_CLEARANCE_PASSED'
    assert clearance['review_sha256'] == sha(review_path) and not clearance['unexpected_historical_hits']
    for item in clearance['inputs'] + clearance['pixel_reports']:
        assert sha(ROOT / item['path']) == item['sha256']
    for item in review['source_manifests']:
        assert sha(ROOT / item['path']) == item['sha256']
    selected = review['samples']
    assert len(selected) == 11 and len({author_identity(row['group']) for row in selected}) == 11
    model = ROOT / 'object-detection-demo/weights/candidates/plant-reviewed-v22/best.pt'
    expected_weight = 'e817453878277c6e80171bac90b8212a2e1ea69ac4ff230ca69e6037183e6a43'
    assert sha(model) == expected_weight
    output = ROOT / 'Datasets/plant-independent-test-v23'
    plan_path = ROOT / 'Reports/plant-independent-test-v23-evaluation-plan.json'
    assert not output.exists() and not plan_path.exists()
    (output / 'images/test').mkdir(parents=True)
    (output / 'labels/test').mkdir(parents=True)
    rows, counts = [], [0, 0]
    for row in selected:
        assert row['original_reviewed'] and row['overlay_reviewed'] and row['reserved_split'] == 'test'
        image, label, overlay = [ROOT / row[key] for key in ['image', 'label', 'overlay']]
        assert sha(image) == row['image_sha256'] and sha(label) == row['label_sha256']
        assert sha(overlay) == row['overlay_sha256']
        for line in label.read_text().splitlines():
            values = line.split()
            assert len(values) == 5 and values[0] in ('0', '1')
            assert all(0 <= float(value) <= 1 for value in values[1:])
            counts[int(values[0])] += 1
        identifier = sha(image)[:24]
        relative_image, relative_label = f'images/test/{identifier}.jpg', f'labels/test/{identifier}.txt'
        shutil.copy2(image, output / relative_image)
        shutil.copy2(label, output / relative_label)
        assert sha(output / relative_image) == row['image_sha256']
        assert sha(output / relative_label) == row['label_sha256']
        rows.append(dict(original_id=row['original_id'], image=relative_image, label=relative_label,
            sha256=sha(image), label_sha256=sha(label), decoded_rgb_sha256=decoded_pixel_digest(image),
            source='openimages-v7', group=row['group'], attribution=row['attribution'], split='test',
            negative=not label.read_text().strip(), source_image=row['image'], source_label=row['label']))
    assert counts == [6, 3]
    limitations = ['Only four positive images and six target instances; limited public pilot coverage.',
                   'Fourteen ambiguous source images remain TEST-reserved and unscored.',
                   'One author-conflicted negative image rejected before inference; all lineage retained.',
                   'No PICO acceptance or broad tabletop-plant reliability established.']
    audit = dict(samples=rows, names=['PottedPlant', 'CutArrangement'], unique_images=len(rows),
        sources=[dict(id='openimages-v7', url='https://storage.googleapis.com/openimages/web/download_v7.html',
                      license='Per-image CC BY2.0 metadata and attribution retained')],
        counts=dict(train=0, val=0, test=11), training_approved=False, evaluation_ready=True,
        review_sha256=sha(review_path), clearance_sha256=sha(clearance_path),
        held_samples=review['held_samples'], rejected_samples=review['excluded_author_conflicts'],
        limitations=limitations, scope='TEST-only public pilot; source-content selection and manual labels frozen before model inference.')
    (output / 'audit.json').write_text(json.dumps(audit, indent=2))
    (output / 'data.yaml').write_text(json.dumps(dict(path=str(output), test='images/test', names=audit['names']), indent=2))
    frozen = [dict(path=(output / 'audit.json').relative_to(ROOT).as_posix(), sha256=sha(output / 'audit.json')),
              dict(path=(output / 'data.yaml').relative_to(ROOT).as_posix(), sha256=sha(output / 'data.yaml'))]
    for row in rows:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            frozen.append(dict(path=(output / row[key]).relative_to(ROOT).as_posix(), sha256=row[digest]))
    for path in [model, ROOT / 'Tools/MultiScene/evaluate_mapped.py', ROOT / 'Tools/MultiScene/dataset_paths.py',
                 ROOT / 'Tools/MultiScene/source_author_identity.py']:
        frozen.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
    plan = dict(model=model.relative_to(ROOT).as_posix(), weight_sha256=expected_weight,
        data=output.relative_to(ROOT).as_posix(), dataset_sha256=sha(output / 'audit.json'), frozen_inputs=frozen,
        split='test', images=11, ground_truth_instances=6, operating_confidence=.35, imgsz=640,
        rect=True, nms_iou=.7, max_det=8, allowed_classes=['PottedPlant'],
        gates=dict(precision_min=.9, recall_min=.8), limitations=limitations,
        policy='One frozen candidate. No threshold search, checkpoint choice, relabeling or image removal based on TEST predictions.',
        deployment_accepted=False)
    plan_path.write_text(json.dumps(plan, indent=2))
    print(json.dumps(dict(status='FROZEN_TEST_PILOT_READY', images=11, target_instances=6,
                          dataset_sha256=plan['dataset_sha256'], deployment_accepted=False)))


if __name__ == '__main__':
    main()
