"""Prepare grouped freeze data, fine-tune, and compare on identical holdouts."""
import argparse
import csv
import io
import json
import random
import re
import shutil
import zipfile
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA_ROOT = ROOT/'Find lighter'
AUDIT = DATA_ROOT/'audit_lighter_v3'
OUT = DATA_ROOT/'detect_merged_v5_freeze'
RUNS = DATA_ROOT/'runs/detect'
RUN = RUNS/'lighter-yolo26n-freeze-v5-50e'
BASE = RUNS/'lighter-yolo26n-quad-v4-50e/weights/best.pt'
EVAL_SUBSETS = ('old','freeze')
BASE_LABEL, NEW_LABEL = 'v4', 'v5'


def review():
    from PIL import Image, ImageDraw
    records = json.loads((AUDIT/'audit.json').read_text())['records']
    with zipfile.ZipFile(DATA_ROOT/'detect lighters.v3i.yolo26.zip') as z:
        for page in range((len(records)+29)//30):
            sheet = Image.new('RGB',(1500,1080),'white')
            d = ImageDraw.Draw(sheet)
            for i,r in enumerate(records[page*30:(page+1)*30]):
                im = Image.open(io.BytesIO(z.read(r['name']))).convert('RGB').resize((250,190))
                draw = ImageDraw.Draw(im)
                for _,x,y,w,h in r['boxes']:
                    draw.rectangle(((x-w/2)*250,(y-h/2)*190,(x+w/2)*250,(y+h/2)*190),outline='red',width=2)
                x0,y0=(i%6)*250,(i//6)*216
                sheet.paste(im,(x0,y0))
                d.text((x0+2,y0+192),f'{page*30+i}: '+Path(r['name']).name[:24],fill='black')
            sheet.save(AUDIT/f'full_review_{page+1:02d}.jpg')


def prepare():
    if OUT.exists():
        raise SystemExit(f'Refusing overwrite {OUT}')
    report = json.loads((AUDIT/'audit.json').read_text())
    records = report['records']
    conflicts = {n for group in report['exact_duplicate_groups'] for n in group}
    # Full contact-sheet review: one export of this two-lighter scene omits the white lighter.
    conflicts.update(r['name'] for r in records if 'IMG_20241103_184708_' in r['name'])
    candidates = [r for r in records if r['name'] not in conflicts]
    parent = {r['name']:r['name'] for r in candidates}
    def find(a):
        while parent[a]!=a:
            parent[a]=parent[parent[a]]
            a=parent[a]
        return a
    def union(a,b):
        if a in parent and b in parent:
            parent[find(a)]=find(b)
    # Keep capture-minute sequences, same filenames and perceptual neighbors together.
    minute_groups=defaultdict(list)
    for r in candidates:
        match=re.search(r'IMG_(\d{8})_(\d{4})\d{2}',r['name'])
        key='_'.join(match.groups()) if match else Path(r['name']).name.split('.rf.')[0]
        minute_groups[key].append(r['name'])
    for names in minute_groups.values():
        for n in names[1:]: union(names[0],n)
    for pair in report['near_pairs']: union(pair['a'],pair['b'])
    groups=defaultdict(list)
    for r in candidates: groups[find(r['name'])].append(r)
    groups=list(groups.values())
    random.Random(42).shuffle(groups)
    groups.sort(key=len,reverse=True)
    targets={'train':len(candidates)*0.7,'valid':len(candidates)*0.15,'test':len(candidates)*0.15}
    assigned=defaultdict(list)
    for group in groups:
        split=max(targets,key=lambda s:targets[s]-len(assigned[s]))
        assigned[split].extend(group)
    assert all(assigned[s] for s in targets)
    manifest=[]
    for split in targets:
        for kind in ('images','labels'):
            (OUT/split/kind).mkdir(parents=True,exist_ok=True)
        old=DATA_ROOT/'detect_merged_v3_quad'/split
        for image in sorted((old/'images').iterdir()):
            if image.suffix.lower() not in ('.jpg','.jpeg','.png','.webp'): continue
            label=old/'labels'/(image.stem+'.txt')
            shutil.copy2(image,OUT/split/'images'/image.name)
            shutil.copy2(label,OUT/split/'labels'/label.name)
            manifest.append(dict(source='old',split=split,image=image.name))
    with zipfile.ZipFile(DATA_ROOT/'detect lighters.v3i.yolo26.zip') as z:
        for split,rows in assigned.items():
            for r in rows:
                name='freeze_'+Path(r['name']).name
                (OUT/split/'images'/name).write_bytes(z.read(r['name']))
                (OUT/split/'labels'/(Path(name).stem+'.txt')).write_text(''.join('0 '+' '.join(f'{v:.9f}' for v in b[1:])+'\n' for b in r['boxes']),encoding='utf-8')
                manifest.append(dict(source='freeze',split=split,image=name,original=r['name'],group=find(r['name'])))
        for name in ('README.dataset.txt','README.roboflow.txt'):
            (OUT/('freeze_'+name)).write_bytes(z.read(name))
    config=f'path: {OUT.as_posix()}\ntrain: train/images\nval: valid/images\ntest: test/images\nnc: 1\nnames: [lighter]\n'
    (OUT/'data.yaml').write_text(config,encoding='utf-8')
    for source in ('old','freeze'):
        paths=[f"test/images/{m['image']}" for m in manifest if m['source']==source and m['split']=='test']
        (OUT/f'{source}_test.txt').write_text('\n'.join(str(OUT/p) for p in paths)+'\n',encoding='utf-8')
        (OUT/f'{source}_eval.yaml').write_text(config.replace('test: test/images',f'test: {source}_test.txt'),encoding='utf-8')
    assignments={m['original']:m['split'] for m in manifest if m['source']=='freeze'}
    assert all(assignments[p['a']]==assignments[p['b']] for p in report['near_pairs'] if p['a'] in assignments and p['b'] in assignments)
    summary={'counts':dict(Counter(m['split'] for m in manifest)), 'new_counts':{s:len(v) for s,v in assigned.items()},'excluded_conflicting_records':sorted(conflicts),'group_sizes':sorted(map(len,groups),reverse=True),'manifest':manifest}
    (OUT/'preparation.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in summary.items() if k not in ('manifest','excluded_conflicting_records')}))


def train():
    from ultralytics import YOLO
    YOLO(str(BASE)).train(data=str(OUT/'data.yaml'),epochs=50,patience=0,imgsz=640,batch=16,device=0,workers=4,project=str(RUNS),name=RUN.name,exist_ok=False,optimizer='AdamW',lr0=0.0005,cos_lr=True,seed=42,freeze=None,plots=True)
    evaluate()


def evaluate():
    import numpy as np
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from ultralytics import YOLO
    summary={}
    for subset in EVAL_SUBSETS:
        fig,axes=plt.subplots(2,2,figsize=(12,9))
        for label,weights,color in [(BASE_LABEL,BASE,'#2563eb'),(NEW_LABEL,RUN/'weights/best.pt','#f97316')]:
            model=YOLO(str(weights))
            metrics=model.val(data=str(OUT/f'{subset}_eval.yaml'),split='test',device=0,batch=16,workers=0,plots=True,conf=0.001,project=str(RUN/'evaluation'),name=f'{subset}_{label}',verbose=False)
            summary[f'{subset}_{label}']={k:float(v) for k,v in metrics.results_dict.items()}
            for ax,curve in zip(axes.flat,metrics.curves_results):
                x,y,xname,yname=curve
                ax.plot(x,np.asarray(y).mean(axis=0),color=color,label=label)
                ax.set(xlabel=xname,ylabel=yname,xlim=(0,1),ylim=(0,1))
                ax.grid(alpha=.2); ax.legend()
            fixed=model.val(data=str(OUT/f'{subset}_eval.yaml'),split='test',device=0,batch=16,workers=0,plots=True,conf=0.399,project=str(RUN/'evaluation'),name=f'{subset}_{label}_conf0399',verbose=False)
            # Confusion matrix gives actual TP/FP/FN at the configured threshold.
            matrix=fixed.confusion_matrix.matrix
            tp=float(matrix[0,0]); fp=float(matrix[0,-1]); fn=float(matrix[-1,0])
            summary[f'{subset}_{label}_conf0399']={'tp':tp,'fp':fp,'fn':fn,'precision':tp/(tp+fp) if tp+fp else 0,'recall':tp/(tp+fn) if tp+fn else 0,'matching_iou':0.45,'max_det':300}
        fig.suptitle(f'{subset} test set: {BASE_LABEL} vs {NEW_LABEL} (same images)')
        fig.tight_layout(); fig.savefig(RUN/f'{subset}_comparison.png',dpi=160); plt.close(fig)
    (RUN/'comparison_metrics.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
    print('COMPARISON_RESULTS',json.dumps(summary))


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('mode',choices=['review','prepare','train','evaluate'])
    args=parser.parse_args()
    globals()[args.mode]()
