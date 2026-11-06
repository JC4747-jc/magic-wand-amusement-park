"""Preserve actual visual QA of selected v78 errors; do not relabel validation."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
NOTES={'80af71df5d201ada8c3935bb': 'One full-frame plant FP on cut sunflower bunches in white buckets.', '478585150555622ee282ec40': 'All four potted plants TP; extra foliage-only prediction duplicates clipped right bonsai.', '9fbc2444b8acf1398deaddd3': 'Full stacked container TP plus extra upper-pot-only duplicate FP.', 'f792813aeb8521eb5e9ba43f': 'Hanging pot recovered as TP; separate bottom-right ground vegetation FP.', 'a8d79cd1e9fd1cdb40e2541a': 'All three hanging baskets missed, including bottom-right clipped partial basket.', '5cd2e794017caf6756d2020a': 'Cut white roses in clear water vase predicted plant under plant-only route; actual rooted plant absent.', 'd6a4f5f64a10ff59fe54d62c': 'Empty amber ribbed glass vase predicted plant, no foliage or soil.', 'ad4054926f2d2738dc4a8773': 'Main red herb pot matches with box extending left; partial white planted pot lower left missed.'}


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
 folder=ROOT/'Reports/plant-v78-freeze-diagnostic/final-runtime-val';report=json.loads((folder/'report.json').read_text())
 errors={Path(r['image'].replace('\\','/')).stem:r for r in report['per_image'] if r['false_positive'] or r['false_negative']}
 assert errors.keys()==NOTES.keys()
 rows=[]
 for key,note in NOTES.items():
  p=folder/f'error-{key}.jpg'
  rows.append(dict(image=errors[key]['image'],counts={k:errors[key][k] for k in ['ground_truth','true_positive','false_positive','false_negative']},observation=note,actually_viewed=True,overlay=p.relative_to(ROOT).as_posix(),overlay_sha256=sha(p)))
 out=folder.parent/'error-review.json';assert not out.exists()
 out.write_text(json.dumps(dict(status='ALL8_SELECTED_V78_ERRORS_ACTUALLY_REVIEWED',reviewed=rows,report_sha256=sha(folder/'report.json'),weight_sha256=report['sha256'],labels_changed=False,checkpoint_reselected=False,deployment_accepted=False,test_used=False,pico_validated=False),indent=2),encoding='utf-8')
 print('All8 selected error overlays recorded with actual SHA; no relabeling or acceptance')


if __name__=='__main__':main()
