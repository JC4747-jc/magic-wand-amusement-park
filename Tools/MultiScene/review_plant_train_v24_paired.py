"""Source-only review of paired upstream Houseplant/Flowerpot TRAIN candidates."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REVIEWS = {
    '55076cc6edd1b1af': ('HOLD_MIXED_INSTANCE_SCOPE', 'Many rooted bulb pots surround a central birch-like floral vessel with mixed branches/blooms. Central rooted-versus-cut scope and all container instances require review.'),
    '58a0c796e16d767e': ('AUXILIARY_ANNOTATION_REQUIRED', 'One rose bouquet with cut stems visible in transparent glass vase; fallen loose petal is not a separate arrangement.'),
    '595aefba4db8aeb3': ('NEGATIVE_ANNOTATION_REVIEW_REQUIRED', 'Decorative mixed floral display has porcelain/textile-like petals, pearl decorations and unnatural colors; verify artificial material before accepting a nonliving-flower negative.'),
    '5c4ac78e1ecc93ee': ('AUXILIARY_ANNOTATION_REVIEW_REQUIRED', 'Azalea-like branches in a narrow-neck ornamental vase; appears a cut-branch arrangement, but rooted versus decorative context needs confirmation.'),
    '5d1838e8161716e7': ('INSTANCE_REVIEW_REQUIRED', 'Two clearly planted blue patio pots plus a possibly planted clipped vessel at far left behind/near chair; inspect edge rather than omit possible instance.'),
    '5e9af8ca6675ddd9': ('POSITIVE_ANNOTATION_REQUIRED', 'One shallow white soil-filled planter with flowering geraniums and small foliage by doorway. Decorative source border does not add another instance.'),
    '5f4c6cada22172c3': ('NEGATIVE_ANNOTATION_REQUIRED', 'Three-panel collage of felt/wool mushroom sculptures in a ceramic bowl, visibly textile fibers; no living planted container or cut flowers.'),
    '6119af9658c2a188': ('INSTANCE_REVIEW_REQUIRED', 'One potted dracaena at left of martial-arts subject, and a small clipped flower vase on upper-right shelf. Include all applicable classes after confirming vase content.'),
    '622e074fe98116c3': ('HOLD_DEPICTION_AND_ROOTING_SCOPE', 'Still-life/art-like image of empty vases and a glass of leafy stems in water. Physical photo versus reproduction and rooted propagation versus cut foliage not established.'),
    '668f30b11ecec6cf': ('POSITIVE_ANNOTATION_REQUIRED', 'One partly person-occluded rectangular white planter with geranium foliage/blooms on sill; visible container and stems belong to one shared planter.'),
    '6748701b7c7a38c8': ('INSTANCE_REVIEW_REQUIRED', 'Foreground moss-covered bonsai/root planting in shared bowl, separate small right fern pot, clipped left pot and possible background plant. Complete associations before annotation.'),
    '680c9d8daa791d9a': ('POSITIVE_ANNOTATION_REQUIRED', 'One clear soil-filled white pot with green leaves; beverage bottles are non-targets. Tall leaf is clipped by top image boundary.'),
}


def main():
    source = ROOT / 'Datasets/plant-container-train-paired-source-v24'
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
    out = ROOT / 'Reports/plant-container-train-v24-paired-original-review.json'
    assert not out.exists()
    out.write_text(json.dumps(dict(samples=rows, source_manifest_sha256=hashlib.sha256(manifest.read_bytes()).hexdigest(),
        reserved_split='train', training_approved=False, labels_generated=False, model_predictions_used=False,
        scope='Twelve full original images actually inspected. Paired upstream annotations only informed discovery, not project labels. Holds and instance ambiguities retained.'), indent=2))
    print(json.dumps(dict(originals_reviewed=len(rows), training_approved=False)))


if __name__ == '__main__':
    main()
