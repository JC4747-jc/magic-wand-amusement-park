"""Release the manually reviewed v25 pilot before any candidate inference."""
import hashlib
import json
import shutil
from pathlib import Path

from source_author_identity import reservation_author_keys
from dataset_pipeline import decoded_pixel_digest

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    output = ROOT / 'Datasets/plant-independent-test-v25'
    plan_path = ROOT / 'Reports/plant-independent-test-v25-evaluation-plan.json'
    assert not output.exists() and not plan_path.exists()
    reviews = [ROOT / 'Reports/plant-independent-test-v25-annotations/reviewed-six.json',
               ROOT / 'Reports/plant-independent-test-v25-negative-annotations/reviewed.json']
    frozen_evidence, selected, held, authors = [], [], [], set()
    for path in reviews:
        review = read(path)
        assert not review['model_predictions_used']
        frozen_evidence.append(path)
        for item in review['source_manifests']:
            source = ROOT / Path(item['path'].replace('\\', '/'))
            assert sha(source) == item['sha256']
            frozen_evidence.append(source)
        selected.extend(review['samples'])
        held.extend(review.get('held_original_ids', []))
        held.extend(row['original_id'] for row in review.get('held_samples', []))
    assert len(selected) == 17 and len(held) == 7
    for suffix, source_name in [('', 'plant-independent-test-source-v25'),
                                ('-negative', 'plant-independent-test-negative-source-v25')]:
        source = ROOT / 'Datasets' / source_name
        metadata_path = ROOT / f'Reports/plant-independent-test-v25{suffix}-metadata-audit.json'
        pixels_path = ROOT / f'Reports/plant-independent-test-v25{suffix}-pixel-audit.json'
        metadata, pixels = read(metadata_path), read(pixels_path)
        assert metadata['source_manifest_sha256'] == pixels['source_manifest_sha256'] == sha(source / 'manifest.json')
        for item in metadata['inputs']:
            assert sha(ROOT / item['path']) == item['sha256']
        own = {f'Datasets/{source_name}/manifest.json', f'Datasets/{source_name}/selection.json',
               f'Reports/plant-independent-test-v25{suffix}-selection.json'}
        assert all(row['path'] in own and row.get('split') not in ('train', 'val')
                   and row.get('reserved_split') not in ('train', 'val') for row in metadata['records'])
        assert pixels['scanned_paths'] == pixels['enumerated_paths'] and pixels['scanned_paths'] > 16000
        assert not any(pixels[key] for key in ['errors', 'matches', 'internal_matches'])
        frozen_evidence.extend([metadata_path, pixels_path])
    model = ROOT / 'object-detection-demo/weights/candidates/plant-reviewed-v24/best.pt'
    expected = '34bfbf18ed65e0d87c6de44a88d26db6a77cd267e974d6cb3aeb484a19c52d48'
    assert sha(model) == expected
    # Validate every byte and annotation before creating the immutable dataset.
    checked, counts, pixel_ids = [], [0, 0], set()
    for row in selected:
        assert row['original_reviewed'] and row['overlay_reviewed'] and row['reserved_split'] == 'test'
        keys = reservation_author_keys(row)
        assert keys and not keys & authors
        authors.update(keys)
        paths = {key: ROOT / row[key].replace('\\', '/') for key in ['image', 'label', 'overlay']}
        for key, path in paths.items():
            assert path.resolve().is_relative_to(ROOT) and sha(path) == row[key + '_sha256']
        pixel = decoded_pixel_digest(paths['image'])
        assert pixel not in pixel_ids
        pixel_ids.add(pixel)
        for line in paths['label'].read_text().splitlines():
            values = line.split()
            assert len(values) == 5 and values[0] in ('0', '1')
            coords = list(map(float, values[1:]))
            assert all(0 <= value <= 1 for value in coords) and coords[2] > 0 and coords[3] > 0
            counts[int(values[0])] += 1
        checked.append((row, paths, pixel))
    assert counts == [4, 2]
    (output / 'images/test').mkdir(parents=True)
    (output / 'labels/test').mkdir(parents=True)
    samples = []
    for row, paths, pixel in checked:
        identifier = sha(paths['image'])[:24]
        image, label = f'images/test/{identifier}.jpg', f'labels/test/{identifier}.txt'
        shutil.copy2(paths['image'], output / image)
        shutil.copy2(paths['label'], output / label)
        samples.append(dict(original_id=row['original_id'], image=image, label=label,
            sha256=sha(output / image), label_sha256=sha(output / label), decoded_rgb_sha256=pixel,
            source='openimages-v7', group=row['group'], attribution=row['attribution'], split='test',
            negative=not paths['label'].read_text().strip(), source_image=row['image'], source_label=row['label']))
    limitations = ['Only four positive images and four target instances; limited public pilot, not broad acceptance.',
                   'Seven ambiguous originals remain TEST-reserved and unscored; selection used content only.',
                   'No positive PICO camera evaluation or end-to-end device acceptance.']
    audit = dict(samples=samples, names=['PottedPlant', 'CutArrangement'], unique_images=17,
        sources=[dict(id='openimages-v7', url='https://storage.googleapis.com/openimages/web/download_v7.html',
                      license='Per-image CC BY2.0; attribution retained')],
        counts=dict(train=0, val=0, test=17), held_original_ids=held,
        training_approved=False, evaluation_ready=True, limitations=limitations,
        scope='Independent source-author-separated public TEST pilot; manual labels frozen before inference.')
    (output / 'audit.json').write_text(json.dumps(audit, indent=2))
    (output / 'data.yaml').write_text(json.dumps(dict(path=str(output), test='images/test', names=audit['names']), indent=2))
    paths = frozen_evidence + [output / 'audit.json', output / 'data.yaml', model,
        ROOT / 'Tools/MultiScene/evaluate_mapped.py', ROOT / 'Tools/MultiScene/dataset_paths.py',
        ROOT / 'Tools/MultiScene/source_author_identity.py', Path(__file__).resolve()]
    paths.extend(output / row[key] for row in samples for key in ['image', 'label'])
    plan = dict(model=model.relative_to(ROOT).as_posix(), weight_sha256=expected,
        data=output.relative_to(ROOT).as_posix(), dataset_sha256=sha(output / 'audit.json'),
        frozen_inputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in paths],
        split='test', images=17, ground_truth_instances=4, operating_confidence=.35,
        imgsz=640, rect=True, nms_iou=.7, max_det=8, allowed_classes=['PottedPlant'],
        gates=dict(precision_min=.9, recall_min=.8), limitations=limitations,
        policy='One frozen candidate; no TEST threshold search, checkpoint selection, relabeling or prediction-based exclusions.',
        deployment_accepted=False)
    plan_path.write_text(json.dumps(plan, indent=2))
    print(json.dumps(dict(status='FROZEN_TEST_PILOT_READY', images=17, target_instances=4,
                          dataset_sha256=plan['dataset_sha256'], frozen_inputs=len(paths))))


if __name__ == '__main__':
    main()
