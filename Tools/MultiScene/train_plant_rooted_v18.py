"""Train confuser v15 with v14 initialization/settings for a data-focused comparison."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    import torch,ultralytics
    from ultralytics import YOLO
    data=ROOT/'Datasets/plant-rooted-v18';out=ROOT/'Reports/plant-rooted-v18-training';assert not out.exists()
    assert sha(data/'audit.json')=='f3eefa042dd1ea24acc10213dfb4eb15aeab4887ee5f52e21b71e91a486f6616'
    a=json.loads((data/'audit.json').read_text());assert a['training_approved'] and a['counts']==dict(train=92,val=13,test=4)
    assert a['target_runtime_val_images']==13 and len((data/'native-val.txt').read_text().splitlines())==12
    for r in a['samples']:
        assert sha(data/r['image'])==r['sha256'] and sha(data/r['label'])==r['label_sha256']
    weight=ROOT/'object-detection-demo/weights/candidates/plant-native-v12/best.pt';assert sha(weight)=='fe706e1763a8ffa5924c849d9798bb1083d416df6c8c6720cb6ffcdb3780177a'
    assert sha(ROOT/'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt')=='fd4a31001feb66127a8c527ffb1e0ba32bfe9f72566cfde4f2f9b5fc0c3e7ca7'
    assert torch.cuda.is_available()
    opts=dict(data=str(data/'data.yaml'),device=0,epochs=50,imgsz=640,batch=4,workers=0,seed=20261105,deterministic=True,freeze=0,optimizer='AdamW',lr0=.0001,lrf=.1,warmup_bias_lr=.0001,mosaic=0.,mixup=0.,cutmix=0.,scale=.3,translate=.05,hsv_h=.01,hsv_s=.3,hsv_v=.2,close_mosaic=0,project=str(out),name='train',plots=True)
    spec=dict(dataset_version=sha(data/'audit.json'),initializer_sha256=sha(weight),parameters=opts,torch=torch.__version__,ultralytics=ultralytics.__version__,hypothesis='Nine independent rooted TRAIN additions:12targetinstances, including overlapping/clipped windowsill, cactus and occluded pots. Same initializer/settings as v15; complete13 runtime gate unchanged.',unity_mapping={'PottedPlant':'ParkPlant','CutArrangement':None},evaluation_protocol=dict(confidence=.35,imgsz=640,rect=True,nms_iou=.7,max_det=8,allowed_classes=['PottedPlant']),gates=dict(precision_min=.9,recall_min=.8),mandatory_runtime_val_images=13,native_two_class_fitness_images=12,limitations=['One unresolved auxiliary VAL image excluded only from native two-class fitness; must remain in complete13-image target acceptance','One unresolved border-flower TRAIN image held before inference; no fold reassignment','Tiny public-photo pilot; fresh independent TEST/PICO positive validation required'],service_routed=False,pico_validated=False,test_policy='No observed TEST selection/tuning')
    out.mkdir();(out/'run.json').write_text(json.dumps(spec,indent=2));YOLO(weight).train(**opts)
if __name__=='__main__':main()
