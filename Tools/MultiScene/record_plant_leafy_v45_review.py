"""Persist pre-inference original review; never infer labels from category names."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Datasets/plant-leafy-val-source-v45'
OUT = ROOT / 'Reports/plant-leafy-val-v45-original-review.json'
NOTES = {
    147459459: ('HOLD_DOMAIN', 'Ground-growing foliage against wall with soil; not harvested-leaf coverage.'),
    101386677: ('NEGATIVE_CANDIDATE', 'Food preparation: loose/chopped greens, peppers, hand and knife; no identifiable planted container or cut-flower arrangement.'),
    110399676: ('HOLD_SHARED_SCENE', 'Harvested leafy pile; visually same physical scene as 110399224 despite different recorded authors. Hold both pending provenance/session resolution.'),
    110399224: ('HOLD_SHARED_SCENE', 'Harvested leafy pile; visually same physical scene as 110399676. Author difference does not establish independent source.'),
    77153479: ('NEGATIVE_CANDIDATE', 'Loose spinach leaves with cut stems on plate, white background; no planted container or cut-flower arrangement.'),
    75729987: ('NEGATIVE_CANDIDATE', 'Market produce table: harvested greens and cauliflower in produce baskets; no identifiable soil-grown potted plant or cut-flower arrangement.'),
    164035483: ('HOLD_DOMAIN', 'Ground-growing foliage and soil; not harvested-leaf coverage.'),
    147571749: ('NEGATIVE_CANDIDATE', 'Market cut/bundled greens and sliced produce in bowls/bucket; no identifiable planted container or cut-flower arrangement.'),
}


def main():
    if OUT.exists():
        raise FileExistsError('Immutable review already exists')
    manifest = SOURCE / 'manifest.json'
    rows = json.loads(manifest.read_text(encoding='utf-8'))['samples']
    reviewed = []
    for row in rows:
        status, note = NOTES[row['page_id']]
        digest = hashlib.sha256((SOURCE / row['image']).read_bytes()).hexdigest()
        assert digest == row['sha256']
        reviewed.append(dict(page_id=row['page_id'], image=row['image'], image_sha256=digest,
                             artist=row['artist'], license=row['license'], status=status,
                             observation=note, original_actually_viewed=True,
                             annotation_approved=False))
    result = dict(source_manifest_sha256=hashlib.sha256(manifest.read_bytes()).hexdigest(),
                  reserved_split='val', model_inference_performed=False, training_approved=False,
                  review_basis='All eight full originals viewed before inference in preceding acquisition turn.',
                  limitations=['Single-assistant visual review; provenance and pixel audits remain separate gates.',
                               'Negative candidates are not released labels.'], samples=reviewed)
    OUT.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(dict(reviewed=len(reviewed), annotation_approved=False)))


if __name__ == '__main__':
    main()
