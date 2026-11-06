"""One bounded development run on reviewed payloads; OPEN hold prevents acceptance."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import shutil
from runtime_checkpoint_selection_v57 import RuntimeCheckpointSelectionV57
from dataset_paths import manifest_relative_path

ROOT = Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    import torch
    import ultralytics
    from ultralytics import YOLO
    out = ROOT / 'Reports/plant-v78-freeze-diagnostic'
    assert not out.exists(), 'Preserve existing run'
    data = ROOT / 'Datasets/plant-houseplant-v75'
    model = ROOT / 'Reports/plant-official-initializer-v55/initializer.pt'
    assert sha(data / 'audit.json') == '4f2edd8d341ab2b9bdcbcd8bd57f44ce9f3829ca586a5e780b494541618d91fe'
    assert sha(model) == 'e155a229ca5c197b40e2f2728d900bc495beca748895fb308950b4dc714595d3'
    audit = json.loads((data / 'audit.json').read_text())
    paths = {model, data/'audit.json', data/'data.yaml', data/'review_hold.json', Path(__file__).resolve(),
             ROOT/'Tools/MultiScene/evaluate_mapped.py', ROOT/'Tools/MultiScene/dataset_paths.py',
             ROOT/'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt'}
    for row in audit['samples']:
        for key, digest in [('image','sha256'), ('label','label_sha256')]:
            p = data / manifest_relative_path(row[key])
            assert sha(p) == row[digest]
            paths.add(p)
    paths.update([ROOT/'Reports/plant-official-initializer-v55/report.json',ROOT/'Reports/plant-official-initializer-v55/forward-probes.json',ROOT/'Tools/MultiScene/runtime_checkpoint_selection_v57.py',ROOT/'Tools/MultiScene/test_runtime_checkpoint_selection_v57.py',ROOT/'Datasets/plant-cut-v35/audit.json'])
    for name in ['Datasets/plant-label-paths-v52/audit.json', 'Reports/plant-v58-runtime-selected/protocol.json', 'Reports/plant-food-v60-integrity.json', 'Reports/plant-food-v60-loader-check.json', 'Reports/plant-food-v60-label-discovery-audit.json', 'Tools/MultiScene/prepare_plant_food_v60.py', 'Tools/MultiScene/audit_plant_food_v60.py', 'Reports/plant-food-train-v59-original-review.json', 'Reports/plant-food-train-v59-pixel-history.json', 'Reports/plant-food-train-v59-identity-history-v2.json', 'Reports/plant-food-train-v59-creator-links.json', 'Reports/plant-food-train-v59-preacquisition.json', 'Datasets/plant-food-train-source-v59/manifest.json', 'Datasets/plant-food-train-source-v59/train-reservation.json']:
        paths.add(ROOT/name)
    for name in ['Datasets/plant-food-v60/audit.json','Reports/plant-v61-food-diagnostic/protocol.json','Reports/plant-planters-v65-integrity.json','Reports/plant-planters-v65-loader-check.json','Tools/MultiScene/prepare_plant_herb_v63.py','Tools/MultiScene/prepare_plant_planters_v65.py','Tools/MultiScene/audit_plant_planters_v65.py','Reports/plant-herb-train-v62-boxes/reviewed-three.json','Reports/plant-herb-planters-v64-boxes/reviewed-one.json','Reports/plant-herb-train-v62-pixel-history.json','Reports/plant-herb-train-v62-identity-history-v2.json','Reports/plant-herb-train-v62-creator-links.json','Datasets/plant-herb-train-source-v62/manifest.json','Datasets/plant-herb-train-source-v62/train-reservation.json']:
        paths.add(ROOT/name)
    for name in ['Datasets/plant-planters-v65/audit.json','Reports/plant-v66-herb-diagnostic/protocol.json','Tools/MultiScene/prepare_plant_market_v68.py','Tools/MultiScene/audit_plant_market_v68.py','Reports/plant-market-v68-integrity.json','Reports/plant-market-v68-loader-check.json','Reports/plant-market-train-v67-original-review.json','Reports/plant-market-train-v67-pixel-history.json','Reports/plant-market-train-v67-identity-history-v2.json','Reports/plant-market-train-v67-creator-links.json','Reports/plant-market-train-v67-preacquisition.json','Datasets/plant-market-train-source-v67/manifest.json','Datasets/plant-market-train-source-v67/train-reservation.json']:
        paths.add(ROOT/name)
    for name in ['Datasets/plant-market-v68/audit.json','Reports/plant-v69-market-diagnostic/protocol.json','Reports/plant-hanging-v72-integrity.json','Reports/plant-hanging-v72-loader-check.json','Tools/MultiScene/prepare_plant_hanging_v72.py','Tools/MultiScene/audit_plant_hanging_v72.py','Tools/MultiScene/draft_plant_hanging_v71.py','Reports/plant-hanging-v71-boxes/reviewed-one.json','Datasets/plant-hanging-v72/audit.json','Reports/plant-houseplant-v75-integrity.json','Reports/plant-houseplant-v75-loader-check.json','Tools/MultiScene/prepare_plant_houseplant_v75.py','Tools/MultiScene/audit_plant_houseplant_v75.py','Tools/MultiScene/draft_plant_houseplant_v74.py','Tools/MultiScene/record_plant_houseplant_v73_review.py','Reports/plant-houseplant-v74-boxes/reviewed-seven.json']:
        paths.add(ROOT/name)
    for stem,version in [('hanging','70'),('houseplant','73')]:
        for name in ['manifest.json','train-reservation.json']:
            paths.add(ROOT/f'Datasets/plant-{stem}-train-source-v{version}/{name}')
        for suffix in ['original-review','pixel-history','identity-history-v2','creator-links','preacquisition']:
            paths.add(ROOT/f'Reports/plant-{stem}-train-v{version}-{suffix}.json')
    for name in ['Reports/plant-v76-positive-diagnostic/protocol.json','Reports/plant-v77-candidate-diagnostic/protocol.json','Reports/plant-v77-candidate-diagnostic/report.json','Reports/plant-v77-candidate-diagnostic/findings.json','Tools/MultiScene/diagnose_plant_candidates_v77.py','Tools/MultiScene/analyze_plant_candidates_v77.py','Reports/plant-v78-freeze-preflight/report.json','Reports/plant-v78-freeze-preflight/installed-model-train.txt','Tools/MultiScene/preflight_plant_freeze_v78.py']:
        paths.add(ROOT/name)
    freeze_preflight=json.loads((ROOT/'Reports/plant-v78-freeze-preflight/report.json').read_text())
    assert freeze_preflight['frozen_bn_buffers_unchanged_after_actual_forward']
    trainer_source=Path(freeze_preflight['trainer_source_path']);assert sha(trainer_source)==freeze_preflight['trainer_source_sha256'];paths.add(trainer_source)
    files = [dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in sorted(paths)]
    opts = dict(data=str(data/'data.yaml'), device=0, epochs=30, patience=30, imgsz=640, batch=4,
        workers=0, seed=20261105, deterministic=True, freeze=10, optimizer='AdamW', lr0=.0001,
        lrf=.1, warmup_bias_lr=.0001, mosaic=0., mixup=0., cutmix=0., scale=.1, translate=.05,
        hsv_h=.01, hsv_s=.3, hsv_v=.2, close_mosaic=0, project=str(out), name='train', plots=True)
    previous=json.loads((ROOT/'Reports/plant-v76-positive-diagnostic/protocol.json').read_text())
    assert {k:v for k,v in opts.items() if k not in ('data','project','freeze')}=={k:v for k,v in previous['parameters'].items() if k not in ('data','project','freeze')}
    old=json.loads((ROOT/'Datasets/plant-houseplant-v75/audit.json').read_text())
    assert len(old['samples'])==194 and len(audit['samples'])==194
    for before,after in zip(old['samples'],audit['samples']):
        assert before==after
    from ultralytics.data.dataset import YOLODataset
    from ultralytics.data.utils import img2label_paths
    for row in audit['samples']:
        inferred=Path(img2label_paths([str(data/manifest_relative_path(row['image']))])[0])
        assert inferred.resolve()==(data/manifest_relative_path(row['label'])).resolve()
        assert sha(inferred)==row['label_sha256']
    actual=YOLODataset(img_path=str(data/'images/train'),data={'names':{0:'PottedPlant',1:'CutArrangement'},'nc':2},imgsz=640,augment=False,batch_size=4)
    actualcounts={0:0,1:0}
    for label in actual.labels:
        for value in label['cls'].reshape(-1):actualcounts[int(value)]+=1
    assert len(actual)==160 and actualcounts=={0:147,1:32}
    del actual
    plan = dict(status='DIAGNOSTIC_TRAINING_ONLY', hypothesis='Same initializer, exact same194 dataset rows/payloads/folds, thirty epochs and options as v76 except freeze0->10. Installed trainer holds frozen BatchNorm in eval mode. Test whether preserving backbone state improves raw-score/recognition retention without increasing false positives; no acceptance presumed.',
        initializer_sha256=sha(model), files=files, parameters=opts,
        budget=dict(runs=1, epochs=30, concurrent_gpu_jobs=1),
        selection='Frozen v57 runtime selector every epoch: gate full30 AND original13 P>=.9/R>=.8 then fullF1,oldF1,P,R,AP95; earliest exact tie. Native best retained for diagnostic only. TEST never selects.',
        evaluation=dict(images=30, operating_confidence=.35, imgsz=640, rect=True, nms_iou=.7,max_det=8),
        limitations=['OPEN annotation hold retained; this run cannot establish acceptance.',
            'Previously observed VAL is development; independent TEST and PICO remain required.',
            'Do not overwrite deployment or alter historical rejection.'],
        torch=torch.__version__, ultralytics=ultralytics.__version__, pid=os.getpid(), deployment_accepted=False)
    lock = ROOT / 'Reports/autoresearch-gpu.lock'
    with lock.open('x') as f:
        f.write(str(os.getpid()))
    try:
        out.mkdir()
        (out/'protocol.json').write_text(json.dumps(plan,indent=2),encoding='utf-8')
        start=time.perf_counter()
        old_audit=json.loads((ROOT/'Datasets/plant-cut-v35/audit.json').read_text())
        old_images=[r['image'] for r in old_audit['samples'] if r['split']=='val']
        selector=RuntimeCheckpointSelectionV57(data,out/'runtime-selection',old_images,python=sys.executable)
        runner=YOLO(model)
        initial_model=YOLO(model).model.float()
        frozen_prefixes=tuple(f'model.{i}.' for i in range(10))
        expected={k:v.detach().cpu().clone() for k,v in initial_model.state_dict().items() if k.startswith(frozen_prefixes)}
        freeze_records=[]
        def check_frozen(trainer):
            epoch=trainer.epoch+1
            if freeze_records and freeze_records[-1]['epoch']==epoch:return
            actual=trainer.model.state_dict()
            assert all(torch.equal(actual[k].detach().cpu(),v) for k,v in expected.items()), 'Frozen backbone state drifted'
            names=[n for n,p in trainer.model.named_parameters() if n.startswith(frozen_prefixes)]
            assert names and all(not p.requires_grad for n,p in trainer.model.named_parameters() if n.startswith(frozen_prefixes))
            bns=[(n,m) for n,m in trainer.model.named_modules() if n.startswith(frozen_prefixes) and isinstance(m,torch.nn.BatchNorm2d)]
            assert len(bns)==39 and all(not m.training for n,m in bns)
            freeze_records.append(dict(epoch=epoch,frozen_parameters=len(names),frozen_bn=len(bns),all_frozen_state_exact=True))
            (out/'freeze-runtime.json').write_text(json.dumps(dict(records=freeze_records,initializer_sha256=sha(model),expected_state_tensors=len(expected),deployment_accepted=False),indent=2),encoding='utf-8')
        runner.add_callback('on_train_batch_start',check_frozen)
        runner.add_callback('on_model_save',selector)
        runner.train(**opts)
        assert len(selector.records)==30
        assert [r['epoch'] for r in freeze_records]==list(range(1,31))
        best=out/'train/weights/best.pt'
        shutil.copy2(best,out/'train/weights/native-fitness-best.pt')
        shutil.copy2(selector.best,best)
        from ultralytics.utils.torch_utils import strip_optimizer
        strip_optimizer(best)
        subprocess.run([sys.executable,'-X','utf8',str(ROOT/'Tools/MultiScene/evaluate_mapped.py'),
            '--model',str(best),'--data',str(data),'--out',str(out/'final-val'), '--split','val',
            '--imgsz','640','--operating-conf','.35','--max-det','8','--nms-iou','.7'],cwd=ROOT,check=True)
        for row in files:
            assert sha(ROOT/row['path'])==row['sha256'],row['path']
        report=json.loads((out/'final-val/report.json').read_text())
        assert report['images']==30 and report['sha256']==sha(best)
        result=dict(status='DIAGNOSTIC_COMPLETE_NOT_ACCEPTED',weight_sha256=sha(best),
                    report_sha256=sha(out/'final-val/report.json'),frozen_files_verified=len(files),
                    elapsed_seconds=time.perf_counter()-start,deployment_accepted=False,
                    operating_per_class=report['operating_per_class'],runtime_rank=selector.best_rank,selected_epoch=[r['epoch'] for r in selector.records if r['selected']][-1])
        (out/'completed.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
        print(json.dumps(result),flush=True)
    except Exception as exc:
        (out/'failure.json').write_text(json.dumps(dict(error=repr(exc),deployment_accepted=False)),encoding='utf-8')
        raise
    finally:
        lock.unlink()


if __name__=='__main__':
    main()
