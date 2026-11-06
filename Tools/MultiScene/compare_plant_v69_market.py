"""Compare completed fixed-policy v66/v69 validation without TEST selection."""
import hashlib
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]


def read(p): return json.loads(p.read_text(encoding='utf-8'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    olddata = ROOT/'Datasets/plant-planters-v65/audit.json'
    newdata = ROOT/'Datasets/plant-market-v68/audit.json'
    old, new = read(olddata), read(newdata)
    before = [r for r in old['samples'] if r['split'] == 'val']
    after = [r for r in new['samples'] if r['split'] == 'val']
    assert before == after and len(before) == 30
    oldplan = read(ROOT/'Reports/plant-v66-herb-diagnostic/protocol.json')
    newplan = read(ROOT/'Reports/plant-v69-market-diagnostic/protocol.json')
    assert oldplan['initializer_sha256'] == newplan['initializer_sha256']
    assert {k:v for k,v in oldplan['parameters'].items() if k not in ('data','project')} == {k:v for k,v in newplan['parameters'].items() if k not in ('data','project')}
    results, evidence = {}, {}
    food = {'f6064c57134f3277e5fe7b20', '467fb80fca7896af512309a0', '0913dea122b9e30e10a04073', 'e890004cc7bf2354f7bdc5f3'}
    for version, folder, data in [('v66','plant-v66-herb-diagnostic',olddata), ('v69','plant-v69-market-diagnostic',newdata)]:
        path = ROOT/'Reports'/folder/'final-val/report.json'
        report = read(path)
        completed = read(path.parent.parent/'completed.json')
        assert report['dataset_version'] == sha(data)
        assert report['sha256'] == completed['weight_sha256'] and sha(path) == completed['report_sha256']
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
    result = dict(status='CONTROLLED_MARKET_NEGATIVE_ADDITION_COMPARISON', results=results, evidence=evidence,
        unchanged_val_photos=30, unchanged_original_rows=174, new_train_market_negative_photos=12,
        limitation='Single seed, small repeatedly observed development VAL; changes in batch count/order also follow data addition. Not an independent TEST or unique causal proof.',
        test_used=False, pico_validated=False, deployment_accepted=False)
    out = ROOT/'Reports/plant-v69-market-comparison.json'
    assert not out.exists()
    out.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__': main()
