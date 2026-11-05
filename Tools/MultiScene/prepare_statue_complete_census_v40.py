"""Append seven reviewed missing VAL instances in a new immutable dataset."""
import json,hashlib,shutil
from pathlib import Path
from dataset_paths import manifest_relative_path as rel
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
 parent=ROOT/'Datasets/statue-flat-confusers-v38';out=ROOT/'Datasets/statue-complete-census-v40';review=ROOT/'Reports/statue-dense-val-v40-census/reviewed.json'
 assert not out.exists() and sha(parent/'audit.json')=='7a5c03ca6f1a75cac5338f8b76fbf992ff136441d4fa2580c46a3124929e3f95'
 d=read(review);a=read(parent/'audit.json');assert d['annotation_approved'] and d['overlay_reviewed'] and len(d['new_boxes'])==21
 assert sha(ROOT/d['overlay'])==d['overlay_sha256']
 changed=[]
 for row in a['samples']:
  for key,digest in [('image','sha256'),('label','label_sha256')]:
   p=parent/rel(row[key]);assert sha(p)==row[digest];dst=out/rel(row[key]);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dst)
  if row['sha256']!=d['source_sha256']:continue
  assert row['split']=='val' and row['label_sha256']==d['label_sha256']
  p=out/rel(row['label']);old=p.read_bytes();assert len(old.splitlines())==14
  w,h=d['dimensions'];lines=[]
  for item in d['proposed_extra_instances']:
   x1,y1,x2,y2=item['box'];assert 0<=x1<x2<=w and 0<=y1<y2<=h
   lines.append(f'0 {(x1+x2)/(2*w):.8f} {(y1+y2)/(2*h):.8f} {(x2-x1)/w:.8f} {(y2-y1)/h:.8f}\n')
  assert len(lines)==7
  p.write_bytes(old+(b'' if old.endswith(b'\n') else b'\n')+''.join(lines).encode('utf-8'))
  assert p.read_bytes().startswith(old) and len(p.read_bytes().splitlines())==21
  changed.append(dict(label=row['label'],old_sha256=row['label_sha256'],new_sha256=sha(p),split='val',old_instances=14,new_instances=21))
  row.update(label_sha256=sha(p),annotation_status='REVIEWED_COMPLETE_VISIBLE_CENSUS_21',review=review.relative_to(ROOT).as_posix())
 assert len(changed)==1
 for before,after in zip(read(parent/'audit.json')['samples'],a['samples']):
  assert before['image']==after['image'] and before['split']==after['split'] and before['group']==after['group']
  assert sha(parent/rel(before['image']))==sha(out/rel(after['image']))
  if before['sha256']!=d['source_sha256']:assert sha(parent/rel(before['label']))==sha(out/rel(after['label']))
 a.update(parent_audit_sha256=sha(parent/'audit.json'),label_corrections=changed,training_approved=True,deployment_accepted=False,revision='Append seven missing visible figures to one VAL label; all110photos/groups/folds and other109label files unchanged, original14lines retained. Historical reports unchanged; prospective selection only.')
 a['context_evidence'].update({review.relative_to(ROOT).as_posix():sha(review),Path(__file__).resolve().relative_to(ROOT).as_posix():sha(Path(__file__).resolve())})
 (out/'audit.json').write_text(json.dumps(a,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 (out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='images/val',test='images/test',names=['Statue']),indent=2)+'\n',encoding='utf-8')
 (out/'annotation_release.json').write_text(json.dumps(dict(status='REVIEWED_DATA_NOT_MODEL_ACCEPTANCE',dataset_audit_sha256=sha(out/'audit.json'),val_images=19,val_instances=40,historical_reports_unchanged=True,retrospective_checkpoint_selection_allowed=False,training_approved=True,deployment_accepted=False),indent=2),encoding='utf-8')
 print(json.dumps(dict(status='PASS',photos=len(a['samples']),splits=a['split_counts'],changed_label_files=1,val_instances=40,audit_sha256=sha(out/'audit.json'))))
if __name__=='__main__':main()
