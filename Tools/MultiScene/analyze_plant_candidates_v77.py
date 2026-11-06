"""Derive conservative failure-stage evidence from actual v77 traces."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def main():
 folder=ROOT/'Reports/plant-v77-candidate-diagnostic';source=folder/'report.json';r=json.loads(source.read_text());protocol=json.loads((folder/'protocol.json').read_text());assert r['protocol_sha256']==hashlib.sha256((folder/'protocol.json').read_bytes()).hexdigest()
 for f in protocol['files']:assert hashlib.sha256((ROOT/f['path']).read_bytes()).hexdigest()==f['sha256']
 rows=[]
 for version,value in r['results'].items():
  for image in value['images']:
   for i,target in enumerate(image['ground_truth_traces']):
    plant=value['plant_native_id'];a=[d for d in target['post_nms_max8_matches'] if d['native_class']==plant and d['score']>=.35];b=[d for d in target['post_nms_max300_matches'] if d['native_class']==plant and d['score']>=.35]
    top=target['top_matching_raw_plant_candidates'][0] if target['top_matching_raw_plant_candidates'] else None
    rows.append(dict(model=version,image=image['image'],truth_index=i,raw_top_plant_score=top['plant_score'] if top else None,raw_top_plant_winner=top['winning_class'] if top else None,raw_top_iou=top['iou'] if top else None,raw_passing_plant_anchors=target['raw_plant_winner_above_operating_confidence'],post_nms_operating_matches_max8=len(a),post_nms_operating_matches_max300=len(b)))
 v76=[x for x in rows if x['model']=='v76'];assert len(v76)==6
 missing=[x for x in v76 if not x['post_nms_operating_matches_max8']];assert len(missing)==5
 assert all(x['raw_passing_plant_anchors']==0 and x['post_nms_operating_matches_max300']==0 for x in missing)
 findings=dict(status='FIVE_V76_MISSES_HAVE_NO_PRE_NMS_PLANT_WINNER_ABOVE_035_AT_IOU50',rows=rows,source_report_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),frozen_inputs_verified=len(protocol['files']),conclusions=['For these five v76 misses, no correctly overlapping plant-winning anchor reaches the fixed0.35 confidence before NMS; raising max_det from8 to300 does not recover a valid operating detection.','Several correctly overlapping anchors exist at lower plant confidence; this is not evidence of a missing image, coordinate conversion failure or absent raw geometry.','The foliage hanging pot has raw matching score official0.773/v69 0.758/v76 0.098; the frozen full-frame v76 report regression is reproduced at raw-score level.','Some basket anchors favor CutArrangement; official COCO basket scores were already below0.35, so fine-tuning alone is not established as the unique cause of every miss.'],limitations=['Only three repeatedly observed development VAL images; no fresh TEST or PICO evidence.','No confidence policy changes, retraining, checkpoint reselection or deployment promotion.','These measurements support a bounded model-retention/target-domain experiment; they do not prove a unique optimization mechanism.'],test_used=False,threshold_changed=False,deployment_accepted=False)
 out=folder/'findings.json';assert not out.exists();out.write_text(json.dumps(findings,indent=2),encoding='utf-8');print(json.dumps(findings,indent=2))
if __name__=='__main__':main()
