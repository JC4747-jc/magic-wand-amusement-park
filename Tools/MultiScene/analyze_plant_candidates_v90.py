import hashlib,json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
out=root/'Reports/plant-v90-candidate-diagnostic'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
protocol=json.loads((out/'protocol.json').read_text());report=json.loads((out/'report.json').read_text())
assert sha(out/'protocol.json')==report['protocol_sha256']
for r in protocol['files']:assert sha(root/r['path'])==r['sha256']
rows=[]
for version,result in report['results'].items():
 for image in result['images']:
  for index,trace in enumerate(image['ground_truth_traces']):
   top=trace['top_matching_raw_plant_candidates']
   scores={cap:max([r['score'] for r in trace[f'post_nms_max{cap}_matches']],default=0) for cap in (8,300)}
   rows.append(dict(model=version,image=image['image'],target_index=index,raw_best_score=top[0]['plant_score'] if top else None,raw_best_iou=top[0]['iou'] if top else None,raw_operating_count=trace['raw_plant_winner_above_operating_confidence'],max8_best=scores[8],max300_best=scores[300],max300_recovers_operating_detection=scores[8]<.35<=scores[300]))
misses=[r for r in rows if r['model']=='v89' and r['max8_best']<.35]
assert len(misses)==4 and all(r['raw_operating_count']==0 and not r['max300_recovers_operating_detection'] for r in misses)
findings=dict(status='CAPTURED_RAW_CANDIDATE_FINDINGS_VERIFIED',rows=rows,v89_missed_targets=4,max300_recovers_v89_misses=0,frozen_files_verified=len(protocol['files']),protocol_sha256=sha(out/'protocol.json'),report_sha256=sha(out/'report.json'),conclusion='All four missed v89 targets have overlapping raw boxes but plant scores below .35; increasing max_det from8to300 on the same tensor does not recover operating detections. This observed failure is not explained by max_det truncation. No unique data/training mechanism established.',scope='Three repeatedly observed VAL photos only; not full-model acceptance, independent test or PICO validation. Native plant class filter applied before max_det.',deployment_accepted=False)
p=out/'findings.json';assert not p.exists();p.write_text(json.dumps(findings,indent=2));print(json.dumps({'misses':misses,'max300_recovers':0}))
