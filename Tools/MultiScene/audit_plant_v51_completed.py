"""Audit retained v51 run and record actually viewed errors; never promote it."""
import csv
import hashlib
import json
from collections import defaultdict
from pathlib import Path
import torch

ROOT = Path(__file__).resolve().parents[2]
NOTES = {
 '2eeeba800c582ff3ecef355e': 'Main bonsai TP; companion vessel still unresolved annotation scope, retain caveat and labels.',
 '80af71df5d201ada8c3935bb': 'New false positive on cut sunflowers in buckets; whole-frame box. Existing negative label retained; auxiliary arrangement grouping needs separate source-based review, not metric-driven relabeling.',
 '9fbc2444b8acf1398deaddd3': 'Plant prediction covers upper planted pot but GT includes larger stacked-container extent: spatial mismatch causes FP+FN, not absence of plant recognition.',
 'f792813aeb8521eb5e9ba43f': 'Hanging pot still FN, previous whole-background/ground-foliage false predictions gone.',
 'a8d79cd1e9fd1cdb40e2541a': 'Two full hanging baskets and one partial basket remain without accepted detections.',
 '5cd2e794017caf6756d2020a': 'Cut roses arrangement detected plus additional overlapping erroneous prediction; extra class/box confusion, not a new physical target.',
 'ad4054926f2d2738dc4a8773': 'Main herb pot TP, partial left white planted pot remains FN.',
 '467fb80fca7896af512309a0': 'New high-confidence plant false positive on loose spinach leaves on plate, no planted container.',
}


def sha(p):
 return hashlib.sha256(p.read_bytes()).hexdigest()


def read(p):
 return json.loads(p.read_text(encoding='utf-8'))


def main():
 out=ROOT/'Reports/plant-v51-diagnostic'
 dest=out/'independent-audit.json'
 assert not dest.exists()
 plan=read(out/'protocol.json'); completed=read(out/'completed.json'); report=read(out/'final-val/report.json')
 for item in plan['files']:
  assert sha(ROOT/item['path'])==item['sha256'], item['path']
 assert report['images']==30 and sha(out/'train/weights/best.pt')==report['sha256']==completed['weight_sha256']
 rows=list(csv.DictReader((out/'train/results.csv').open()))
 assert [int(r['epoch']) for r in rows]==list(range(1,31))
 ckpt=torch.load(out/'train/weights/best.pt',map_location='cpu',weights_only=False)
 selected=max(rows,key=lambda r:float(r['metrics/mAP50-95(B)']))
 assert int(selected['epoch'])==28
 assert ckpt['train_metrics']['fitness']==float(selected['metrics/mAP50-95(B)'])
 for k in ['metrics/precision(B)','metrics/recall(B)','metrics/mAP50(B)','metrics/mAP50-95(B)']:
  assert ckpt['train_metrics'][k]==float(selected[k])
 old=read(ROOT/'Reports/plant-v47-prospective-baseline/report.json')
 audit=read(ROOT/'Datasets/plant-hanging-v50/audit.json')
 lookup={r['image'].replace('\\','/'):r for r in audit['samples']}
 cohorts={}
 for name,r in [('old',old),('new',report)]:
  groups=defaultdict(lambda:dict(images=0,ground_truth=0,true_positive=0,false_positive=0,false_negative=0))
  for row in r['per_image']:
   c=groups[lookup[row['image'].replace('\\','/')].get('validation_cohort','original13')];c['images']+=1
   for k in ['ground_truth','true_positive','false_positive','false_negative']:c[k]+=row[k]
  cohorts[name]=dict(groups)
 errors=[]
 for error in report['errors']:
  stem=Path(error['example']).stem.removeprefix('error-')
  errors.append(dict(error,observation=NOTES[stem],overlay_actually_viewed=True,
                     overlay_sha256=sha(out/'final-val'/error['example'])))
 assert len(errors)==8
 result=dict(status='COMPLETED_EVIDENCE_VERIFIED_NOT_ACCEPTED',native_best_epoch_from_csv_and_checkpoint=28,
  stripped_epoch_field=ckpt['epoch'], frozen_files_verified=len(plan['files']),epochs_completed=30,
  best_sha256=sha(out/'train/weights/best.pt'),last_sha256=sha(out/'train/weights/last.pt'),
  csv_sha256=sha(out/'train/results.csv'),report_sha256=sha(out/'final-val/report.json'),
  old_report_sha256=sha(ROOT/'Reports/plant-v47-prospective-baseline/report.json'),
  cohorts_both_classes=cohorts,reviewed_errors=errors,labels_changed=False,
  decision='NOT_ACCEPTED: plantP.8R.727 fails development gates; OPEN annotation hold and no independent TEST/PICO.',
  deployment_accepted=False)
 dest.write_text(json.dumps(result,indent=2),encoding='utf-8')
 print(json.dumps(dict(status=result['status'],native_best_epoch=28,cohorts=cohorts)))


if __name__=='__main__':main()
