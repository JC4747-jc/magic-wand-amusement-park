"""Single-class plant pilot; preserve parent partitions and all reviewed boxes."""
import hashlib,json,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    import torch,ultralytics
    from ultralytics import YOLO
    parent=ROOT/'Datasets/plant-container-expanded-v9'
    data=ROOT/'Datasets/plant-native-v10'
    out=ROOT/'Reports/plant-native-v10-training'
    assert not data.exists() and not out.exists(),'Inspect existing artifacts; never restart blindly'
    assert sha(parent/'audit.json')=='83784472faeba9fe00a061ccd92d21163965965d663a8e0b2cc5fb6d5131a956'
    audit=json.loads((parent/'audit.json').read_text())
    folds={}
    for row in audit['samples']:
        assert sha(parent/row['image'])==row['sha256']
        assert sha(parent/row['label'])==row['label_sha256']
        g=row['group'];assert g not in folds or folds[g]==row['split'];folds[g]=row['split']
        for line in (parent/row['label']).read_text().splitlines():assert line.split()[0]=='58'
    initializer=ROOT/'object-detection-demo/weights/candidates/common-yolov8m-official-v1/best.pt'
    assert sha(initializer)=='5d4a90cdc7a21786cc59cd19778e9eafff836df9e2da32524737c7ee6efe4fe5'
    assert torch.cuda.is_available()
    for row in audit['samples']:
        ip=data/row['image'];lp=data/row['label'];ip.parent.mkdir(parents=True,exist_ok=True);lp.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(parent/row['image'],ip)
        lines=['0 '+line.split(' ',1)[1] for line in (parent/row['label']).read_text().splitlines()]
        lp.write_text('\n'.join(lines)+('\n' if lines else ''))
        row['parent_label_sha256']=row['label_sha256'];row['label_sha256']=sha(lp)
    audit.update(parent_audit_sha256=sha(parent/'audit.json'),names=['PottedPlant'],class_remap={'58':0},deployment_accepted=False,scope='Same parent reviewed samples and folds; observed test never used for selection')
    (data/'audit.json').write_text(json.dumps(audit,indent=2))
    (data/'data.yaml').write_text(json.dumps(dict(path=str(data),train='images/train',val='images/val',test='images/test',names=['PottedPlant'])))
    options=dict(data=str(data/'data.yaml'),device=0,epochs=40,imgsz=640,batch=4,workers=0,seed=20261105,deterministic=True,freeze=0,optimizer='AdamW',lr0=.0001,lrf=.1,warmup_bias_lr=.0001,mosaic=0.,mixup=0.,cutmix=0.,scale=.3,translate=.05,hsv_h=.01,hsv_s=.3,hsv_v=.2,close_mosaic=0,project=str(out),name='train',plots=True)
    out.mkdir()
    spec=dict(dataset_sha256=sha(data/'audit.json'),initializer_sha256=sha(initializer),parameters=options,torch=torch.__version__,ultralytics=ultralytics.__version__,hypothesis='Single-class native head and full-network adaptation may improve container/foliage discrimination; not an isolated one-variable ablation',protocol=dict(confidence=.65,imgsz=640,rect=True,nms_iou=.7,max_det=8),gates=dict(precision_min=.9,recall_min=.8),test_policy='Validation selection only; observed TEST is diagnostic, fresh independent TEST required after passing VAL',service_routed=False,pico_validated=False)
    (out/'run.json').write_text(json.dumps(spec,indent=2))
    YOLO(initializer).train(**options)
if __name__=='__main__':main()
