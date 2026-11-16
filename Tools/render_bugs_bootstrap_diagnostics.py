"""Render the frozen label QA and recorded training/validation diagnostics; never reruns inference."""
import argparse,csv,json,os
from pathlib import Path
os.environ.setdefault('OMP_NUM_THREADS','1')
os.environ.setdefault('OPENBLAS_NUM_THREADS','1')
ROOT=Path(__file__).resolve().parents[1]
def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--run-id',required=True);args=parser.parse_args()
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from matplotlib.patches import Rectangle
    from PIL import Image
    import render_yolo_annotation_review as qa
    out=ROOT/'.codex-tmp/hci-bugs-training-20261116';out.mkdir(exist_ok=True)
    artifact=ROOT/'.codex-tmp/yolo-training/artifacts'/args.run_id
    assert artifact.resolve().is_relative_to((ROOT/'.codex-tmp/yolo-training/artifacts').resolve())
    qa.OUT=out/'qa';qa.OUT.mkdir(exist_ok=True)
    review=json.loads((ROOT/'TrainingData/bugs-bootstrap-review-v1.json').read_text(encoding='utf-8-sig'))
    qa.pages([dict(x,local_path=x['source_path']) for x in review['records'] if x['key'].startswith('oi-') and x['boxes']],'spider-reviewed')
    rows=[{k.strip():float(v) for k,v in x.items()} for x in csv.DictReader((artifact/'results.csv').open())]
    report=json.loads((artifact/'report.json').read_text());samples=report['resource_samples'];epoch=[x['epoch'] for x in rows]
    fig,axes=plt.subplots(1,3,figsize=(13,3.6),layout='constrained')
    axes[0].plot(epoch,[x['train/cls_loss'] for x in rows],label='train cls');axes[0].plot(epoch,[x['val/cls_loss'] for x in rows],label='val cls');axes[0].set_title('Classification loss');axes[0].legend()
    values=[x['metrics/mAP50(B)'] for x in rows]
    axes[1].plot(epoch,values,label='validation per-epoch mAP50');axes[1].axhline(0,color='red',label='final holdout mAP50 = 0');axes[1].set_ylim(-.005,max(values)*1.15);axes[1].set_title('Candidate fails holdout');axes[1].legend(fontsize=8)
    axes[2].plot([x['elapsed_s'] for x in samples],[x['host_free_bytes']/1024**3 for x in samples]);axes[2].axhline(3,color='red',linestyle='--',label='3 GiB floor');axes[2].set_xlabel('seconds');axes[2].set_ylabel('GiB');axes[2].set_title('Sampled host available RAM');axes[2].legend()
    for ax in axes[:2]:ax.set_xlabel('epoch')
    fig.savefig(out/'training-diagnostics.png',dpi=130);plt.close(fig)
    reload=json.loads((artifact/'windows-reload.json').read_text())
    selected=[next(x for x in reload['validation_only'] if x['key']==key) for key in ['oi-3b4d1022c2be3076','inat-413336029','oi-2e8edb16c6a09beb']]
    fig,axes=plt.subplots(1,3,figsize=(15,6),layout='constrained')
    for ax,x in zip(axes,selected):
        with Image.open(ROOT/x['source_path']) as im:w,h=im.size;ax.imshow(im.convert('RGB'))
        for box in x['truth']:
            x0,y0,x1,y1=box['xyxy_pixels'];ax.add_patch(Rectangle((x0,y0),x1-x0,y1-y0,fill=False,edgecolor='lime',linewidth=2))
        predictions=x['predictions'][:3]
        for i,box in enumerate(predictions,1):
            x0,y0,x1,y1=[v*z for v,z in zip(box['xyxy_normalized'],[w,h,w,h])];ax.add_patch(Rectangle((x0,y0),x1-x0,y1-y0,fill=False,edgecolor='red',linewidth=1))
            ax.text(x0,y0,str(i),fontsize=8,color='white',bbox={'facecolor':'red','alpha':.8,'pad':1})
        scores='; '.join(str(i)+': '+p['class']+' '+format(p['confidence'],'.3f') for i,p in enumerate(predictions,1))
        ax.set_title(x['key']+'\nValidation only; green truth / red low-score proposals\n'+scores,fontsize=8);ax.set_axis_off()
    fig.savefig(out/'validation-failures.png',dpi=100);plt.close(fig)
    assert sum(p.stat().st_size for p in out.rglob('*') if p.is_file())<32*1024**2
    print(json.dumps({'trainingGraphs':2,'QA_sheets':3,'inferenceCalls':0,'output':str(out)}))
if __name__=='__main__':main()
