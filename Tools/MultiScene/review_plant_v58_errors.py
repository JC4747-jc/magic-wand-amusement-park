"""Record actual visual inspection, without changing labels or model selection."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
NOTES = {
    '2eeeba800c582ff3ecef355e': ('annotation_scope_hold', 'Main bonsai matched. Extra small companion vessel at lower left predicted plant; its semantic scope remains unresolved, so this FP is not unqualified evidence of model error.'),
    '80af71df5d201ada8c3935bb': ('cut_flower_confusion', 'Sunflower bunches in white buckets receive near-full-image plant prediction. Existing negative scope retained; no prediction-driven auxiliary relabeling.'),
    '581cedc885d26e10455cf980': ('cross_class_confusion', 'White cut-flower arrangement in decorative silver urn has near-matching geometry but is predicted PottedPlant instead of auxiliary CutArrangement.'),
    '9fbc2444b8acf1398deaddd3': ('nested_duplicate_box', 'Left stacked planted containers have one matching large prediction plus extra smaller upper-pot prediction. Rabbit is outside predictions; instance grouping scope is retained.'),
    'f792813aeb8521eb5e9ba43f': ('hanging_miss_background_fp', 'Annotated suspended pot on left has no prediction. Separate red FP covers lower-left climbing ivy without a visible container.'),
    'a8d79cd1e9fd1cdb40e2541a': ('hanging_miss', 'Two full suspended flower baskets and one bottom-right partial basket all have GT boxes and no predictions.'),
    'ad4054926f2d2738dc4a8773': ('partial_target_miss', 'Main red soil herb pot matches; partial planted white container at lower left is missed.'),
    'f6064c57134f3277e5fe7b20': ('harvested_food_fp', 'Chopped leafy food on plate with hands and knife receives large plant prediction; no planted container visible.'),
    '467fb80fca7896af512309a0': ('harvested_food_fp', 'Loose harvested spinach on plate predicted as plant; no planted container visible.'),
    '0913dea122b9e30e10a04073': ('harvested_market_fp', 'Two red plant predictions on harvested leafy produce in market cartons; no soil-planted pot visible.'),
    'e890004cc7bf2354f7bdc5f3': ('harvested_market_fp', 'Two large red predictions cover harvested leafy bundles in market bowls/buckets and surrounding scene; no planted pot visible.'),
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    folder = ROOT / 'Reports/plant-v58-runtime-selected/final-val'
    report_path = folder / 'report.json'
    report = json.loads(report_path.read_text(encoding='utf-8'))
    rows = {Path(r['image'].replace('\\', '/')).stem: r for r in report['per_image']
            if r['false_positive'] or r['false_negative']}
    assert rows.keys() == NOTES.keys()
    reviewed = []
    for key, (category, note) in NOTES.items():
        overlay = folder / f'error-{key}.jpg'
        reviewed.append(dict(image=rows[key]['image'], category=category, observation=note,
                             counts={k: rows[key][k] for k in ('ground_truth', 'true_positive', 'false_positive', 'false_negative')},
                             overlay=overlay.relative_to(ROOT).as_posix(), overlay_sha256=sha(overlay),
                             actually_viewed=True))
    result = dict(status='ALL11_SELECTED_WEIGHT_ERROR_OVERLAYS_VISUALLY_REVIEWED',
                  report_sha256=sha(report_path), weight_sha256=report['sha256'], reviewed=reviewed,
                  labels_changed=False, checkpoint_reselected=False, deployment_accepted=False,
                  test_used=False, pico_validated=False,
                  next_action='Compare fixed selected-weight TRAIN fit with full VAL under identical runtime policy before further training. Preserve annotation scope holds and frozen validation labels.')
    out = folder.parent / 'error-review.json'
    assert not out.exists(), 'Preserve existing review'
    out.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(dict(status=result['status'], reviewed=len(reviewed), report_sha256=result['report_sha256'])))


if __name__ == '__main__':
    main()
