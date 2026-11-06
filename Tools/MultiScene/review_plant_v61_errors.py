"""Record all actual selected-v61 error overlays, preserving frozen labels."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
NOTES={
'0913dea122b9e30e10a04073':'Two plant false positives on market harvested produce cartons; plated-food negatives did not remove market-container confusion.',
'2eeeba800c582ff3ecef355e':'Main bonsai matched; lower-left companion vessel predicted plant. Existing semantic scope hold remains, so this is not an unqualified model FP.',
'478585150555622ee282ec40':'Four annotated plants matched, but extra smaller boxes on clipped left/right plants produce duplicate false positives; retain original instance grouping.',
'80af71df5d201ada8c3935bb':'Near-full-image plant false positive on cut sunflower bunches in white buckets; negative label scope retained.',
'9fbc2444b8acf1398deaddd3':'Stacked planted pots matched by one tall box with an extra upper-pot prediction; nested duplicate/grouping issue.',
'a6bbcafb10d754a05e0706b3':'Sparse planted succulent has plant TP plus overlapping auxiliary CutArrangement false positive; class competition/duplicate prediction.',
'a8d79cd1e9fd1cdb40e2541a':'Two full hanging baskets missed; third partial lower-right basket matched. Hanging appearance coverage remains weak.',
'ad4054926f2d2738dc4a8773':'Main herb in red soil pot matched; partial planted white pot at lower left missed.',
'c40a67d7e5c2aa244969b1f6':'Basil in black soil pot has plant TP plus overlapping auxiliary CutArrangement false positive.',
'e890004cc7bf2354f7bdc5f3':'One large plant false positive covers harvested leafy bundles in market bowls/buckets; visible food context remains confused.',
'f792813aeb8521eb5e9ba43f':'Annotated suspended pot missed; separate tiny red FP at bottom right on ground vegetation without a visible pot.'}


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
 folder=ROOT/'Reports/plant-v61-food-diagnostic/final-val'
 report=json.loads((folder/'report.json').read_text())
 errors={Path(r['image'].replace('\\','/')).stem:r for r in report['per_image'] if r['false_positive'] or r['false_negative']}
 assert errors.keys()==NOTES.keys()
 rows=[]
 for key,note in NOTES.items():
  overlay=folder/f'error-{key}.jpg'
  rows.append(dict(image=errors[key]['image'],observation=note,actually_viewed=True,
   counts={k:errors[key][k] for k in ['ground_truth','true_positive','false_positive','false_negative']},
   overlay=overlay.relative_to(ROOT).as_posix(),overlay_sha256=sha(overlay)))
 out=folder.parent/'error-review.json';assert not out.exists()
 out.write_text(json.dumps(dict(status='ALL11_SELECTED_V61_ERRORS_VISUALLY_REVIEWED',reviewed=rows,
  report_sha256=sha(folder/'report.json'),weight_sha256=report['sha256'],labels_changed=False,
  checkpoint_reselected=False,test_used=False,pico_validated=False,deployment_accepted=False),indent=2),encoding='utf-8')
 print('All11 error overlays reviewed; frozen labels and rejected deployment status retained')


if __name__=='__main__':main()
