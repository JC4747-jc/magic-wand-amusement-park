"""Bounded native YOLOv8m comparison on reviewed TRAIN/VAL; no TEST selection."""
import hashlib
import json
from pathlib import Path
import torch
import ultralytics
from ultralytics import YOLO

ROOT=Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    data=ROOT/'Datasets/cockroach-positive-train-v11'
    initializer=ROOT/'Reports/pretrained-yolov8m/yolov8m.pt'
    out=ROOT/'Reports/cockroach-native-v13-training'
    if out.exists():
        raise FileExistsError('Inspect the existing process and artifacts before restarting')
    assert sha(data/'audit.json')=='7a9ad7fc58abf6de5ed9c9f7ef72fbae37eb374a06a40dc0cd0693a590a73837'
    assert sha(initializer)=='5d4a90cdc7a21786cc59cd19778e9eafff836df9e2da32524737c7ee6efe4fe5'
    assert sha(ROOT/'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt')=='fd4a31001feb66127a8c527ffb1e0ba32bfe9f72566cfde4f2f9b5fc0c3e7ca7'
    audit=json.loads((data/'audit.json').read_text())
    assert audit['training_approved'] and audit['counts']==dict(train=161,val=39,test=9)
    for row in audit['samples']:
        assert sha(data/row['image'])==row['sha256'] and sha(data/row['label'])==row['label_sha256']
    comparator=ROOT/'Reports/cockroach-positive-train-v11-runtime-val/report.json'
    report=json.loads(comparator.read_text())
    assert report['dataset_version']==sha(data/'audit.json') and report['split']=='val'
    assert report['sha256']=='8ab80feefdce415c286f45a22e8e73f653a2260041514104fddd73efcf46281c'
    if not torch.cuda.is_available():
        raise RuntimeError('Recorded experiment requires CUDA')
    model=YOLO(initializer)
    assert type(model.model).__name__=='DetectionModel' and len(model.names)==80
    assert not any('roach' in name.lower() for name in model.names.values())
    options=dict(data=str(data/'data.yaml'),device=0,epochs=60,imgsz=640,batch=4,workers=0,
        seed=20261105,deterministic=True,freeze=0,optimizer='AdamW',lr0=.0005,lrf=.1,
        warmup_bias_lr=.0005,mosaic=.3,mixup=0.,cutmix=0.,scale=.3,translate=.05,
        hsv_h=.01,hsv_s=.3,hsv_v=.2,close_mosaic=10,project=str(out),name='train',plots=True)
    spec=dict(initializer=str(initializer),initializer_sha256=sha(initializer),
        initializer_source=json.loads((initializer.parent/'source.json').read_text()),
        initializer_scope='COCO80 has no Cockroach class; trainer creates a single-class detector head',
        dataset_version=sha(data/'audit.json'),parameters=options,torch=torch.__version__,
        ultralytics=ultralytics.__version__,cuda_device=torch.cuda.get_device_name(0),
        comparator_validation_sha256=sha(comparator),script_sha256=sha(Path(__file__)),
        service_routed=False,pico_validated=False,scope='Periplaneta americana public-photo pilot',
        test_policy='No historical or fresh TEST for model/threshold selection; new independent acceptance required',
        evaluation_protocol=dict(confidence=.65,imgsz=640,rect=True,nms_iou=.7,max_det=8),
        gates=dict(precision_min=.9,recall_min=.8,negative_false_positives_max=0),
        comparability='Architecture, initialization, learning rate, augmentation and epochs differ; not a pure architecture ablation')
    out.mkdir();(out/'run.json').write_text(json.dumps(spec,indent=2))
    model.train(**options)


if __name__=='__main__':
    main()
