"""Exhaust captured score breakpoints without new inference or deployment changes."""
import hashlib,json,math
from pathlib import Path
from runtime_checkpoint_selection_v57 import metrics,key
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 folder=ROOT/'Reports/plant-v82-confidence-diagnostic';source=folder/'report.json';report=json.loads(source.read_text());protocol=json.loads((folder/'protocol.json').read_text());assert report['protocol_sha256']==sha(folder/'protocol.json')
 for row in protocol['files']:assert sha(ROOT/row['path'])==row['sha256']
 policy=json.loads((ROOT/'Reports/plant-v81-binary-diagnostic/runtime-selection/policy.json').read_text());old=set(policy['regression_images'])
 images=report['per_image'];assert len(images)==30
 def evaluate(threshold):
  rows=[]
  for row in images:
   gt=row['curves'][0]['ground_truth'];accepted=[p for p in row['predictions'] if p['confidence']>=threshold];tp=sum(p['match_iou50'] for p in accepted);rows.append(dict(image=row['image'],ground_truth=gt,true_positive=tp,false_positive=len(accepted)-tp,false_negative=gt-tp))
  full=metrics(rows);original=metrics([r for r in rows if key(r['image']) in old]);gate=lambda m:m['precision']>=.9 and m['recall']>=.8
  return dict(threshold=threshold,full=full,original13=original,passes_both_gates=gate(full) and gate(original))
 for row in report['grid']:assert evaluate(row['threshold'])==row
 # With score>=threshold, each distinct score represents the interval ending at it.
 # nextafter also explicitly covers each immediately-excluded score set.
 scores={float(p['confidence']) for row in images for p in row['predictions']};assert all(.001<=s<=1 for s in scores)
 points=sorted({.001,1.,*scores,*[math.nextafter(s,math.inf) for s in scores if s<1.]});traces=[evaluate(t) for t in points]
 passing=[r for r in traces if r['passes_both_gates']];recall_eligible=[r for r in traces if r['full']['recall']>=.8];assert recall_eligible
 best=max(recall_eligible,key=lambda r:(r['full']['precision'],r['full']['recall'],-r['threshold']))
 result=dict(status='NO_CAPTURED_SCORE_THRESHOLD_PASSES_BOTH_GATES' if not passing else 'CAPTURED_SCORE_THRESHOLD_PASSES_DEVELOPMENT_GATES',source_report_sha256=sha(source),distinct_scores=len(scores),breakpoints_evaluated=len(points),passing_count=len(passing),best_full_precision_subject_to_recall08=best,passing_breakpoints=passing,all_predeclared_grid_counts_exact=True,method='Enumerate every captured score and immediately-above boundary plus endpoints; no change of model, matching, max_det or NMS. This exhausts accepted subsets obtainable by thresholding these recorded predictions.',scope='Observed30-image development VAL and captured post-NMS predictions at conf0.001/max_det8 only; not a new official benchmark, fresh test, or proof for other preprocessing/NMS policies.',historical_gate_changed=False,runtime_config_changed=False,new_inference_performed=False,test_used=False,deployment_accepted=False)
 assert not passing
 out=folder/'breakpoint-findings.json';assert not out.exists();out.write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
if __name__=='__main__':main()
