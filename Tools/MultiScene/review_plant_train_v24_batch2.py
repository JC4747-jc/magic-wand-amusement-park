"""Record actual full-original review for the second new TRAIN source batch."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REVIEWS = {
    '3f3d313f206cfc91': ('INSTANCE_REVIEW_REQUIRED', 'House porch with several partly occluded and distant planted containers; surrounding palms/trees rooted in ground are not container targets.'),
    '3f8dc73efefb2744': ('INSTANCE_REVIEW_REQUIRED', 'Plant bench: three front pots, three back containers and a separate floor pot appear visible. Verify middle-back shared container boundary; inverted empty red vessel below is not planted.'),
    '41b8e47200872ffb': ('HOLD_WREATH_SCOPE', 'Floral wreath on red door and ground/wall ivy; cut/artificial/live wreath status is unresolved, with no conventional planted container.'),
    '41e1bfea84eb0de0': ('HOLD_COIR_PLANTER_SCOPE', 'Succulents assembled in one shared hanging coir/metal-frame planting medium. Confirm class convention for this unusual container before labeling; do not label each rosette independently.'),
    '460a055d587fc039': ('HOLD_ROOTED_OR_CUT_SCOPE', 'Toy rabbits in cups; purple flowers at right in a cup-like vessel have unresolved rooted versus cut status. Not an empty negative.'),
    '4762cb439452cf70': ('AUXILIARY_ANNOTATION_REVIEW_REQUIRED', 'Detached flowers float in one water-filled stone basin. Rooted target absent; confirm auxiliary convention for floating floral display versus background negative.'),
    '4dab78f1c29c5edb': ('POSITIVE_ANNOTATION_REQUIRED', 'One small clipped flowering planted container at far left; two central trimmed shrubs are rooted in ground, not additional potted instances.'),
    '4eadfebb2e0ab5de': ('NEGATIVE_ANNOTATION_REQUIRED', 'Two suspended flowering bulbs have visibly exposed roots and no planted containers. They are living plants outside the PottedPlant container definition, not cut arrangements; mannequin is non-target.'),
    '4ef5b00d381faa07': ('POSITIVE_ANNOTATION_REQUIRED', 'Multicolored petunias share one visible soil-filled pot; one container instance, not one per flower/color.'),
    '509f1919fc44ced5': ('AUXILIARY_ANNOTATION_REQUIRED', 'One mixed cut-flower arrangement with visible stems in a narrow transparent glass vase; exclude from Unity plant target.'),
    '5205065e52d0d282': ('POSITIVE_ANNOTATION_REQUIRED', 'One small planted pot at far right by pool behind dogs. Ground vegetation and dogs are not container targets; inspect clipping and foliage extent.'),
    '54cd881a0cd21d6a': ('HOLD_DEPICTION_OR_ARTIFICIAL_SCOPE', 'Booth/interior design image with bamboo-like plant, illustrative human silhouette and synthetic-looking surfaces. Physical-photo versus rendering/artificial planting not established.'),
}


def main():
    source = ROOT / 'Datasets/plant-container-train-batch2-source-v24'
    manifest = source / 'manifest.json'
    data = json.loads(manifest.read_text())
    assert set(REVIEWS) == {row['original_id'] for row in data['samples']}
    rows = []
    for row in data['samples']:
        image = source / row['image']
        assert hashlib.sha256(image.read_bytes()).hexdigest() == row['sha256']
        decision, observation = REVIEWS[row['original_id']]
        rows.append(dict(**row, source_image=image.relative_to(ROOT).as_posix(), original_reviewed=True,
                         decision=decision, observation=observation, training_approved=False))
    out = ROOT / 'Reports/plant-container-train-v24-batch2-original-review.json'
    assert not out.exists()
    out.write_text(json.dumps(dict(samples=rows, source_manifest_sha256=hashlib.sha256(manifest.read_bytes()).hexdigest(),
        reserved_split='train', training_approved=False, labels_generated=False, model_predictions_used=False,
        scope='Twelve actual original-resolution reviews. Candidate decisions are not annotations or training approval; scope and instance ambiguities retained.'), indent=2))
    print(json.dumps(dict(originals_reviewed=len(rows), training_approved=False)))


if __name__ == '__main__':
    main()
