"""Evaluate finalized v15 target on all13 VAL rows, never the12-image fitness subset."""
import csv,hashlib,json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    import torch
    run=ROOT/'Reports/plant-confuser-v15-training';data=ROOT/'Datasets/plant-confuser-v15'
    rows=list(csv.DictReader((run/'train/results.csv').open()));assert len(rows)==50 and int(rows[-1]['epoch'])==50
    spec=json.loads((run/'run.json').read_text());assert spec['dataset_version']==sha(data/'audit.json')
    a=json.loads((data/'audit.json').read_text());val=[r for r in a['samples'] if r['split']=='val']
    assert len(val)==13 and not any(r.get('evaluation_excluded') for r in val)
    assert any(r['original_id']=='03abf85fbae6ceaa' for r in val),'Hard negative must remain in full target acceptance'
    weight=run/'train/weights/best.pt'
    for name in ['best.pt','last.pt']:
        ck=torch.load(run/'train/weights'/name,map_location='cpu',weights_only=False);assert ck['epoch']==-1 and ck.get('optimizer') is None
    results={}
    for label,dataset,split in [('runtime-val','plant-confuser-v15','val'),('auxiliary-val-diagnostic','plant-confuser-v13-aux-val-view','val'),('train-diagnostic','plant-confuser-v15','train'),('observed-cut-flower-negative','plant-additional-cut-flower-negative-v1','test')]:
        out=ROOT/f'Reports/plant-confuser-v15-{label}';assert not out.exists()
        allowed=['PottedPlant','CutArrangement'] if label=='auxiliary-val-diagnostic' else ['PottedPlant']
        subprocess.run([sys.executable,str(ROOT/'Tools/MultiScene/evaluate_mapped.py'),'--model',str(weight),'--data',str(ROOT/'Datasets'/dataset),'--out',str(out),'--split',split,'--imgsz','640','--operating-conf','.35','--allowed-classes',*allowed,'--max-det','8','--nms-iou','.7'],cwd=ROOT,check=True)
        r=json.loads((out/'report.json').read_text());assert r['sha256']==sha(weight)
        if label=='runtime-val':assert r['images']==13 and not r['excluded_samples'] and r['ground_truth_instances']==13
        results[label]=dict(images=r['images'],operating=[p for p in r['operating_per_class'] if p['name'] in allowed],ap=r['per_class'],mean_inference_ms=r['mean_inference_ms'])
    v=results['runtime-val']['operating'][0];n=results['observed-cut-flower-negative']['operating'][0]
    passed=(v['precision'] or 0)>=.9 and (v['recall'] or 0)>=.8 and n['false_positive']==0
    decision=dict(status='FULL_VAL_AND_OBSERVED_NEGATIVE_PASSED_MORE_REGRESSION_AND_FRESH_TEST_PENDING' if passed else 'REJECTED_VALIDATION_OR_NEGATIVE_REGRESSION',sha256=sha(weight),dataset_version=sha(data/'audit.json'),results=results,training_seconds=float(rows[-1]['time']),native_two_class_fitness_images=12,target_runtime_val_images=13,service_routed=False,pico_validated=False,fresh_test_performed=False,limitations=['CutArrangement is auxiliary only; never a Unity target','Native fitness12omits unresolved auxiliary sunflower-label image; full target gate13includes it','Observed negative diagnostic has one eligible image; not broad negative acceptance','Tiny public-photo pilot; independent fresh TEST and PICO positives still required'])
    (ROOT/'Reports/plant-confuser-v15-decision.json').write_text(json.dumps(decision,indent=2));print(json.dumps(decision))
if __name__=='__main__':main()
