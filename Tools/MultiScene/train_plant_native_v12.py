"""Bounded real-cut-flower negative adaptation; fixed VAL gate, no TEST tuning."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    import torch,ultralytics
    from ultralytics import YOLO
    data=ROOT/'Datasets/plant-native-v12';out=ROOT/'Reports/plant-native-v12-training'
    assert not out.exists(),'Inspect existing run, do not duplicate'
    a=json.loads((data/'audit.json').read_text());assert len(a['samples'])==85
    assert a['parent_audit_sha256']=='de41a31a94cdb7c4c435c2a020cbb386970cc971291da71ff65ebbb6b13bf8ea'
    for r in a['samples']:
        assert sha(data/r['image'])==r['sha256'] and sha(data/r['label'])==r['label_sha256']
    weight=ROOT/'object-detection-demo/weights/candidates/plant-native-v11/best.pt'
    assert sha(weight)=='0f91ac17fc3966a3185447181c80e6cc7e700fe0176f6e0d3cc9e377580baa1f'
    assert torch.cuda.is_available()
    options=dict(data=str(data/'data.yaml'),device=0,epochs=30,imgsz=640,batch=4,workers=0,seed=20261105,deterministic=True,freeze=0,optimizer='AdamW',lr0=.0001,lrf=.1,warmup_bias_lr=.0001,mosaic=0.,mixup=0.,cutmix=0.,scale=.3,translate=.05,hsv_h=.01,hsv_s=.3,hsv_v=.2,close_mosaic=0,project=str(out),name='train',plots=True)
    out.mkdir();(out/'run.json').write_text(json.dumps(dict(dataset_version=sha(data/'audit.json'),initializer_sha256=sha(weight),parameters=options,torch=torch.__version__,ultralytics=ultralytics.__version__,hypothesis='Two new reviewed flowering-container TRAIN images/four boxes may improve positive recall; fixed .35 inherited from previous VAL selection',protocol=dict(confidence=.35,imgsz=640,rect=True,nms_iou=.7,max_det=8),gates=dict(precision_min=.9,recall_min=.8),test_policy='VAL selection only; fresh independent TEST required after validation/negative gates',service_routed=False,pico_validated=False),indent=2))
    YOLO(weight).train(**options)
if __name__=='__main__':main()
