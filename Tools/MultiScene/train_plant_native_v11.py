"""Bounded real-cut-flower negative adaptation; fixed VAL gate, no TEST tuning."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    import torch,ultralytics
    from ultralytics import YOLO
    data=ROOT/'Datasets/plant-native-v11';out=ROOT/'Reports/plant-native-v11-training'
    assert not out.exists(),'Inspect existing run, do not duplicate'
    a=json.loads((data/'audit.json').read_text());assert len(a['samples'])==83
    assert a['parent_audit_sha256']=='e74e2af7b6be77fd03fc2c95544c6d27fe55911c3df25fc630b33d28db1f3cdc'
    for r in a['samples']:
        assert sha(data/r['image'])==r['sha256'] and sha(data/r['label'])==r['label_sha256']
    weight=ROOT/'object-detection-demo/weights/candidates/plant-native-v10/best.pt'
    assert sha(weight)=='d120ae894572ea1c685e9fc6548e5dddd95c8a87a027036526342b17025c718f'
    assert torch.cuda.is_available()
    options=dict(data=str(data/'data.yaml'),device=0,epochs=30,imgsz=640,batch=4,workers=0,seed=20261105,deterministic=True,freeze=0,optimizer='AdamW',lr0=.0001,lrf=.1,warmup_bias_lr=.0001,mosaic=0.,mixup=0.,cutmix=0.,scale=.3,translate=.05,hsv_h=.01,hsv_s=.3,hsv_v=.2,close_mosaic=0,project=str(out),name='train',plots=True)
    out.mkdir();(out/'run.json').write_text(json.dumps(dict(dataset_version=sha(data/'audit.json'),initializer_sha256=sha(weight),parameters=options,torch=torch.__version__,ultralytics=ultralytics.__version__,hypothesis='Four source-group-disjoint real cut-flower TRAIN negatives may reduce confusion; no threshold changes',protocol=dict(confidence=.65,imgsz=640,rect=True,nms_iou=.7,max_det=8),gates=dict(precision_min=.9,recall_min=.8),test_policy='VAL selection only; fresh independent TEST required after validation/negative gates',service_routed=False,pico_validated=False),indent=2))
    YOLO(weight).train(**options)
if __name__=='__main__':main()
