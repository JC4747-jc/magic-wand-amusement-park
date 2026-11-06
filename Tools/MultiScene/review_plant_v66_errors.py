"""Preserve actual visual QA of selected v66 errors; do not relabel validation."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
NOTES={
'0913dea122b9e30e10a04073':'Three plant FPs on harvested market produce in small cartons; food/container confusion.',
'2eeeba800c582ff3ecef355e':'Main bonsai TP plus companion-vessel FP under frozen scope. Companion semantics remain an annotation hold.',
'467fb80fca7896af512309a0':'Loose harvested spinach leaves on white plate predicted plant; no rooted container visible.',
'478585150555622ee282ec40':'All four annotated potted plants match; extra smaller predictions on clipped right target duplicate the same instance.',
'80af71df5d201ada8c3935bb':'Cut sunflower bunches in white buckets get near-full-image plant FP; frozen negative scope retained.',
'a8d79cd1e9fd1cdb40e2541a':'One right hanging basket matches with overlarge box; left full basket and bottom-right partial basket missed.',
'ad4054926f2d2738dc4a8773':'Main red soil herb pot matches; partial white planted pot at lower left missed.',
'd6a4f5f64a10ff59fe54d62c':'Empty decorative amber/gold ribbed glass vase predicted plant; no foliage or soil.',
'e890004cc7bf2354f7bdc5f3':'Two large plant FPs on market harvested leafy produce in bowls/buckets and foreground bundles.',
'f792813aeb8521eb5e9ba43f':'Hanging pot matches but box extends far above plant; separate FPs on left wall ivy and bottom-right ground vegetation.'}


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
 folder=ROOT/'Reports/plant-v66-herb-diagnostic/final-val';report=json.loads((folder/'report.json').read_text())
 errors={Path(r['image'].replace('\\','/')).stem:r for r in report['per_image'] if r['false_positive'] or r['false_negative']}
 assert errors.keys()==NOTES.keys()
 rows=[]
 for key,note in NOTES.items():
  p=folder/f'error-{key}.jpg'
  rows.append(dict(image=errors[key]['image'],counts={k:errors[key][k] for k in ['ground_truth','true_positive','false_positive','false_negative']},observation=note,actually_viewed=True,overlay=p.relative_to(ROOT).as_posix(),overlay_sha256=sha(p)))
 out=folder.parent/'error-review.json';assert not out.exists()
 out.write_text(json.dumps(dict(status='ALL10_SELECTED_V66_ERRORS_ACTUALLY_REVIEWED',reviewed=rows,report_sha256=sha(folder/'report.json'),weight_sha256=report['sha256'],labels_changed=False,checkpoint_reselected=False,deployment_accepted=False,test_used=False,pico_validated=False),indent=2),encoding='utf-8')
 print('All10 selected error overlays recorded with actual SHA; no relabeling or acceptance')


if __name__=='__main__':main()
