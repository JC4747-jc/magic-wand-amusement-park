"""Bounded confuser-negative experiment; keep all TEST sealed and runtime unchanged."""
import hashlib
import json
from pathlib import Path
import torch
import ultralytics
from ultralytics import YOLO
import ultralytics.nn.text_model as text_model

ROOT=Path(__file__).resolve().parents[2]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    data=ROOT/'Datasets/cockroach-confusers-v7'
    model=ROOT/'object-detection-demo/weights/candidates/cockroach-expanded-v5/best.pt'
    out=ROOT/'Reports/cockroach-confusers-v7-training'
    if out.exists():
        raise FileExistsError('Inspect existing process/weights before any restart')
    assert sha(data/'audit.json')=='09d9573cf1123d7d2b2351c72394159c42f4f756239df330148c10dd47c6d8de'
    assert sha(model)=='fd057af587c598d79a66fe1a6a10ee4e642a95ab68be3b041503722695f2d663'
    assert sha(ROOT/'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt')=='fd4a31001feb66127a8c527ffb1e0ba32bfe9f72566cfde4f2f9b5fc0c3e7ca7'
    audit=json.loads((data/'audit.json').read_text(encoding='utf-8'))
    assert audit['training_approved'] is True
    for row in audit['samples']:
        assert sha(data/row['image'])==row['sha256'] and sha(data/row['label'])==row['label_sha256']
    baseline=ROOT/'Reports/cockroach-confusers-v7-initial-runtime-val/report.json'
    report=json.loads(baseline.read_text())
    assert report['sha256']==sha(model) and report['dataset_version']==sha(data/'audit.json') and report['split']=='val'
    if not torch.cuda.is_available():
        raise RuntimeError('This recorded experiment requires the verified CUDA environment')
    text_model.WEIGHTS_DIR=ROOT/'Reports/pretrained-yoloworld'
    assert (text_model.WEIGHTS_DIR/'clip/ViT-B-32.pt').is_file()
    options=dict(data=str(data/'data.yaml'),device=0,epochs=30,imgsz=640,batch=4,workers=0,
        seed=20261105,deterministic=True,freeze=0,optimizer='AdamW',lr0=.0001,lrf=.1,warmup_bias_lr=.0001,
        mosaic=0.,mixup=0.,cutmix=0.,scale=.3,translate=.05,hsv_h=.01,hsv_s=.3,hsv_v=.2,
        close_mosaic=0,project=str(out),name='train',plots=True)
    out.mkdir()
    specification=dict(initializer=str(model),initializer_sha256=sha(model),dataset_version=sha(data/'audit.json'),
        parameters=options,torch=torch.__version__,ultralytics=ultralytics.__version__,
        cuda_device=torch.cuda.get_device_name(0),baseline_report_sha256=sha(baseline),script_sha256=sha(Path(__file__)),
        service_routed=False,pico_validated=False,scope='Periplaneta americana public-photo pilot',
        test_policy='Existing TEST retained sealed for training/selection; fresh independent acceptance needed',
        evaluation_protocol=dict(confidence=.55,imgsz=640,rect=True,nms_iou=.7,max_det=8),
        gates=dict(precision_min=.9,recall_min=.8,negative_false_positives_max=0),
        comparability='24 reviewed new TRAIN beetle negatives, 12 new VAL beetle negatives, fixed 30 epochs/AdamW1e-4; not a pure data-only ablation')
    (out/'run.json').write_text(json.dumps(specification,indent=2))
    YOLO(model).train(**options)


if __name__=='__main__':
    main()
