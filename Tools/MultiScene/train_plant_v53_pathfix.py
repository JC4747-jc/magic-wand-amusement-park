"""One bounded development run on reviewed payloads; OPEN hold prevents acceptance."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time
from dataset_paths import manifest_relative_path

ROOT = Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    import torch
    import ultralytics
    from ultralytics import YOLO
    out = ROOT / 'Reports/plant-v53-pathfix-diagnostic'
    assert not out.exists(), 'Preserve existing run'
    data = ROOT / 'Datasets/plant-label-paths-v52'
    model = ROOT / 'object-detection-demo/weights/candidates/plant-cut-v36/best.pt'
    assert sha(data / 'audit.json') == '39681aca2f7ca939ff7f19fcd7bca6f61a87ee7f558631be0e8aa38a1aba88e8'
    assert sha(model) == '9778ae05560fcdc3882a56a303783b3e3c3b8471a7c39cba22e77af20ea32a82'
    audit = json.loads((data / 'audit.json').read_text())
    paths = {model, data/'audit.json', data/'data.yaml', data/'review_hold.json', Path(__file__).resolve(),
             ROOT/'Tools/MultiScene/evaluate_mapped.py', ROOT/'Tools/MultiScene/dataset_paths.py',
             ROOT/'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt'}
    for row in audit['samples']:
        for key, digest in [('image','sha256'), ('label','label_sha256')]:
            p = data / manifest_relative_path(row[key])
            assert sha(p) == row[digest]
            paths.add(p)
    files = [dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in sorted(paths)]
    opts = dict(data=str(data/'data.yaml'), device=0, epochs=30, patience=30, imgsz=640, batch=4,
        workers=0, seed=20261105, deterministic=True, freeze=0, optimizer='AdamW', lr0=.0001,
        lrf=.1, warmup_bias_lr=.0001, mosaic=0., mixup=0., cutmix=0., scale=.1, translate=.05,
        hsv_h=.01, hsv_s=.3, hsv_v=.2, close_mosaic=0, project=str(out), name='train', plots=True)
    previous=json.loads((ROOT/'Reports/plant-v51-diagnostic/protocol.json').read_text())
    assert {k:v for k,v in opts.items() if k not in ('data','project')}=={k:v for k,v in previous['parameters'].items() if k not in ('data','project')}
    old=json.loads((ROOT/'Datasets/plant-hanging-v50/audit.json').read_text())
    assert len(old['samples'])==len(audit['samples'])==161
    for before,after in zip(old['samples'],audit['samples']):
        assert {k:v for k,v in before.items() if k!='label'}=={k:v for k,v in after.items() if k!='label'}
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
    assert len(actual)==127 and actualcounts=={0:133,1:32}
    del actual
    plan = dict(status='DIAGNOSTIC_TRAINING_ONLY', hypothesis='Controlled label-discovery repair versus v51: same initializer, parameters, image/label bytes and splits; only two TRAIN label paths repaired for actual YOLO discovery.',
        initializer_sha256=sha(model), files=files, parameters=opts,
        budget=dict(runs=1, epochs=30, concurrent_gpu_jobs=1),
        selection='Ultralytics native fitness best across full30VAL, exact ties use framework behavior. Evaluate only that best afterward at fixed operating policy; do not compare TEST/checkpoints/thresholds.',
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
        YOLO(model).train(**opts)
        best=out/'train/weights/best.pt'
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
                    operating_per_class=report['operating_per_class'])
        (out/'completed.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
        print(json.dumps(result),flush=True)
    except Exception as exc:
        (out/'failure.json').write_text(json.dumps(dict(error=repr(exc),deployment_accepted=False)),encoding='utf-8')
        raise
    finally:
        lock.unlink()


if __name__=='__main__':
    main()
