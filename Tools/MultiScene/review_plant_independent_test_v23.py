"""Record actual source-only review; no model predictions or automatic boxes."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REVIEWS = {
    '05e87c5b3af1ffda': ('POSITIVE_ANNOTATION_REQUIRED', 'Three separate wall-mounted cans with succulents; shared foliage in each can belongs to its container instance.'),
    '095be420bf7977ec': ('NEGATIVE_ANNOTATION_REQUIRED', 'Decorated ceramic teapot, no vegetation.'),
    '394572c3a670b113': ('NEGATIVE_ANNOTATION_REQUIRED', 'Balcony with stools, no container plants visible.'),
    '431b4faaaf8350f7': ('HOLD_CONTAINER_NATURE', 'Small branch/plant in translucent vessel at lower right; rooted plant versus cut branch unclear.'),
    '02ba70a37a4cba80': ('NEGATIVE_ANNOTATION_REQUIRED', 'Harvested culinary greens piled in a kitchen bowl, not a rooted container plant or decorative flower arrangement.'),
    '0a8b367d8983474a': ('POSITIVE_ANNOTATION_REQUIRED', 'One clipped blue container with flowering plant at upper right behind rabbit; rabbit and ground foliage are not target instances.'),
    '0a8bf9c7a090a29d': ('CUT_ARRANGEMENT_ANNOTATION_REQUIRED', 'Three cut roses and filler flowers in a single transparent water vase.'),
    '0afc393104e371e6': ('INSTANCE_REVIEW_REQUIRED', 'Foreground and background snake-plant bowls; low-resolution leaves and overlapping/clipped distant bowls require exact instance review.'),
    '0d0ab533f365188d': ('INSTANCE_REVIEW_REQUIRED', 'Riverfront houses with numerous tiny/clipped plant pots; full original reviewed but complete container-to-foliage association not yet established.'),
    '1064d513dc8bd213': ('POSITIVE_ANNOTATION_REQUIRED', 'One small herb pot centered on windowsill, distinct from ground garden and trellis vines.'),
    '10f0ea63ac0ed9a2': ('HOLD_CONTAINER_BOUNDARY', 'Seedlings in soil/raised-bed setting; foreground container boundary not established and background boxes unclear.'),
    '1396b5d7fb9e3cdb': ('HOLD_ROOTING_CONTEXT', 'Ivy on fence; rooting/container context not visible.'),
    '14fceb7a490249a0': ('CUT_ARRANGEMENT_ANNOTATION_REQUIRED', 'Cut lavender stems in a single transparent water jar.'),
    '179c9818e1216bba': ('POSITIVE_ANNOTATION_REQUIRED', 'One rooftop/window planter containing flowering geraniums; one shared container instance.'),
    '1a77c62a6eb7e3e3': ('HOLD_INSTANCE_BOUNDARY', 'Hand holds removed herb rootball above a partially visible planted pot; target extent/association requires further review.'),
    '1c27353042d7a2bf': ('CUT_ARRANGEMENT_ANNOTATION_REQUIRED', 'One mixed cut-flower arrangement with visible stems in rectangular water vase.'),
}


def main():
    sources = ['plant-independent-test-container-source-v23', 'plant-independent-test-box-source-v23']
    rows, manifests = [], []
    for name in sources:
        source = ROOT / 'Datasets' / name
        path = source / 'manifest.json'
        manifests.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        for row in json.loads(path.read_text())['samples']:
            image = source / row['image']
            assert hashlib.sha256(image.read_bytes()).hexdigest() == row['sha256']
            decision, observation = REVIEWS[row['original_id']]
            rows.append(dict(**row, source_image=image.relative_to(ROOT).as_posix(),
                             original_reviewed=True, decision=decision, observation=observation,
                             model_predictions_used=False))
    assert {row['original_id'] for row in rows} == set(REVIEWS)
    out = ROOT / 'Reports/plant-independent-test-v23-context-original-review.json'
    assert not out.exists()
    out.write_text(json.dumps(dict(source_manifests=manifests, samples=rows,
        reserved_split='test', training_approved=False, evaluation_ready=False,
        scope='Actual sixteen full-original reviews. No labels or model scores generated. Complex instance cases remain pending; they are not silently treated as negatives or discarded based on predictions.'), indent=2))
    print(json.dumps(dict(originals_reviewed=len(rows), evaluation_ready=False)))


if __name__ == '__main__':
    main()
