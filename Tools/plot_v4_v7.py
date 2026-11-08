import json
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

root=Path(__file__).resolve().parents[1]/'Find lighter/runs/detect'
out=root/'v4_v7_performance'
out.mkdir(exist_ok=True)
data={}
for name in ['lighter-yolo26n-freeze-v5-50e','lighter-yolo26n-assgn2-v6-50e','lighter-yolo26n-korek-v7-50e']:
    data.update(json.loads((root/name/'comparison_metrics.json').read_text()))
versions=['v4','v5','v6','v7']
subsets=['old','freeze','assgn2','korek']
colors=['#64748b','#a855f7','#2563eb','#f97316']
fig,axes=plt.subplots(2,2,figsize=(14,10))
for ax,subset in zip(axes.flat,subsets):
    for i,version in enumerate(versions):
        m=data.get(f'{subset}_{version}')
        if m is None:
            ax.text(i,4,'Not evaluated',rotation=90,ha='center',va='bottom',color='#777')
            continue
        for offset,key,alpha in [(-.18,'metrics/mAP50(B)',.45),(.18,'metrics/mAP50-95(B)',1)]:
            v=m[key]*100
            ax.bar(i+offset,v,.35,color=colors[i],alpha=alpha)
            ax.text(i+offset,v+1,f'{v:.1f}',ha='center',fontsize=9)
    ax.set(title=f'{subset} test set',ylabel='AP (%)',ylim=(0,110),xticks=range(4),xticklabels=versions)
    ax.grid(axis='y',alpha=.2)
fig.suptitle('v4–v7: same-test-set comparisons\nLight bars: mAP50 | Solid bars: mAP50–95 | Higher is better',fontsize=15)
fig.text(.5,.015,'Historical evaluations, not live PICO results. Missing combinations are not zero.',ha='center')
fig.tight_layout(rect=(0,.035,1,.93)); fig.savefig(out/'map_comparison.png',dpi=160); plt.close(fig)
fig,axes=plt.subplots(1,2,figsize=(14,5))
for ax,subset in zip(axes,['old','freeze']):
    for offset,key in [(-.18,'fp'),(.18,'fn')]:
        vals=[data[f'{subset}_{v}_conf0399'][key] for v in versions]
        bars=ax.bar(np.arange(4)+offset,vals,.35,label='False positives' if key=='fp' else 'Missed objects',color='#eab308' if key=='fp' else '#ef4444')
        ax.bar_label(bars,fmt='%.0f',padding=3)
    ax.set(title=f'{subset}: errors at confidence=0.399',xticks=range(4),xticklabels=versions,ylabel='Object count (lower is better)',ylim=(0,42))
    ax.legend(); ax.grid(axis='y',alpha=.2)
fig.suptitle('Matching IoU=0.45, max_det=300; deployment max_det=1 is different')
fig.tight_layout(); fig.savefig(out/'errors_comparison.png',dpi=160); plt.close(fig)
(out/'source_metrics.json').write_text(json.dumps(data,indent=2))
print(out)
