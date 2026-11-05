"""Manual, prediction-blind labels for twelve semantically resolved TEST images."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
BOXES = {
    '4c9172a8085252e7': [], '4f4f743e9a5868ff': [],
    '05e87c5b3af1ffda': [(0, 358, 181, 547, 483), (0, 295, 452, 397, 608), (0, 258, 535, 446, 811)],
    '095be420bf7977ec': [], '394572c3a670b113': [],
    '02ba70a37a4cba80': [],
    '0a8b367d8983474a': [(0, 650, 95, 768, 330)],
    '0a8bf9c7a090a29d': [(1, 34, 148, 744, 960)],
    '1064d513dc8bd213': [(0, 284, 270, 381, 378)],
    '14fceb7a490249a0': [(1, 193, 75, 650, 1015)],
    '179c9818e1216bba': [(0, 296, 477, 534, 649)],
    '1c27353042d7a2bf': [(1, 0, 108, 682, 811)],
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    sources = ['plant-independent-test-source-v23', 'plant-independent-test-container-source-v23',
               'plant-independent-test-box-source-v23']
    output = ROOT / 'Reports/plant-independent-test-v23-annotations'
    output.mkdir(exist_ok=False)
    (output / 'labels').mkdir()
    rows, held, source_hashes = [], [], []
    for name in sources:
        source = ROOT / 'Datasets' / name
        manifest = source / 'manifest.json'
        source_hashes.append(dict(path=manifest.relative_to(ROOT).as_posix(), sha256=sha(manifest)))
        for row in json.loads(manifest.read_text())['samples']:
            assert row['reserved_split'] == 'test'
            identifier = row['original_id']
            if identifier not in BOXES:
                held.append(dict(original_id=identifier, group=row['group'], reserved_split='test',
                                 source_manifest=manifest.relative_to(ROOT).as_posix(),
                                 reason='Unresolved container/rooting/instance context in original review; not labeled negative. No model predictions used.'))
                continue
            image_path = source / row['image']
            assert sha(image_path) == row['sha256']
            with Image.open(image_path) as original:
                image = original.convert('RGB')
            width, height = image.size
            drawing, lines = ImageDraw.Draw(image), []
            for category, x1, y1, x2, y2 in BOXES[identifier]:
                assert 0 <= x1 < x2 <= width and 0 <= y1 < y2 <= height
                lines.append(f'{category} {(x1+x2)/2/width:.8f} {(y1+y2)/2/height:.8f} {(x2-x1)/width:.8f} {(y2-y1)/height:.8f}')
                drawing.rectangle((x1, y1, x2-1, y2-1), outline='blue' if category == 0 else 'orange', width=3)
            label = output / 'labels' / f'{identifier}.txt'
            label.write_text('\n'.join(lines) + ('\n' if lines else ''))
            overlay = output / f'{identifier}.jpg'
            image.save(overlay)
            rows.append(dict(original_id=identifier, image=image_path.relative_to(ROOT).as_posix(),
                image_sha256=sha(image_path), label=label.relative_to(ROOT).as_posix(), label_sha256=sha(label),
                overlay=overlay.relative_to(ROOT).as_posix(), overlay_sha256=sha(overlay),
                group=row['group'], attribution=row['attribution'], reserved_split='test',
                boxes_class_xyxy=BOXES[identifier], original_reviewed=True, overlay_reviewed=False))
    assert len(rows) == 12 and len(held) == 14
    (output / 'draft.json').write_text(json.dumps(dict(samples=rows, held_samples=held,
        source_manifests=source_hashes, names=['PottedPlant', 'CutArrangement'],
        model_predictions_used=False, training_approved=False, evaluation_ready=False,
        limitations=['Only four positive images and six target instances; limited public pilot coverage.',
                     'Fourteen unresolved originals remain TEST-reserved and unscored; report their omission explicitly.',
                     'Does not establish PICO or broad tabletop-plant reliability.'],
        scope='Provisional manual annotations. Overlay review and full source/pixel clearance required before frozen evaluation.'), indent=2))
    print(json.dumps(dict(images=len(rows), target_instances=6, auxiliary_instances=3,
                          held=len(held), evaluation_ready=False)))


if __name__ == '__main__':
    main()
