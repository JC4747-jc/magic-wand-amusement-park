"""Build TEST-only public pilot from pre-inference reviewed sources, never train."""
import hashlib
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
    review = ROOT/'Reports/statue-independent-v22-boxes/reviewed.json'
    d = read(review)
    assert d['status']=='ORIGINALS_AND_FINAL_OVERLAYS_REVIEWED_PREINFERENCE'
    assert d['labels_approved'] and not d['model_inference_performed']
    assert d['candidate_sha256']==sha(ROOT/'object-detection-demo/weights/candidates/statue-oiv7m-vr-negatives-v21/best.pt')
    for item in d['inputs']:
        assert sha(ROOT/item['path'])==item['sha256']
    out = ROOT/'Datasets/statue-independent-test-v22'
    assert not out.exists()
    rows=[]
    holds=[]
    for row in d['samples']:
        assert sha(ROOT/row['image'])==row['sha256']
        if row['status']=='HOLD':
            holds.append(row)
            continue
        assert row['status']=='ORIGINAL_AND_OVERLAY_REVIEWED_BOX_APPROVED'
        assert row['original_reviewed'] and row['overlay_reviewed']
        assert sha(ROOT/row['overlay'])==row['overlay_sha256']
        source = ROOT/f"Datasets/statue-{row['source_pool']}-independent-source-v22"
        m = read(source/'manifest.json')
        s = next(s for s in m['samples'] if s['page_id']==row['page_id'])
        pixels=read(ROOT/f"Reports/statue-independent-v22-{row['source_pool']}-pixel-history.json")
        assert not pixels['errors'] and not pixels['matches'] and not pixels['internal_matches']
        assert pixels['enumerated_paths']==pixels['scanned_paths']
        assert pixels['source_manifest_sha256']==sha(source/'manifest.json')
        dest=out/'images/test'/Path(row['image']).name
        dest.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(ROOT/row['image'],dest)
        label=out/'labels/test'/(dest.stem+'.txt')
        label.parent.mkdir(parents=True,exist_ok=True)
        w,h=row['dimensions']
        lines=[]
        for x1,y1,x2,y2 in row['boxes']:
            assert 0<=x1<x2<=w and 0<=y1<y2<=h
            lines.append(f'0 {(x1+x2)/(2*w):.8f} {(y1+y2)/(2*h):.8f} {(x2-x1)/w:.8f} {(y2-y1)/h:.8f}\n')
        label.write_text(''.join(lines),encoding='utf-8')
        rows.append(dict(s,image=dest.relative_to(out).as_posix(),label=label.relative_to(out).as_posix(),
                         label_sha256=sha(label),group=row['test_group'],original_group=s['group'],
                         split='test',annotation_status='REVIEWED',review_approved=True,
                         training_approved=False,review=review.relative_to(ROOT).as_posix()))
    audit=dict(names=['Statue'],samples=rows,split_counts={'test':len(rows)},
               unique_images=len(rows),source_groups=len({s['group'] for s in rows}),
               ground_truth_instances=sum(len(s['boxes']) for s in d['samples'] if s['status']!='HOLD'),
               sources=[dict(id='commons-statue-independent-v22',
                   url='https://commons.wikimedia.org/',license='Per-image CC BY / CC BY-SA; preserve attribution and ShareAlike',reviewed=True)],
               review_sha256=sha(review),preacquisition_sha256=sha(ROOT/'Reports/statue-independent-v22-preacquisition.json'),
               training_approved=False,deployment_accepted=False,
               held_samples=holds,
               limitations=['Single assistant annotator; no independent second annotation.',
                   'Public sources may overlap pretrained model data; unknown pretraining independence.',
                   'TEST pilot only; no target-positive PICO images, no device acceptance.',
                   'Two whole-image holds selected for identity/semantic ambiguity before any inference; retained TEST-only forever.'])
    (out/'audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (out/'data.yaml').write_text(json.dumps(dict(path=str(out),train='images/train',val='images/val',test='images/test',names=['Statue']),indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(status='PASS',images=len(rows),groups=audit['source_groups'],GT=audit['ground_truth_instances'],holds=len(holds),audit_sha256=sha(out/'audit.json'))))
if __name__=='__main__':main()
