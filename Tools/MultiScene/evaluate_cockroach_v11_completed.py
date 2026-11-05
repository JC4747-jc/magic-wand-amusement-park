"""Evaluate finalized expanded cockroach checkpoint on VAL/TRAIN and observed negatives only."""
import csv
import hashlib
import json
import subprocess
import sys
from pathlib import Path
import torch

ROOT=Path(__file__).resolve().parents[2]


def main():
    run=ROOT/'Reports/cockroach-positive-train-v11-training'
    rows=list(csv.DictReader((run/'train/results.csv').open()))
    assert len(rows)==30 and int(rows[-1]['epoch'])==30
    weight=run/'train/weights/best.pt'
    for name in ['best.pt','last.pt']:
        checkpoint=torch.load(run/'train/weights'/name,map_location='cpu',weights_only=False)
        assert checkpoint['epoch']==-1 and checkpoint.get('optimizer') is None
    digest=hashlib.sha256(weight.read_bytes()).hexdigest();results={}
    cases=[('runtime-val','cockroach-positive-train-v11','val'),('train-diagnostic','cockroach-positive-train-v11','train'),
           ('observed-public-negative','cockroach-public-negative-test-v3','test'),
           ('observed-pico-negative','pico-cockroach-negative-heldout-v1','test')]
    for name,dataset,split in cases:
        out=ROOT/f'Reports/cockroach-positive-train-v11-{name}'
        if out.exists():
            raise FileExistsError(out)
        subprocess.run([sys.executable,str(ROOT/'Tools/MultiScene/evaluate_mapped.py'),'--model',str(weight),
            '--data',str(ROOT/'Datasets'/dataset),'--out',str(out),'--split',split,'--imgsz','640',
            '--operating-conf','.65','--allowed-classes','Cockroach','--max-det','8','--nms-iou','.7'],cwd=ROOT,check=True)
        report=json.loads((out/'report.json').read_text())
        assert report['sha256']==digest
        results[name]=dict(images=report['images'],operating=report['operating_per_class'])
    val=results['runtime-val']['operating'][0]
    passed=(val['precision'] or 0)>=.9 and (val['recall'] or 0)>=.8
    negative_pass=all(results[k]['operating'][0]['false_positive']==0 for k in ['observed-public-negative','observed-pico-negative'])
    decision=dict(status='VAL_AND_OBSERVED_NEGATIVES_PASSED_FRESH_TEST_PENDING' if passed and negative_pass else 'REJECTED_VALIDATION_OR_NEGATIVE_REGRESSION',
        model_sha256=digest,training_seconds=float(rows[-1]['time']),results=results,service_routed=False,
        fresh_test_performed=False,pico_positive_test='NOT_PERFORMED',
        limitations=['Observed negative regressions are not fresh TEST; VAL has29positiveinstances and12negativeimages',
                     'Historical/fresh-v4/fresh-v6 TEST not accessed for checkpoint or threshold selection'])
    (ROOT/'Reports/cockroach-positive-train-v11-decision.json').write_text(json.dumps(decision,indent=2));print(json.dumps(decision))


if __name__=='__main__':
    main()
