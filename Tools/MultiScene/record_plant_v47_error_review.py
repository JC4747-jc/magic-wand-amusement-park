"""Record actual visual inspection without changing labels or acceptance."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
NOTES = {
 '2eeeba800c582ff3ecef355e': ('UNRESOLVED_LABEL_SCOPE', 'Main bonsai correctly detected; small companion vessel predicted plant at .90. Rooted content remains unresolved from earlier publisher review. Preserve existing annotation and mark metric caveat; do not relabel from prediction.'),
 'db867d614aa06c5027d0ea72': ('NO_ACCEPTED_DETECTION', 'Large hanging flowering basket has no accepted detection at .35. Visible foliage occupies most image; not explained by tiny-target resolution alone.'),
 'f792813aeb8521eb5e9ba43f': ('WRONG_SPATIAL_SUPPORT', 'Visible suspended planted container missed. One accepted prediction spans nearly whole frame including ground ivy/tree; another covers lower-right ground foliage. These are false spatial supports, not near-correct boxes for hanging pot.'),
 'a8d79cd1e9fd1cdb40e2541a': ('NO_ACCEPTED_DETECTION', 'Two complete hanging baskets plus lower-right partial basket have no accepted detections. Cropped third target was included before model inference.'),
 'd6a4f5f64a10ff59fe54d62c': ('EMPTY_VESSEL_CONFUSION', 'Decorative faceted empty glass vase is predicted PottedPlant .93 despite no visible vegetation. Container appearance alone is insufficient.'),
 'ad4054926f2d2738dc4a8773': ('PARTIAL_INSTANCE_MISS', 'Main soil-grown herb in red container is correctly detected; bottom-left partial white planted container missed. Maintain pre-inference partial-instance annotation.'),
 'f6064c57134f3277e5fe7b20': ('HARVESTED_FOOD_CONFUSION', 'Chopped loose food greens on plate with knife/hands predicted PottedPlant .92 nearly full-frame; no rooted container. High-confidence semantic false positive.'),
}


def main():
 folder = ROOT / 'Reports/plant-v47-prospective-baseline'
 dest = folder / 'error-review.json'
 assert not dest.exists()
 report = json.loads((folder / 'report.json').read_text())
 reviewed = []
 for error in report['errors']:
  stem = Path(error['example']).stem.removeprefix('error-')
  category, observation = NOTES[stem]
  image = folder / error['example']
  reviewed.append(dict(error, category=category, observation=observation,
                       overlay_actually_viewed=True,
                       overlay_sha256=hashlib.sha256(image.read_bytes()).hexdigest()))
 assert len(reviewed) == 7
 result = dict(report_sha256=hashlib.sha256((folder / 'report.json').read_bytes()).hexdigest(),
  reviewed_errors=reviewed, labels_changed=False, deployment_accepted=False,
  conclusions=['Hanging failures include large targets and spatial confusion; do not assume image resolution alone.',
               'Empty vessel and harvested-food errors exceed .9 confidence; a small threshold increase cannot be presumed sufficient.',
               'Need representative source-separated TRAIN coverage and bounded optimization assessment; current evidence does not isolate one cause.'],
  next_experiment_constraints=['Keep all prospective VAL images out of TRAIN.',
    'Preserve original13 regression cohort and all historical rejections.',
    'Do not train against unresolved companion-vessel annotation.',
    'No deployment without independent TEST and PICO validation.'])
 dest.write_text(json.dumps(result, indent=2), encoding='utf-8')
 print(json.dumps(dict(reviewed=7, labels_changed=False, deployment_accepted=False)))


if __name__ == '__main__':
 main()
