"""Compare completed fixed-policy v78/v81 validation without TEST selection."""
import hashlib
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]


def read(p): return json.loads(p.read_text(encoding='utf-8'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    olddata = ROOT/'Datasets/plant-houseplant-v75/audit.json'
    newdata = ROOT/'Datasets/plant-binary-v79/audit.json'
    old, new = read(olddata), read(newdata)
    before = [r for r in old['samples'] if r['split'] == 'val']
    after = [r for r in new['samples'] if r['split'] == 'val']
    assert len(before)==len(after)==30
    identity_keys=['image','label','sha256','split','group','source']
    assert all(all(a[k]==b[k] for k in identity_keys) for a,b in zip(old['samples'],new['samples']))
    from dataset_paths import manifest_relative_path as rel
    for a,b in zip(old['samples'],new['samples']):
        parent=(olddata.parent/rel(a['label'])).read_bytes();current=(newdata.parent/rel(b['label'])).read_bytes()
        assert [l for l in parent.splitlines() if l.strip() and l.split()[0]==b'0']==[l for l in current.splitlines() if l.strip()]
    assert all(a['split']==b['split'] for a,b in zip(old['samples'],new['samples']))
    oldplan = read(ROOT/'Reports/plant-v78-freeze-diagnostic/protocol.json')
    newplan = read(ROOT/'Reports/plant-v81-binary-diagnostic/protocol.json')
    init=read(ROOT/'Reports/plant-binary-initializer-v80/report.json')
    assert oldplan['initializer_sha256']==init['source_initializer_sha256'] and newplan['initializer_sha256']==init['initializer_sha256']
    assert {k:v for k,v in oldplan['parameters'].items() if k not in ('data','project','freeze')} == {k:v for k,v in newplan['parameters'].items() if k not in ('data','project','freeze')}
    assert oldplan['parameters']['freeze']==newplan['parameters']['freeze']==10
    results, evidence = {}, {}
    food = {'f6064c57134f3277e5fe7b20', '467fb80fca7896af512309a0', '0913dea122b9e30e10a04073', 'e890004cc7bf2354f7bdc5f3'}
    for version, folder, data in [('v78','plant-v78-freeze-diagnostic',olddata), ('v81','plant-v81-binary-diagnostic',newdata)]:
        path = ROOT/'Reports'/folder/('final-runtime-val' if version=='v78' else 'final-val')/'report.json'
        report = read(path)
        completed = read(path.parent.parent/'completed.json')
        assert report['dataset_version'] == sha(data)
        expected=read(path.parent.parent/'runtime-scope-audit.json')['final_report_sha256'] if version=='v78' else completed['report_sha256']
        assert report['sha256']==completed['weight_sha256'] and sha(path)==expected
        assert report['allowed_classes']==['PottedPlant']
        assert report['images'] == 30 and report['split'] == 'val'
        assert report['operating_confidence'] == .35 and report['imgsz'] == 640
        assert report['rect'] and report['max_det'] == 8 and report['nms_iou'] == .7
        items = [r for r in report['per_image'] if Path(r['image'].replace('\\','/')).stem in food]
        assert len(items) == 4 and all(r['ground_truth'] == 0 for r in items)
        herb_stems = {'61b87cc9e422041b15538369', 'c40a67d7e5c2aa244969b1f6', 'ad4054926f2d2738dc4a8773'}
        herbs = [r for r in report['per_image'] if Path(r['image'].replace(chr(92),'/')).stem in herb_stems]
        assert len(herbs) == 3 and sum(r['ground_truth'] for r in herbs) == 4
        records = read(path.parent.parent/'runtime-selection/selection.json')['records']
        selected = [r for r in records if r['selected']][-1]
        assert selected['epoch'] == completed['selected_epoch']
        results[version] = dict(selected_epoch=completed['selected_epoch'], operating_per_class=report['operating_per_class'],
            per_class_ap=report['per_class'], runtime_cohorts=selected['cohorts'], soil_herb_photos=herbs,
            harvested_food_negative_photos=items, harvested_food_false_positives=sum(r['false_positive'] for r in items),
            runtime_gate_pass=bool(selected['rank'][0]), weight_sha256=report['sha256'])
        evidence[path.relative_to(ROOT).as_posix()] = sha(path)
    result = dict(status='CONTROLLED_PLANT_ONLY_TASK_COMPARISON', results=results, evidence=evidence,
        unchanged_val_photos=30, unchanged_original_rows=194, changed_task=dict(before_classes=2,after_classes=1,auxiliary_targets_removed=True), all_image_folds_and_class0_label_lines_exact=True,
        limitation='Single seed and repeatedly observed small VAL. Auxiliary output/targets/task-loss dimensionality change together; this does not isolate a unique mechanism or prove independent generalization.',
        test_used=False, pico_validated=False, deployment_accepted=False)
    out = ROOT/'Reports/plant-v81-binary-comparison.json'
    assert not out.exists()
    out.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__': main()
