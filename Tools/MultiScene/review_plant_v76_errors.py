"""Preserve actual visual QA of selected v76 errors; do not relabel validation."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
NOTES={'0913dea122b9e30e10a04073': 'Two plant FPs on harvested vegetables in small market cartons.', '2eeeba800c582ff3ecef355e': 'Main bonsai TP plus companion-vessel FP under frozen scope; existing annotation ambiguity retained.', '80af71df5d201ada8c3935bb': 'Two plant FPs on cut sunflower bunches in white buckets, full-frame and right clipped bucket.', '9fbc2444b8acf1398deaddd3': 'Full stacked-container plant matches; extra upper-pot-only prediction duplicates the same instance.', 'a8d79cd1e9fd1cdb40e2541a': 'All three hanging baskets missed, two full baskets and bottom-right partial basket.', 'ad4054926f2d2738dc4a8773': 'Main red soil herb pot TP; partial white planted pot lower left still missed.', 'e890004cc7bf2354f7bdc5f3': 'Two large plant FPs on harvested market leafy produce in bowls/buckets.', 'f792813aeb8521eb5e9ba43f': 'Hanging pot missed; tall left wall-ivy FP, not a valid target match.'}


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
 folder=ROOT/'Reports/plant-v76-positive-diagnostic/final-val';report=json.loads((folder/'report.json').read_text())
 errors={Path(r['image'].replace('\\','/')).stem:r for r in report['per_image'] if r['false_positive'] or r['false_negative']}
 assert errors.keys()==NOTES.keys()
 rows=[]
 for key,note in NOTES.items():
  p=folder/f'error-{key}.jpg'
  rows.append(dict(image=errors[key]['image'],counts={k:errors[key][k] for k in ['ground_truth','true_positive','false_positive','false_negative']},observation=note,actually_viewed=True,overlay=p.relative_to(ROOT).as_posix(),overlay_sha256=sha(p)))
 out=folder.parent/'error-review.json';assert not out.exists()
 out.write_text(json.dumps(dict(status='ALL8_SELECTED_V76_ERRORS_ACTUALLY_REVIEWED',reviewed=rows,report_sha256=sha(folder/'report.json'),weight_sha256=report['sha256'],labels_changed=False,checkpoint_reselected=False,deployment_accepted=False,test_used=False,pico_validated=False),indent=2),encoding='utf-8')
 print('All8 selected error overlays recorded with actual SHA; no relabeling or acceptance')


if __name__=='__main__':main()
