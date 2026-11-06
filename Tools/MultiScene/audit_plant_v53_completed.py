"""Verify path-fix experiment and preserve visual review plus train/VAL contrast."""
import csv,hashlib,json
from pathlib import Path
import torch
ROOT=Path(__file__).resolve().parents[2]
def read(p):return json.loads(p.read_text(encoding='utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
NOTES={
 '2eeeba800c582ff3ecef355e':'Main bonsai TP, unresolved companion vessel FP caveat retained; no relabeling.',
 '9fbc2444b8acf1398deaddd3':'Upper planted pot prediction versus larger stacked-container GT creates FP+FN spatial mismatch.',
 'a8d79cd1e9fd1cdb40e2541a':'Two full hanging baskets and one partial remain missed.',
 '5cd2e794017caf6756d2020a':'Cut roses auxiliary TP plus erroneous overlapping PottedPlant prediction.',
 'd6a4f5f64a10ff59fe54d62c':'Empty decorative glass vase again misclassified PottedPlant.',
 'ad4054926f2d2738dc4a8773':'Main herb pot TP; partial left planted container still missed.',
 'f6064c57134f3277e5fe7b20':'Harvested chopped food greens on plate misclassified PottedPlant.',
 '467fb80fca7896af512309a0':'Loose plate spinach leaves misclassified PottedPlant.',
 'e890004cc7bf2354f7bdc5f3':'Harvested market produce scene misclassified as one large PottedPlant.'}
def main():
 out=ROOT/'Reports/plant-v53-pathfix-diagnostic';dest=out/'independent-audit.json';assert not dest.exists()
 plan=read(out/'protocol.json');val=read(out/'final-val/report.json');trainpath=ROOT/'Reports/plant-v53-training-diagnostic/report.json';train=read(trainpath)
 for r in plan['files']:assert sha(ROOT/r['path'])==r['sha256'],r['path']
 best=out/'train/weights/best.pt';assert sha(best)==val['sha256']==train['sha256']=='bd65eb25f027fc56ec9fdd2199f3eb4fce401cb0520474e6953584b00589ff90'
 rows=list(csv.DictReader((out/'train/results.csv').open()));assert [int(r['epoch']) for r in rows]==list(range(1,31))
 chosen=max(rows,key=lambda r:float(r['metrics/mAP50-95(B)']))
 ckpt=torch.load(best,map_location='cpu',weights_only=False)
 assert ckpt['train_metrics']['fitness']==float(chosen['metrics/mAP50-95(B)'])
 oldplan=read(ROOT/'Reports/plant-v51-diagnostic/protocol.json')
 assert {k:v for k,v in plan['parameters'].items() if k not in ('data','project')}=={k:v for k,v in oldplan['parameters'].items() if k not in ('data','project')}
 repaired=[r for r in train['per_image'] if any(stem in r['image'] for stem in ['27375dda6bb6b0eb','13998fa14da472bf'])]
 assert len(repaired)==2 and sum(r['ground_truth'] for r in repaired)==7
 errors=[]
 for r in val['errors']:
  p=out/'final-val'/r['example'];stem=p.stem.removeprefix('error-')
  errors.append(dict(r,observation=NOTES[stem],overlay_actually_viewed=True,overlay_sha256=sha(p)))
 assert len(errors)==9
 result=dict(status='COMPLETED_DIAGNOSTIC_NOT_ACCEPTED',epochs=30,native_best_epoch=int(chosen['epoch']),
  frozen_files_verified=len(plan['files']),best_sha256=sha(best),last_sha256=sha(out/'train/weights/last.pt'),
  report_sha256=sha(out/'final-val/report.json'),train_report_sha256=sha(trainpath),
  training_metrics=train['operating_per_class'],validation_metrics=val['operating_per_class'],repaired_training_images=repaired,
  actual_val_errors_reviewed=errors,labels_semantically_changed=False,deployment_accepted=False,
  conclusion='Label discovery repair verified independently. TRAIN fit improvement does not establish held-out generalization; fullVALprecision/recall still fail.')
 dest.write_text(json.dumps(result,indent=2),encoding='utf-8')
 print(json.dumps({k:result[k] for k in ['status','native_best_epoch','training_metrics','repaired_training_images']}))
if __name__=='__main__':main()
