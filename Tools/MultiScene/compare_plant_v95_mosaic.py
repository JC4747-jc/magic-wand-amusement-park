"""Compare completed v89/v95 under identical runtime policy; never select on TEST."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    olddata = ROOT/'Datasets/plant-hanging-v88/audit.json'
    newdata = ROOT/'Datasets/plant-hanging-v88/audit.json'
    old, new = read(olddata), read(newdata)
    assert len(old['samples']) == len(new['samples']) == 200
    assert old == new and sha(olddata) == sha(newdata)
    val = [[r for r in d['samples'] if r['split'] == 'val'] for d in (old, new)]
    assert val[0] == val[1] and len(val[0]) == 30
    plans = [read(ROOT/'Reports'/f/'protocol.json') for f in
             ('plant-v89-hanging-diagnostic', 'plant-v95-mosaic-diagnostic')]
    assert plans[0]['initializer_sha256'] == plans[1]['initializer_sha256']
    options = [{k: v for k, v in p['parameters'].items() if k not in ('project', 'mosaic')} for p in plans]
    assert options[0] == options[1]
    assert plans[0]['parameters']['mosaic'] == 0. and plans[1]['parameters']['mosaic'] == .5
    results, evidence = {}, {}
    food = {'f6064c57134f3277e5fe7b20', '467fb80fca7896af512309a0',
            '0913dea122b9e30e10a04073', 'e890004cc7bf2354f7bdc5f3'}
    herbs = {'61b87cc9e422041b15538369', 'c40a67d7e5c2aa244969b1f6', 'ad4054926f2d2738dc4a8773'}
    reports = []
    for version, folder, data in [('v89', 'plant-v89-hanging-diagnostic', olddata),
                                  ('v95', 'plant-v95-mosaic-diagnostic', newdata)]:
        directory = ROOT/'Reports'/folder
        completed = read(directory/'completed.json')
        reportpath = directory/'final-val/report.json'
        report = read(reportpath)
        assert sha(reportpath) == completed['report_sha256']
        assert report['sha256'] == completed['weight_sha256']
        assert report['dataset_version'] == sha(data)
        assert report['images'] == 30 and report['split'] == 'val'
        assert report['allowed_classes'] == ['PottedPlant']
        assert report['operating_confidence'] == .35 and report['imgsz'] == 640
        assert report['rect'] and report['max_det'] == 8 and report['nms_iou'] == .7
        records = read(directory/'runtime-selection/selection.json')['records']
        assert len(records) == 30
        selected = [r for r in records if r['selected']][-1]
        assert selected['epoch'] == completed['selected_epoch']
        subsets = {}
        for name, stems, gt in [('harvested_food', food, 0), ('soil_herb', herbs, 4)]:
            rows = [r for r in report['per_image'] if Path(r['image'].replace(chr(92), '/')).stem in stems]
            assert len(rows) == len(stems) and sum(r['ground_truth'] for r in rows) == gt
            subsets[name] = dict(images=len(rows), ground_truth=gt,
                                 true_positive=sum(r['true_positive'] for r in rows),
                                 false_positive=sum(r['false_positive'] for r in rows),
                                 false_negative=sum(r['false_negative'] for r in rows))
        results[version] = dict(selected_epoch=selected['epoch'], weight_sha256=report['sha256'],
                                operating_per_class=report['operating_per_class'],
                                per_class_ap=report['per_class'], runtime_cohorts=selected['cohorts'],
                                runtime_gate_pass=bool(selected['rank'][0]), subsets=subsets)
        evidence[reportpath.relative_to(ROOT).as_posix()] = sha(reportpath)
        reports.append(report)
    # Preserve a direct image-by-image count comparison, including regressions.
    indexed = [{Path(r['image'].replace(chr(92), '/')).stem: r for r in p['per_image']} for p in reports]
    assert indexed[0].keys() == indexed[1].keys()
    per_image = []
    for stem, row in indexed[0].items():
        other = indexed[1][stem]
        assert row['ground_truth'] == other['ground_truth']
        keys = ('ground_truth', 'true_positive', 'false_positive', 'false_negative')
        per_image.append(dict(stem=stem, before={k: row[k] for k in keys},
                              after={k: other[k] for k in keys}))
    result = dict(status='CONTROLLED_MOSAIC_ONLY_COMPARISON', results=results,
                  unchanged_parent_rows=200, new_train_images=0, mosaic_before=0., mosaic_after=.5, unchanged_val_images=30,
                  initializer_and_other_options_exact=True, per_image=per_image, evidence=evidence,
                  limitation='Single seed; small repeatedly observed development VAL. Only Mosaic probability changes; one seed cannot establish robust superiority or independent generalization. Synthetic mosaics are not new independent source observations.',
                  test_used=False, pico_validated=False, deployment_accepted=False)
    out = ROOT/'Reports/plant-v95-mosaic-comparison.json'
    assert not out.exists(), 'Preserve existing comparison'
    out.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
