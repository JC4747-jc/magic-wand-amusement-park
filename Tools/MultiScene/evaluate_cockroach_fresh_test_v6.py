"""Release reviewed fresh positive/confuser TEST cohort, evaluate one frozen v5 model."""
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def load(p):
    return json.loads(p.read_text(encoding='utf-8'))


def main():
    frozen=ROOT/'Reports/cockroach-v5-fresh-v6-preannotation-plan.json'
    plan=load(frozen)
    assert plan['protocol']==dict(confidence=.55,imgsz=640,rect=True,nms_iou=.7,max_det=8)
    model=ROOT/'object-detection-demo/weights/candidates/cockroach-expanded-v5/best.pt'
    assert sha(model)==plan['model_sha256']=='fd057af587c598d79a66fe1a6a10ee4e642a95ab68be3b041503722695f2d663'
    assert sha(ROOT/'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt')=='fd4a31001feb66127a8c527ffb1e0ba32bfe9f72566cfde4f2f9b5fc0c3e7ca7'
    data=ROOT/'Datasets/cockroach-fresh-test-v6';out=ROOT/'Reports/cockroach-v5-conf055-fresh-test-v6'
    release=ROOT/'Reports/cockroach-fresh-test-v6-evaluation-plan.json'
    assert not any(p.exists() for p in [data,out,release]), 'Preserve prior TEST release/results'
    groups=set();samples=[];reviews=[]
    parent=load(ROOT/'Datasets/cockroach-expanded-v5/audit.json')
    parent_groups={r['group'] for r in parent['samples']}
    for kind,folder,review_path,key in [
        ('positive','cockroach-fresh-positive-v6-source','cockroach-fresh-v6-positive-label-review/reviewed.json','positive_manifest_sha256'),
        ('beetle','cockroach-beetle-confuser-test-v6-source','cockroach-fresh-v6-negative-review.json','confuser_manifest_sha256')]:
        source=ROOT/'Datasets'/folder;manifest=load(source/'manifest.json')
        review_file=ROOT/'Reports'/review_path;review=load(review_file)
        assert sha(source/'manifest.json')==plan[key]==review['source_manifest_sha256']
        assert review['training_approved'] is False
        if kind=='positive': assert review['evaluation_approved'] is True
        rows={r['original_id']:r for r in manifest['samples']}
        assert len(rows)==len(review['samples'])==16
        for suffix in ['model-input-pixel','pixel-history']:
            check=load(ROOT/f'Reports/cockroach-fresh-v6-{kind}-{suffix}.json')
            assert check['source_manifest_sha256']==plan[key]
            assert not any(check[k] for k in ['errors','matches','internal_matches'])
        for annotation in review['samples']:
            row=rows[annotation['original_id']]
            assert row['group'] not in groups|parent_groups
            groups.add(row['group'])
            assert annotation['original_viewed'] and row['reserved_split']=='test' and row['training_approved'] is False
            assert sha(source/row['image'])==row['sha256']
            if kind=='positive':
                assert annotation['overlay_reviewed'] and annotation['source_sha256']==row['sha256']
                label=ROOT/annotation['label'];assert sha(label)==annotation['label_sha256']
                label_bytes=label.read_bytes()
            else:
                assert annotation['annotation_status']=='REVIEWED_NO_VISIBLE_COCKROACH'
                assert annotation['image_sha256']==row['sha256'] and not annotation['boxes_xyxy']
                label_bytes=b''
            image_dest=f'images/test/{Path(row["image"]).name}'
            label_dest=f'labels/test/{Path(row["image"]).stem}.txt'
            (data/image_dest).parent.mkdir(parents=True,exist_ok=True)
            (data/label_dest).parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(source/row['image'],data/image_dest)
            (data/label_dest).write_bytes(label_bytes)
            samples.append(dict(row,image=image_dest,label=label_dest,label_sha256=sha(data/label_dest),split='test',cohort=kind))
        reviews.append(dict(path=str(review_file),sha256=sha(review_file)))
    audit=dict(names=['Cockroach'],samples=samples,training_approved=False,evaluation_approved=True,
        reviews=reviews,sources=[dict(id='inaturalist',license='Per-photo CC BY or CC0; attribution retained in samples',reviewed=True)],
        limitations=['32 public photos, 32 publisher observers; not a household or PICO benchmark',
            'Beetles mostly ladybirds/leaf beetles with some specimen images',
            'Publisher taxonomy not independently expert verified'])
    (data/'audit.json').write_text(json.dumps(audit,indent=2))
    (data/'data.yaml').write_text(json.dumps(dict(path=data.as_posix(),test='images/test',names=['Cockroach']),indent=2))
    evaluation_plan=dict(preannotation_plan_sha256=sha(frozen),model_sha256=sha(model),data_sha256=sha(data/'audit.json'),
        protocol=plan['protocol'],gates=dict(precision_min=.9,recall_min=.8,negative_false_positives_max=0),
        policy=plan['policy'],limitations=audit['limitations'])
    release.write_text(json.dumps(evaluation_plan,indent=2))
    subprocess.run([sys.executable,str(ROOT/'Tools/MultiScene/evaluate_mapped.py'),'--model',str(model),'--data',str(data),
        '--out',str(out),'--split','test','--imgsz','640','--operating-conf','.55','--allowed-classes','Cockroach',
        '--max-det','8','--nms-iou','.7'],cwd=ROOT,check=True)
    report=load(out/'report.json');assert report['sha256']==sha(model)
    stats=report['operating_per_class'][0]
    negative_paths={r['image'] for r in samples if r['cohort']=='beetle'}
    negative_fp=sum(r['false_positive'] for r in report['per_image'] if r['image'].replace('\\','/') in negative_paths)
    assert len([r for r in report['per_image'] if r['image'].replace('\\','/') in negative_paths])==16
    passed=(stats['precision'] or 0)>=.9 and (stats['recall'] or 0)>=.8 and negative_fp==0
    decision=dict(status='PUBLIC_PILOT_TEST_PASSED_PICO_PENDING' if passed else 'REJECTED_FRESH_TEST',
        operating=stats,negative_images=16,negative_false_positives=negative_fp,
        model_sha256=sha(model),plan_sha256=sha(release),report_sha256=sha(out/'report.json'),
        deployment_changed=False,pico_test='NOT_PERFORMED',limitations=audit['limitations'])
    (out/'decision.json').write_text(json.dumps(decision,indent=2));print(json.dumps(decision))


if __name__=='__main__':
    main()
