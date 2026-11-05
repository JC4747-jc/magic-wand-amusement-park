"""Record original-resolution TRAIN candidate review, without adopting upstream boxes."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REVIEWS = {
    '1d4a192a1141f7f6': ('INSTANCE_REVIEW_REQUIRED', 'White orchid in central black pot; clipped decorated vessel and adjacent foliage need association review. Do not label only the flowers.'),
    '1dd5a63166f05223': ('INSTANCE_REVIEW_REQUIRED', 'Greenhouse garden with many distinct planted containers, tiny pots and occlusions; enumerate all visible planted containers before release.'),
    '1e3bb22ec9731e1f': ('INSTANCE_REVIEW_REQUIRED', 'People with several partly occluded palm pots on both sides; include clipped pots and assign foliage carefully.'),
    '2307da683364847f': ('INSTANCE_REVIEW_REQUIRED', 'Geranium in black lined planter and foreground succulent in orange container; garden gnome and empty ceramic vessels are non-targets. Inspect right boundary for additional instances.'),
    '249b4c5b70fa891b': ('POSITIVE_ANNOTATION_REQUIRED', 'One clearly visible central planted container among ground garden vegetation; separate container plant from ground foliage.'),
    '28f24452feefb94a': ('HOLD_CONTAINER_ASSOCIATION', 'Indoor pond with ground/bed ferns and a clipped right-hand palm container; container ownership/instance extent unclear.'),
    '29c0986a180374df': ('HOLD_CONTAINER_ASSOCIATION', 'Vines drape over wooden cradle around partly concealed plant pots; shared versus separate container roots unresolved.'),
    '2af6a2a751af08b5': ('INSTANCE_REVIEW_REQUIRED', 'Dog close-up with blurred left and right background container plants; do not treat as an empty negative.'),
    '2c365c9b42ca2d87': ('HOLD_FLORAL_SCOPE', 'Mixed red/white floral decoration close-up; rooting, cut-stem and possible artificial-flower status not established.'),
    '2e96a848715c869c': ('HOLD_ROOTING_SCOPE', 'Garden gnome holds pot with yellow squash blossoms; real rooted plant versus placed cut blossom unclear.'),
    '32cebad428009a99': ('NEGATIVE_ANNOTATION_REQUIRED', 'Cat beside bowl containing decorative glass marbles; no live plant in that vessel.'),
}


def main():
    source = ROOT / 'Datasets/plant-container-train-source-v24'
    manifest = source / 'manifest.json'
    data = json.loads(manifest.read_text())
    assert {row['original_id'] for row in data['samples']} == set(REVIEWS)
    rows = []
    for row in data['samples']:
        image = source / row['image']
        assert hashlib.sha256(image.read_bytes()).hexdigest() == row['sha256']
        decision, observation = REVIEWS[row['original_id']]
        rows.append(dict(**row, source_image=image.relative_to(ROOT).as_posix(), original_reviewed=True,
                         decision=decision, observation=observation, training_approved=False))
    result = dict(samples=rows, acquisition_failures=data['failures'],
        source_manifest_sha256=hashlib.sha256(manifest.read_bytes()).hexdigest(),
        selector_sha256=hashlib.sha256((ROOT / 'Tools/MultiScene/select_plant_independent_test_v23.py').read_bytes()).hexdigest(),
        identity_helper_sha256=hashlib.sha256((ROOT / 'Tools/MultiScene/source_author_identity.py').read_bytes()).hexdigest(),
        reserved_split='train', training_approved=False, labels_generated=False,
        scope='Eleven full originals actually reviewed; complex examples retained for instance annotation. No model predictions used and no old TEST image/author reassigned.')
    out = ROOT / 'Reports/plant-container-train-v24-original-review.json'
    assert not out.exists()
    out.write_text(json.dumps(result, indent=2))
    print(json.dumps(dict(originals_reviewed=len(rows), failed_downloads=len(data['failures']), training_approved=False)))


if __name__ == '__main__':
    main()
