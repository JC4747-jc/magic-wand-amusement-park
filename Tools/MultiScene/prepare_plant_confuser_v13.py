"""Assemble target-preserving two-class plant/confuser development data."""
import hashlib,json,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    parent=ROOT/'Datasets/plant-native-v12';out=ROOT/'Datasets/plant-confuser-v13';assert not out.exists()
    assert sha(parent/'audit.json')=='3e64d1787a8677d316e396a230d1ca727ebe8166bad3b3dad66f98394c7aedcf'
    a=json.loads((parent/'audit.json').read_text());reviewdir=ROOT/'Reports/plant-confuser-v13-review';complete=ROOT/'Reports/plant-confuser-v13-completeness'
    train_review=reviewdir/'reviewed.json';val_review=complete/'val-reviewed.json';positives=complete/'positive-train-review.json'
    tr=json.loads(train_review.read_text(encoding='utf-8-sig'));vr=json.loads(val_review.read_text(encoding='utf-8-sig'));pr=json.loads(positives.read_text())
    assert len(tr['samples'])==15 and len(vr['samples'])==3 and len(pr['samples'])==50
    aux={r['original_id']:r for r in tr['samples']+vr['samples']}
    for r in aux.values():
        assert r['annotation_approved'] and r['overlay_reviewed']
        assert sha(ROOT/r['label'])==r['label_sha256']
    for r in pr['samples']:
        assert r['decision']==('HOLD_BORDER_FLOWER_DECORATION' if r['original_id']=='44533174330f4238' else 'NO_VISIBLE_AUXILIARY_CUT_ARRANGEMENT')
    holds=[];rows=[];native_val=[]
    for old in a['samples']:
        assert sha(parent/old['image'])==old['sha256'] and sha(parent/old['label'])==old['label_sha256']
        if old['split']=='train' and old['original_id']=='44533174330f4238':
            holds.append(dict(original_id=old['original_id'],split='train',reason='Border flower decoration has unresolved auxiliary class; omitted from derived TRAIN only, parent retained'));continue
        r=dict(old);ip=out/r['image'];lp=out/r['label'];ip.parent.mkdir(parents=True,exist_ok=True);lp.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(parent/r['image'],ip)
        original=(parent/r['label']).read_text();assert all(line.split()[0]=='0' for line in original.splitlines())
        addition=(ROOT/aux[r['original_id']]['label']).read_text() if r['original_id'] in aux else ''
        lp.write_text(original+addition);r.update(parent_label_sha256=old['label_sha256'],label_sha256=sha(lp),auxiliary_instances=len(addition.splitlines()))
        if r['original_id']=='03abf85fbae6ceaa':r['auxiliary_annotation_status']='HOLD_MULTIPLE_FLORIST_BUCKETS';r['native_two_class_val_included']=False
        elif r['split']=='val':
            r['native_two_class_val_included']=True;native_val.append('./'+Path(r['image']).as_posix().replace('\\','/'))
        rows.append(r)
    # All 13 class0 validation images, including the unresolved auxiliary bucket image,
    # remain in audit/images/val for mandatory runtime-shaped PottedPlant acceptance.
    assert sum(r['split']=='val' for r in rows)==13 and len(native_val)==12
    (out/'native-val.txt').write_text('\n'.join(native_val)+'\n')
    a.update(samples=rows,unique_images=len(rows),names=['PottedPlant','CutArrangement'],parent_audit_sha256=sha(parent/'audit.json'),training_approved=True,training_exclusions=holds,annotation_review_inputs={str(p.relative_to(ROOT)):sha(p) for p in [train_review,val_review,positives]},counts={s:sum(r['split']==s for r in rows) for s in ['train','val','test']},native_two_class_val_images=12,target_runtime_val_images=13,auxiliary_validation_limitation='Native two-class fitness omits one unresolved auxiliary-label image; mandatory PottedPlant runtime gate must evaluate all13 VAL images including this hard negative',scope='Existing class0 labels and folds retained; auxiliary class1 never routes to Unity; one unresolved TRAIN photo held before new model inference',deployment_accepted=False)
    (out/'audit.json').write_text(json.dumps(a,indent=2));(out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='native-val.txt',test='images/test',names=a['names']),indent=2));print(json.dumps(dict(counts=a['counts'],aux_train=sum(r['auxiliary_instances'] for r in rows if r['split']=='train'),aux_val=sum(r['auxiliary_instances'] for r in rows if r['split']=='val'),native_val=12,mandatory_target_val=13)))
if __name__=='__main__':main()
