"""Evaluate only finalized native plant v11; fixed protocol, no TEST selection."""
import csv,hashlib,json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def main():
    import torch
    run=ROOT/'Reports/plant-native-v11-training'
    rows=list(csv.DictReader((run/'train/results.csv').open()))
    assert len(rows)==30 and int(rows[-1]['epoch'])==30,'Training not complete'
    weight=run/'train/weights/best.pt'
    for name in ['best.pt','last.pt']:
        ck=torch.load(run/'train/weights'/name,map_location='cpu',weights_only=False)
        assert ck['epoch']==-1 and ck.get('optimizer') is None,'Checkpoint not finalized'
    digest=hashlib.sha256(weight.read_bytes()).hexdigest();results={}
    for label,dataset,split in [('runtime-val','plant-native-v11','val'),('train-diagnostic','plant-native-v11','train'),('observed-cut-flower-negative','plant-additional-cut-flower-negative-v1','test')]:
        out=ROOT/f'Reports/plant-native-v11-{label}'
        assert not out.exists(),out
        subprocess.run([sys.executable,str(ROOT/'Tools/MultiScene/evaluate_mapped.py'),'--model',str(weight),'--data',str(ROOT/'Datasets'/dataset),'--out',str(out),'--split',split,'--imgsz','640','--operating-conf','.65','--allowed-classes','PottedPlant','--max-det','8','--nms-iou','.7'],cwd=ROOT,check=True)
        r=json.loads((out/'report.json').read_text());assert r['sha256']==digest
        results[label]=dict(images=r['images'],operating=[p for p in r['operating_per_class'] if p['name']=='PottedPlant'],ap=r['per_class'],mean_inference_ms=r['mean_inference_ms'])
    v=results['runtime-val']['operating'][0];n=results['observed-cut-flower-negative']['operating'][0]
    passed=(v['precision'] or 0)>=.9 and (v['recall'] or 0)>=.8 and n['false_positive']==0
    decision=dict(status='VAL_AND_OBSERVED_NEGATIVE_PASSED_FRESH_TEST_PENDING' if passed else 'REJECTED_VALIDATION_OR_NEGATIVE_REGRESSION',sha256=digest,results=results,training_seconds=float(rows[-1]['time']),service_routed=False,pico_validated=False,fresh_test_performed=False,limitations=['Small public-photo pilot; observed TEST not accessed for model or threshold selection','No PICO positive acceptance; fresh independent TEST required after validation gates'])
    (ROOT/'Reports/plant-native-v11-decision.json').write_text(json.dumps(decision,indent=2));print(json.dumps(decision))
if __name__=='__main__':main()
