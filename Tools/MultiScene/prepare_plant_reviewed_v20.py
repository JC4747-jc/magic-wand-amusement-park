"""Hold one ambiguous TRAIN group image; never modify held-out labels or old versions."""
from pathlib import Path
import json,hashlib,shutil
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 parent=ROOT/'Datasets/plant-instance-v19';out=ROOT/'Datasets/plant-reviewed-v20';assert not out.exists();audit=parent/'audit.json';assert sha(audit)=='12ff87b047fedb6a1985475d3a33ad006dc6f0ff1bc7a20e35cb98d14e713838';a=json.loads(audit.read_text());hold=json.loads((parent/'review_hold.json').read_text());assert hold['status']=='OPEN' and hold['dataset_audit_sha256']==sha(audit);removed=[r for r in a['samples'] if r['original_id']=='d4457bb0551265a6'];assert len(removed)==1 and removed[0]['split']=='train'
 coverage=ROOT/'Reports/plant-train-v19-full-review-coverage.json';review=json.loads(coverage.read_text());assert review['dataset_audit_sha256']==sha(audit) and review['status']=='ALL92_TRAIN_ORIGINALS_VISUALLY_REVIEWED_THIS_PASS'
 rows=[]
 for old in a['samples']:
  for k,digest in [('image','sha256'),('label','label_sha256')]:assert sha(parent/old[k])==old[digest]
  if old['original_id']=='d4457bb0551265a6':continue
  r=dict(old)
  for k in ('image','label'):
   p=out/r[k];p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(parent/r[k],p);assert p.read_bytes()==(parent/r[k]).read_bytes()
  rows.append(r)
 shutil.copy2(parent/'native-val.txt',out/'native-val.txt');a.update(samples=rows,unique_images=len(rows),counts={s:sum(r['split']==s for r in rows) for s in ('train','val','test')},parent_audit_sha256=sha(audit),source_review_coverage_sha256=sha(coverage),held_train_samples=[dict(**removed[0],reason='Independent wrapped bouquets in shared display bucket: auxiliary instance boundary uncertain',reserved_split='train')],scope='Reviewed TRAIN instance corrections inherited; one unresolved TRAIN group image held without relabeling or fold reassignment. Full13VAL and4historicalTEST unchanged.',training_approved=True,deployment_accepted=False)
 (out/'audit.json').write_text(json.dumps(a,indent=2));(out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='native-val.txt',test='images/test',names=a['names']),indent=2));report=dict(status='PASS_TRAIN_ONLY_ANNOTATION_HOLD',dataset_sha256=sha(out/'audit.json'),counts=a['counts'],held_original_id='d4457bb0551265a6',held_author_reservation='TRAIN_ONLY_DO_NOT_REUSE_AS_TEST',all_retained_image_label_bytes_unchanged=True,all_val_test_rows_retained=True,native_val_list_unchanged=True,training_started=False,model_accepted=False)
 (ROOT/'Reports/plant-reviewed-v20-release-integrity.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
if __name__=='__main__':main()
