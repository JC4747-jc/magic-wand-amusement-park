"""Record actual original-image review and provisional manual TRAIN annotations."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
REVIEWS = {
    '872ed3904bb60f2c': ('HOLD_ARTIFICIAL_FLOWER_SCOPE', 'Textile-like petals in decorative glass stand; artificial floral display suspected. Do not assert live/cut flowers.', None),
    'f5d9be6f34b670fc': ('EXCLUDE_IRRELEVANT', 'Lollipop centerpiece in glass jar, visible photography watermark; no plants.', None),
    '004db87a4abb2595': ('DRAFT_EMPTY_NEGATIVE', 'Empty decorative ceramic ewer with painted flowers; no real vegetation.', []),
    '1569d5d54261fc3f': ('DRAFT_CUT_ARRANGEMENT', 'Pink tulip stems and leaves assembled in a single glass vase, lower vase clipped by image boundary.', [(1, 0, 74, 768, 1024)]),
    '217b8c8b3c114f05': ('DRAFT_EMPTY_NEGATIVE', 'Two empty decorated vessels; painted patterns are not vegetation.', []),
    '3d9e6b81b3e6a84c': ('DRAFT_EMPTY_NEGATIVE', 'Empty transparent turquoise glass vase with surface decoration.', []),
    '06e65f19fde56f05': ('EXCLUDE_IRRELEVANT', 'Shoes printed with flowers; no plants.', None),
    '17dc2f3aa0c87e39': ('EXCLUDE_IRRELEVANT', 'Historical botanical illustration rather than a camera photo of a physical arrangement.', None),
    '22d9e033dadee2ab': ('DRAFT_EMPTY_NEGATIVE', 'Empty painted ceramic ewer; flower motifs are surface decoration.', []),
    '279ded8ae1300207': ('EXCLUDE_IRRELEVANT', 'Floral-print dress on hanger; no plants.', None),
    '73a1ffb704222608': ('DRAFT_CUT_ARRANGEMENT', 'Carnations with visible cut stems in one transparent water vase; bow belongs to the arrangement.', [(1, 46, 45, 741, 996)]),
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    source = ROOT / 'Datasets/plant-cut-arrangement-source-v22'
    manifest = json.loads((source / 'manifest.json').read_text())
    assert set(REVIEWS) == {row['original_id'] for row in manifest['samples']}
    output = ROOT / 'Reports/plant-cut-arrangement-v22-review'
    output.mkdir(exist_ok=False)
    (output / 'labels').mkdir()
    rows = []
    for row in manifest['samples']:
        identifier = row['original_id']
        decision, observation, boxes = REVIEWS[identifier]
        path = source / row['image']
        assert sha(path) == row['sha256']
        result = dict(original_id=identifier, image=path.relative_to(ROOT).as_posix(),
                      image_sha256=sha(path), group=row['group'], attribution=row['attribution'],
                      original_reviewed=True, decision=decision, observation=observation,
                      reserved_split='train', training_approved=False)
        if boxes is not None:
            with Image.open(path) as original:
                image = original.convert('RGB')
            width, height = image.size
            drawing = ImageDraw.Draw(image)
            labels = []
            for category, x1, y1, x2, y2 in boxes:
                assert 0 <= x1 < x2 <= width and 0 <= y1 < y2 <= height
                labels.append(f'{category} {(x1+x2)/2/width:.8f} {(y1+y2)/2/height:.8f} {(x2-x1)/width:.8f} {(y2-y1)/height:.8f}')
                drawing.rectangle((x1, y1, x2-1, y2-1), outline='blue', width=3)
            label = output / 'labels' / f'{identifier}.txt'
            label.write_text('\n'.join(labels) + ('\n' if labels else ''))
            overlay = output / f'{identifier}.jpg'
            image.save(overlay)
            result.update(label=label.relative_to(ROOT).as_posix(), label_sha256=sha(label),
                          overlay=overlay.relative_to(ROOT).as_posix(), overlay_sha256=sha(overlay),
                          boxes_class_xyxy=boxes, overlay_reviewed=False)
        rows.append(result)
    (output / 'draft.json').write_text(json.dumps(dict(
        source_manifest_sha256=sha(source / 'manifest.json'), samples=rows,
        names=['PottedPlant', 'CutArrangement'], training_approved=False,
        remaining_checks=['Provisional overlay review', 'Full reservation and pixel duplicate clearance'],
        scope='Actual original reviews; provisional annotations only. No dataset release or training approval.'
    ), indent=2) + '\n')
    print(json.dumps(dict(originals_reviewed=len(rows), draft_auxiliary_images=2,
                          draft_empty_vase_negatives=4, held=1, excluded=4, training_approved=False)))


if __name__ == '__main__':
    main()
