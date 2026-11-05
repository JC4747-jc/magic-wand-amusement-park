"""Evaluate actual pretrained/custom detections against canonical class IDs.
Unsupported classes receive zero recall, rather than remapping unrelated COCO IDs.
"""
import argparse, hashlib, json, time, math
from pathlib import Path
import cv2, numpy as np, torch, ultralytics
from ultralytics import YOLO
from ultralytics.utils.metrics import ap_per_class
from dataset_paths import manifest_relative_path

ALIASES={'tree':'Tree','spider':'Spider','cockroach':'Cockroach','statue':'Statue','sculpture':'Statue','cat':'Cat','dog':'Dog','potted plant':'PottedPlant','pottedplant':'PottedPlant'}
def evaluate(model_path,root,out,split='val',imgsz=320,operating_conf=.5,square=False,allowed_classes=None,max_det=300,nms_iou=.7,confidence_grid=None):
    root=Path(root); out=Path(out); out.mkdir(parents=True,exist_ok=True)
    import yaml
    audit=json.loads((root/'audit.json').read_text(encoding='utf-8')); schema=yaml.safe_load((root/'data.yaml').read_text(encoding='utf-8'))
    # Native COCO schemas retain class 58 "potted plant" during fine-tuning.
    # Canonicalize schema aliases as well as predictions without changing IDs.
    schema_names=schema['names']
    if isinstance(schema_names,dict):
        ids=sorted(int(k) for k in schema_names)
        if ids!=list(range(len(ids))): raise ValueError('Class IDs must be contiguous from zero')
        schema_names=[schema_names[k] if k in schema_names else schema_names[str(k)] for k in ids]
    names=[ALIASES.get(name.lower(),name) for name in schema_names]
    model=YOLO(model_path); target=[]; predictions=[]; confidences=[]; correct=[]; timings=[]; failures=[]
    active=set(allowed_classes) if allowed_classes else set(names)
    if not active.issubset(set(names)):raise ValueError('Allowed class absent from data schema')
    native_ids=[i for i,n in model.names.items() if ALIASES.get(n.lower(),n) in active]
    if allowed_classes and not native_ids:raise ValueError('Allowed classes absent from model')
    operating=[dict(name=name,true_positive=0,false_positive=0,false_negative=0) for name in names]
    thresholds=sorted(set(confidence_grid or []))
    if any(not math.isfinite(t) or not 0<=t<=1 for t in thresholds):
        raise ValueError('Confidence grid must contain finite values between zero and one')
    curve={t:[dict(name=name,true_positive=0,false_positive=0,false_negative=0)
              for name in names if name in active] for t in thresholds}
    false_positive_images=0
    image_metrics=[]
    excluded=[]
    for row in audit['samples']:
        if row['split']!=split:continue
        if row.get('evaluation_excluded', False):
            excluded.append(dict(image=row['image'],reason=row.get('exclusion_reason','Pending annotation review')))
            continue
        relative_image=manifest_relative_path(row['image'])
        image_path=root.joinpath(*relative_image.parts)
        image=cv2.imread(str(image_path))
        if image is None:raise ValueError('Unreadable image: '+str(image_path))
        h,w=image.shape[:2]
        relative_label=manifest_relative_path(row['label']) if row.get('label') else manifest_relative_path('labels/'+split+'/'+relative_image.with_suffix('.txt').name)
        labels=root.joinpath(*relative_label.parts).read_text(encoding='utf-8').splitlines()
        truth=[]
        for line in labels:
            c,x,y,bw,bh=map(float,line.split())
            if names[int(c)] in active:truth.append((int(c),np.array([(x-bw/2)*w,(y-bh/2)*h,(x+bw/2)*w,(y+bh/2)*h])))
        target.extend(c for c,b in truth)
        start=time.perf_counter(); result=model.predict(image,imgsz=imgsz,rect=not square,device='cpu',conf=.001,iou=nms_iou,classes=native_ids if allowed_classes else None,max_det=max_det,verbose=False)[0]; timings.append((time.perf_counter()-start)*1000)
        det=[]
        for box in result.boxes:
            native=int(box.cls.item())
            native_name=model.names[native]
            name=ALIASES.get(native_name.lower(),native_name)
            if name not in active:continue
            det.append((names.index(name),float(box.conf.item()),box.xyxy[0].cpu().numpy()))
        det.sort(key=lambda d:-d[1]); flags=np.zeros((len(det),10),dtype=bool)
        for j,threshold in enumerate(np.linspace(.5,.95,10)):
            used=set()
            for i,(c,score,b) in enumerate(det):
                choices=[]
                for t,(tc,tb) in enumerate(truth):
                    if t in used or tc!=c:continue
                    intersection=np.maximum(0,np.minimum(b[2:],tb[2:])-np.maximum(b[:2],tb[:2])).prod()
                    union=np.maximum(0,b[2:]-b[:2]).prod()+np.maximum(0,tb[2:]-tb[:2]).prod()-intersection
                    choices.append((intersection/max(union,1e-9),t))
                if choices:
                    overlap,t=max(choices)
                    if overlap>=threshold:flags[i,j]=True; used.add(t)
        predictions.extend(c for c,s,b in det); confidences.extend(s for c,s,b in det); correct.extend(flags)
        accepted=[i for i,(_,score,_) in enumerate(det) if score>=operating_conf]
        tp=sum(bool(flags[i,0]) for i in accepted); fp=len(accepted)-tp; fn=len(truth)-tp
        image_metrics.append(dict(image=row['image'],ground_truth=len(truth),true_positive=int(tp),
            false_positive=int(fp),false_negative=int(fn),inference_ms=timings[-1]))
        false_positive_images+=int(fp>0)
        for c,stats in enumerate(operating):
            class_tp=sum(bool(flags[i,0]) for i in accepted if det[i][0]==c)
            stats['true_positive']+=class_tp
            stats['false_positive']+=sum(det[i][0]==c for i in accepted)-class_tp
            stats['false_negative']+=sum(tc==c for tc,_ in truth)-class_tp
        # Reuse the same inference and confidence-ordered IoU matches. Lower
        # confidence predictions cannot claim truth before higher ones.
        for threshold,classes in curve.items():
            indices=[i for i,(_,score,_) in enumerate(det) if score>=threshold]
            for stats in classes:
                c=names.index(stats['name'])
                class_tp=sum(bool(flags[i,0]) for i in indices if det[i][0]==c)
                stats['true_positive']+=class_tp
                stats['false_positive']+=sum(det[i][0]==c for i in indices)-class_tp
                stats['false_negative']+=sum(tc==c for tc,_ in truth)-class_tp
        if len(failures)<30 and (fn>0 or fp>0):
            filename='error-'+relative_image.stem+'.jpg'; annotated=image.copy()
            for c,b in truth:
                a,bottom=tuple(map(int,b[:2])),tuple(map(int,b[2:]));cv2.rectangle(annotated,a,bottom,(255,100,0),2)
                cv2.putText(annotated,'GT '+names[c],a,cv2.FONT_HERSHEY_SIMPLEX,.45,(255,100,0),1)
            for i in accepted:
                c,score,b=det[i];color=(0,200,0) if flags[i,0] else (0,0,255);a,bottom=tuple(map(int,b[:2])),tuple(map(int,b[2:]))
                cv2.rectangle(annotated,a,bottom,color,2);cv2.putText(annotated,('TP ' if flags[i,0] else 'FP ')+names[c]+f' {score:.2f}',a,cv2.FONT_HERSHEY_SIMPLEX,.45,color,1)
            cv2.imwrite(str(out/filename),annotated)
            failures.append(dict(image=row['image'],example=filename,ground_truth=len(truth),true_positives=int(tp),false_positives=int(fp),false_negatives=int(fn),confidence=operating_conf))
    if not timings:raise ValueError('No images in requested evaluation split')
    per_class=[]
    if target:
        metrics=ap_per_class(np.array(correct,dtype=bool).reshape(-1,10),np.array(confidences),np.array(predictions),np.array(target))
        per_class=[dict(class_id=int(c),name=names[int(c)],precision=float(metrics[2][i]),recall=float(metrics[3][i]),mAP50=float(metrics[5][i,0]),mAP50_95=float(metrics[5][i].mean()),labels=int(sum(np.array(target)==c))) for i,c in enumerate(metrics[6])]
    for stats in operating:
        tp,fp,fn=(stats[k] for k in ('true_positive','false_positive','false_negative'))
        stats.update(precision=tp/(tp+fp) if tp+fp else None,recall=tp/(tp+fn) if tp+fn else None)
    sources=audit.get('sources',[])
    openimages_only=bool(sources) and all(s['id']=='openimages-v7' for s in sources)
    pico_only=bool(sources) and all(s['id']=='pico' for s in sources)
    domain=('pico-negative-only-heldout' if not target else 'pico-source-subset') if pico_only else ('public-openimages-pilot' if openimages_only else 'public-source-pilot')
    limitations=['Small source-grouped subset; not an official benchmark score']
    if not pico_only:limitations.append('No PICO targets evaluated')
    elif not target:limitations.append('No positive PICO targets: cannot establish precision, recall or AP for target recognition')
    if 'PottedPlant' in names:limitations.append('Houseplant labels are broader than potted tabletop plants; domain validation required')
    if 'Statue' in names:limitations.append('Statue class does not establish historical identity, age or full 3D pose')
    if 'JumpingSpider' in names:limitations.append('JumpingSpider covers Salticidae family only; scores do not establish recognition of all spiders or identification of one biological species')
    for classes in curve.values():
        for stats in classes:
            tp,fp,fn=(stats[k] for k in ('true_positive','false_positive','false_negative'))
            stats['precision']=tp/(tp+fp) if tp+fp else None
            stats['recall']=tp/(tp+fn) if tp+fn else None
    report=dict(mode='training-diagnostic' if split=='train' else ('baseline' if split=='val' else 'test'),domain=domain,deployment_accepted=False,
        dataset_sources=sources,
        limitations=limitations,
        model=str(Path(model_path).resolve()),architecture=type(model.model).__name__,sha256=hashlib.sha256(Path(model_path).read_bytes()).hexdigest(),
        dataset_version=hashlib.sha256((root/'audit.json').read_bytes()).hexdigest(),split=split,torch=torch.__version__,ultralytics=ultralytics.__version__,model_names=model.names,
        per_class=per_class,images=len(timings),excluded_samples=excluded,ground_truth_instances=len(target),
        false_positive_images=false_positive_images,per_image=image_metrics,
        ap_scope='Positive target instances' if target else 'NOT_APPLICABLE: negative-only evaluation',
        operating_confidence=operating_conf,operating_iou=.5,operating_per_class=operating,
        operating_confidence_curve=[dict(confidence=t,per_class=stats) for t,stats in curve.items()],
        threshold_selection_scope='Use validation only for threshold selection; test curves are diagnostic, not tuning data',
        mean_inference_ms=float(np.mean(timings)),p95_inference_ms=float(np.percentile(timings,95)),cold_first_image_ms=timings[0],warm_mean_ms=float(np.mean(timings[1:])) if len(timings)>1 else None,
        timing_scope='PC PyTorch prediction only; first sample includes cold initialization; excludes TCP, stereo and Unity animation',imgsz=imgsz,rect=not square,device='cpu',errors=failures,
        allowed_classes=sorted(active),max_det=max_det,nms_iou=nms_iou,
        preprocessing='YOLO predict: minimal stride-aligned rectangular letterbox' if not square else 'YOLO predict: square letterbox',
        native_validation_comparability='Ultralytics val may add 0.5 stride padding and produce different tensor shapes at the same imgsz; use this runtime-shaped report for the service gate')
    hold_path=root/'review_hold.json'
    if hold_path.exists():
        hold=json.loads(hold_path.read_text(encoding='utf-8-sig'))
        if hold.get('dataset_audit_sha256')!=report['dataset_version']:
            raise ValueError('Dataset review hold version mismatch; inspect before evaluating')
        report['annotation_review_hold']=hold
        report['limitations'].append('Semantic labels under review; metrics are diagnostic and cannot establish model acceptance')
    (out/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8'); print(json.dumps(per_class,indent=2)); return report
if __name__=='__main__':
    p=argparse.ArgumentParser(); p.add_argument('--model',required=True); p.add_argument('--data',required=True); p.add_argument('--out',required=True); p.add_argument('--split',default='val'); p.add_argument('--imgsz',type=int,default=320); p.add_argument('--operating-conf',type=float,default=.5); p.add_argument('--square',action='store_true',help='Use square letterbox instead of minimal rectangular padding'); p.add_argument("--allowed-classes",nargs="+"); p.add_argument("--max-det",type=int,default=300); p.add_argument("--nms-iou",type=float,default=.7)
    p.add_argument('--confidence-grid',nargs='+',type=float,help='Evaluate operating thresholds using one prediction pass')
    a=p.parse_args()
    if not 0<a.nms_iou<1:p.error('NMS IoU must be between 0 and 1')
    if a.max_det<1:p.error('max-det must be positive')
    if not 0<=a.operating_conf<=1:p.error('operating confidence must be between 0 and 1')
    evaluate(a.model,a.data,a.out,a.split,a.imgsz,a.operating_conf,a.square,a.allowed_classes,a.max_det,a.nms_iou,a.confidence_grid)
