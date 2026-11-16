"""Reload a rejected bootstrap candidate on Windows CPU; validation diagnostics only, no test-set reuse."""
import os
os.environ.update(OMP_NUM_THREADS='1',MKL_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',YOLO_AUTOINSTALL='false',YOLO_OFFLINE='true')
import argparse,hashlib,json,math,time
from pathlib import Path
import psutil

ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--run-id',required=True);args=parser.parse_args()
    artifact=ROOT/'.codex-tmp/yolo-training/artifacts'/args.run_id
    assert artifact.resolve().is_relative_to((ROOT/'.codex-tmp/yolo-training/artifacts').resolve())
    report=json.loads((artifact/'report.json').read_text());dataset=json.loads((artifact/'frozen-dataset.json').read_text())
    assert report['status']=='CANDIDATE_TRAINED_AND_TESTED_NOT_DEPLOYMENT_ACCEPTED' and not report['accepted_for_deployment']
    weight=artifact/'best.pt';assert sha(weight)==report['artifacts']['best.pt']['sha256']
    configs={p:sha(ROOT/p) for p in ['object-detection-demo/scene_profiles.json','yolo26n.pt']}
    began=time.monotonic();samples=[]
    def guard():
        sample={'seconds':time.monotonic()-began,'freeRAM':psutil.virtual_memory().available,'RSS':psutil.Process().memory_info().rss}
        samples.append(sample)
        assert sample['freeRAM']>=3*1024**3 and sample['RSS']<=3*1024**3,'CPU reload memory guard'
        assert psutil.disk_usage(str(ROOT)).free>=20*1024**3 and sample['seconds']<120
    guard()
    import torch
    from ultralytics import YOLO
    torch.set_num_threads(1);torch.set_num_interop_threads(1)
    model=YOLO(str(weight));assert list(model.names.values())==dataset['names']==['Cockroach','Spider']
    assert all(bool(torch.isfinite(p).all()) for p in model.model.parameters())
    rows=[]
    for item in dataset['records']:
        if item['split']!='valid':continue
        image=ROOT/item['source_path'];assert sha(image)==item['sha256'];guard()
        # Low-conf validation-only boxes expose score/localization failures, never redefine the test threshold.
        prediction=model.predict(str(image),device='cpu',imgsz=416,conf=.001,iou=.7,max_det=20,save=False,verbose=False)[0]
        boxes=prediction.boxes
        assert bool(torch.isfinite(boxes.data).all())
        rows.append({'key':item['key'],'source_path':item['source_path'],'imageSHA256':item['sha256'],'truth':item['boxes'],
            'background_only':not bool(item['boxes']),'predictions':[{'class':model.names[int(c)],'confidence':float(s),'xyxy_normalized':b.tolist()} for b,c,s in zip(boxes.xyxyn.cpu(),boxes.cls.cpu(),boxes.conf.cpu())]})
    guard();assert all(sha(ROOT/p)==v for p,v in configs.items())
    out={'status':'PASS_WINDOWS_RELOAD_AND_FINITE_VALIDATION_INFERENCE_ONLY','accepted_for_deployment':False,'weightSHA256':sha(weight),'windowsTorch':torch.__version__,'names':model.names,
        'model_parameters':sum(p.numel() for p in model.model.parameters()),'validation_only':rows,'test_images_reused':0,'confidence':.001,'max_det':20,'resources':samples,
        'scope':'Serialization/names/finite tensor and validation-only diagnostic checks; failed test metrics unchanged. This is not detector quality or deployment acceptance.','production_config_unchanged':True}
    (artifact/'windows-reload.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
    print(json.dumps({'status':out['status'],'validationImages':len(rows),'detectionsAt001':sum(len(r['predictions']) for r in rows),'maximumValidationConfidence':max((d['confidence'] for r in rows for d in r['predictions']),default=0),'minimumSampledRAMGiB':min(r['freeRAM'] for r in samples)/1024**3,'maximumSampledRSSMiB':max(r['RSS'] for r in samples)/1024**2}))
if __name__=='__main__':main()
