"""Release reviewed v35 payload after classifying refreshed duplicate matches."""
import json,hashlib,shutil
from pathlib import Path
from dataset_paths import manifest_relative_path as rel
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
 parent=ROOT/'Datasets/statue-articulated-group-v35';out=ROOT/'Datasets/statue-articulated-release-v36'
 assert not out.exists()
 audit=read(parent/'audit.json');assert sha(parent/'audit.json')=='9129b576abf65e4f2f3098b28176b000b1c0e0361202f6ad52502ee9a47927c3'
 pixel=ROOT/'Reports/statue-group-train-v35-pixel-history.json';r=read(pixel)
 assert r['scanned_paths']==r['enumerated_paths']==17712 and not r['errors'] and not r['internal_matches']
 source=ROOT/'Datasets/statue-group-train-source-v25/manifest.json';assert sha(source)==r['source_manifest_sha256']
 sources={x['page_id']:x for x in read(source)['samples']}
 checked=[]
 for hit in r['matches']:
  assert hit['encoded_exact'] and hit['decoded_exact'] and hit['dhash_distance']==0
  hp=rel(hit['historical_image']);dataset=ROOT/'Datasets'/hp.parts[0]
  spec=read(dataset/'audit.json');image=Path(*hp.parts[1:]).as_posix()
  matching=[x for x in spec['samples'] if rel(x['image']).as_posix()==image]
  assert len(matching)==1 and matching[0]['split']=='train'
  assert matching[0]['sha256']==hit['historical_sha256']==sources[hit['candidate_id']]['sha256']
  assert sha(ROOT/'Datasets'/hp)==hit['historical_sha256']
  checked.append(dict(hit,dataset_audit_sha256=sha(dataset/'audit.json'),review='Exact known TRAIN copy, never VAL or TEST'))
 assert len(checked)==13
 assert [x['candidate_id'] for x in checked].count(49138897)==1
 review=ROOT/'Reports/statue-group-train-v35-pixel-review.json'
 assert not review.exists()
 review.write_text(json.dumps(dict(status='PASS',source_manifest_sha256=sha(source),pixel_report_sha256=sha(pixel),reviewed_matches=checked,scope='No cross-split pixel match found in actual17712-file scan; unknown aliases, physical subject, unrecorded crops and external pretraining not excluded.'),indent=2),encoding='utf-8')
 for row in audit['samples']:
  for key,digest in [('image','sha256'),('label','label_sha256')]:
   src=parent/rel(row[key]);assert sha(src)==row[digest]
   dst=out/rel(row[key]);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst);assert sha(dst)==row[digest]
 audit.update(parent_audit_sha256=sha(parent/'audit.json'),training_approved=True,deployment_accepted=False,revision='Release same103photos81TRAIN19VAL3historicalTEST after refreshed pixel review; all payload bytes/folds unchanged from v35.')
 audit['context_evidence'].update({p.relative_to(ROOT).as_posix():sha(p) for p in [pixel,review,Path(__file__).resolve()]})
 (out/'audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 (out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='images/val',test='images/test',names=['Statue']),indent=2)+'\n',encoding='utf-8')
 (out/'annotation_release.json').write_text(json.dumps(dict(status='REVIEWED_DATA_NOT_MODEL_ACCEPTANCE',dataset_audit_sha256=sha(out/'audit.json'),parent_review_hold_preserved=True,training_approved=True,deployment_accepted=False),indent=2),encoding='utf-8')
 print(json.dumps(dict(status='PASS',images=len(audit['samples']),splits=audit['split_counts'],audit_sha256=sha(out/'audit.json'))))
if __name__=='__main__':main()
