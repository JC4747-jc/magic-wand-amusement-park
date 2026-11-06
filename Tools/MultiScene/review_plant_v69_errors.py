"""Preserve actual visual QA of selected v69 errors; do not relabel validation."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
NOTES={'2eeeba800c582ff3ecef355e': 'Main bonsai TP plus small lower-left companion-vessel FP; existing annotation-scope hold retained.', '581cedc885d26e10455cf980': 'White cut-flower arrangement in decorative silver urn misclassified as plant instead of CutArrangement.', '80af71df5d201ada8c3935bb': 'Two plant FPs on cut sunflower bunches in white buckets: full-frame and right clipped bucket; frozen negative scope retained.', '9fbc2444b8acf1398deaddd3': 'Rabbit and stacked pots: prediction encloses upper pot only, mismatching taller frozen ground-truth extent, FP plus FN.', 'a8d79cd1e9fd1cdb40e2541a': 'All three hanging baskets missed, including two full baskets and bottom-right partial basket.', 'ad4054926f2d2738dc4a8773': 'Main red soil herb pot TP; partial white planted pot at lower left FN.', 'e890004cc7bf2354f7bdc5f3': 'One large plant FP on harvested market leafy produce in bowls and buckets.', 'f792813aeb8521eb5e9ba43f': 'Hanging pot TP plus tall extra FP on left wall ivy.'}


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
 folder=ROOT/'Reports/plant-v69-market-diagnostic/final-val';report=json.loads((folder/'report.json').read_text())
 errors={Path(r['image'].replace('\\','/')).stem:r for r in report['per_image'] if r['false_positive'] or r['false_negative']}
 assert errors.keys()==NOTES.keys()
 rows=[]
 for key,note in NOTES.items():
  p=folder/f'error-{key}.jpg'
  rows.append(dict(image=errors[key]['image'],counts={k:errors[key][k] for k in ['ground_truth','true_positive','false_positive','false_negative']},observation=note,actually_viewed=True,overlay=p.relative_to(ROOT).as_posix(),overlay_sha256=sha(p)))
 out=folder.parent/'error-review.json';assert not out.exists()
 out.write_text(json.dumps(dict(status='ALL8_SELECTED_V69_ERRORS_ACTUALLY_REVIEWED',reviewed=rows,report_sha256=sha(folder/'report.json'),weight_sha256=report['sha256'],labels_changed=False,checkpoint_reselected=False,deployment_accepted=False,test_used=False,pico_validated=False),indent=2),encoding='utf-8')
 print('All8 selected error overlays recorded with actual SHA; no relabeling or acceptance')


if __name__=='__main__':main()
