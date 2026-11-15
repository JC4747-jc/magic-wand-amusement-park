"""Bounded real CUDA training smoke. Not a dataset/accuracy/deployment acceptance."""
import argparse
from collections import deque
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import time

ROOT=Path(__file__).resolve().parents[1]
OWNED=Path('/opt/magicmr-yolo')
os.environ.update(OMP_NUM_THREADS='1',MKL_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',NUMEXPR_NUM_THREADS='1',
                  YOLO_AUTOINSTALL='False',YOLO_OFFLINE='True',WANDB_MODE='disabled',CUDA_VISIBLE_DEVICES='0',
                  YOLO_CONFIG_DIR=str(OWNED/'config'),MPLCONFIGDIR=str(OWNED/'mpl'),ULTRALYTICS_API_KEY='',
                  TORCH_HOME=str(OWNED/'torch-cache'),CUDA_CACHE_PATH=str(OWNED/'cuda-cache'),
                  CUDA_CACHE_MAXSIZE=str(128*1024**2),TRITON_CACHE_DIR=str(OWNED/'triton-cache'))
sys.dont_write_bytecode=True

def digest(path):
    value=hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda:stream.read(1024*1024),b''):value.update(chunk)
    return value.hexdigest()

def host_memory():
    command=['/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe','-NoLogo','-NoProfile','-NonInteractive','-WindowStyle','Hidden','-Command','[Console]::Write((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory)']
    value=subprocess.run(command,check=True,capture_output=True,timeout=10).stdout.decode('utf-8',errors='replace').replace('\x00','').strip()
    if not value.isdecimal():raise RuntimeError('Cannot establish host free RAM; refusing unmonitored training')
    return int(value)*1024

def directory_bytes(folder):
    total=0;count=0
    for directory,_,files in os.walk(folder):
        for name in files:
            count+=1
            if count>1000:raise RuntimeError('Smoke output exceeded 1,000-file cap')
            total+=(Path(directory)/name).stat().st_size
    return total

class Guard:
    def __init__(self,folder,torch,psutil):
        self.folder=folder;self.torch=torch;self.process=psutil.Process();self.started=time.monotonic();self.last=0;self.samples=deque(maxlen=256)
    def check(self,*_,force=False):
        now=time.monotonic()
        if not force and now-self.last<5:return
        self.last=now;free,total=self.torch.cuda.mem_get_info();ram=host_memory();size=directory_bytes(self.folder);rss=self.process.memory_info().rss
        sample={'elapsed_s':round(now-self.started,3),'host_free_bytes':ram,'process_rss_bytes':rss,'gpu_free_bytes':free,'torch_allocated_bytes':self.torch.cuda.memory_allocated(),'torch_reserved_bytes':self.torch.cuda.memory_reserved(),'run_bytes':size}
        self.samples.append(sample)
        for drive,minimum in (('/mnt/c',20*1024**3),('/mnt/d',25*1024**3),(str(OWNED),8*1024**3)):
            if shutil.disk_usage(drive).free<minimum:raise RuntimeError('Disk floor crossed: '+drive)
        if ram<3*1024**3 or rss>3*1024**3:raise RuntimeError('Host RAM floor or process RSS ceiling crossed')
        if free<1024**3:raise RuntimeError('Less than 1 GiB global GPU memory remains')
        if size>128*1024**2:raise RuntimeError('Smoke output exceeds 128 MiB')
        if now-self.started>900:raise RuntimeError('Smoke wall-clock budget exceeded')

def prepare(folder):
    from ultralytics.utils import YAML
    source=ROOT/'Find lighter/detect_merged_v3_quad';config=YAML.load(source/'data.yaml')
    assert config['names']==['lighter'],'Unexpected source classes'
    records=[];seen=set();copied=0
    for split,limit in (('train',8),('valid',4)):
        candidates=sorted((source/split/'images').glob('*.jpg'));assert len(candidates)>=limit
        chosen=[candidates[i*len(candidates)//limit] for i in range(limit)]
        for image in chosen:
            label=source/split/'labels'/(image.stem+'.txt');rows=label.read_text().splitlines()
            assert rows and label.stat().st_size<8192,'Missing/oversized smoke labels'
            for row in rows:
                values=list(map(float,row.split()));assert len(values)==5 and values[0]==0
                assert all(math.isfinite(v) and 0<=v<=1 for v in values[1:]) and values[3]>0 and values[4]>0
            sha=digest(image);assert sha not in seen,'Duplicate selected smoke image';seen.add(sha)
            copied+=image.stat().st_size+label.stat().st_size
            if copied>16*1024**2:raise RuntimeError('Smoke data copy exceeds 16 MiB')
            for item,kind in ((image,'images'),(label,'labels')):
                destination=folder/split/kind/item.name;destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(item,destination)
            records.append({'split':split,'image':image.relative_to(ROOT).as_posix(),'image_sha256':sha,'label_sha256':digest(label)})
    data={'path':str(folder),'train':'train/images','val':'valid/images','names':['lighter'],'nc':1}
    YAML.save(folder/'data.yaml',data)
    manifest={'purpose':'GPU_SMOKE_ONLY_NOT_ACCURACY_ACCEPTANCE','source':str(source),'copied_bytes':copied,'records':records,'test_split_used':False}
    (folder/'manifest.json').write_text(json.dumps(manifest,indent=2))
    return folder/'data.yaml',manifest

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--run-id',required=True);args=parser.parse_args()
    if not re.fullmatch(r'[a-z0-9][a-z0-9-]{3,60}',args.run_id):raise ValueError('Invalid owned run identifier')
    if sys.platform!='linux' or sys.prefix!=str(OWNED/'venv'):raise RuntimeError('Use the owned Linux training environment')
    run=OWNED/'runs'/args.run_id
    if run.exists():raise RuntimeError('Refusing to overwrite an existing run')
    run.mkdir(parents=True);os.chdir(run)
    report={'purpose':'GPU_TRAINING_SMOKE_NOT_ACCURACY_ACCEPTANCE','status':'RUNNING','accepted_for_deployment':False,'run':str(run),
            'runner_sha256':digest(Path(__file__).resolve()),'limits':{'run_bytes':128*1024**2,'cuda_cache_bytes':128*1024**2,'allocator_fraction':.55,'minimum_host_free_bytes':3*1024**3,'maximum_rss_bytes':3*1024**3,'seconds':900}}
    workspace=ROOT/'.codex-tmp/yolo-training/artifacts'/args.run_id;workspace.mkdir(parents=True,exist_ok=False)
    shutil.copy2(Path(__file__).resolve(),workspace/'runner.py')
    helper=Path(__file__).with_name('yolo_training_logs.py');shutil.copy2(helper,workspace/helper.name)
    report['log_helper_sha256']=digest(helper)
    report['log_receipt']=str(ROOT/'.codex-tmp/yolo-training/run-logs'/(args.run_id+'.run.json'))
    guard=None;torch=None
    try:
        import psutil
        import torch
        import ultralytics
        from ultralytics import YOLO,settings
        from ultralytics.models.yolo.detect import DetectionTrainer
        from ultralytics.utils import events
        settings.update({key:False for key in ('sync','clearml','comet','dvc','mlflow','raytune','tensorboard','wandb') if key in settings})
        events.events.enabled=False
        class LocalTrainer(DetectionTrainer):
            def run_callbacks(self,event):
                for callback in self.callbacks.get(event,[]):
                    module=callback.__module__
                    if module.startswith('ultralytics.utils.events') or module.startswith('ultralytics.utils.callbacks.') and module!='ultralytics.utils.callbacks.base':continue
                    callback(self)
        assert torch.__version__=='2.4.0+cu124' and ultralytics.__version__=='8.4.212','Training dependency versions changed'
        report['versions']={'torch':torch.__version__,'ultralytics':ultralytics.__version__,'python':sys.version.split()[0]}
        torch.set_num_threads(1);torch.set_num_interop_threads(1)
        assert torch.cuda.is_available(),'CUDA unavailable'
        torch.cuda.set_per_process_memory_fraction(.55,0);torch.cuda.reset_peak_memory_stats()
        guard=Guard(run,torch,psutil);guard.check(force=True)
        if torch.cuda.mem_get_info()[0]<4*1024**3:raise RuntimeError('Less than 4 GiB GPU memory at start')
        signal.signal(signal.SIGALRM,lambda *_:(_ for _ in ()).throw(TimeoutError('900-second training smoke budget')));signal.alarm(900)
        data,manifest=prepare(run/'data')
        weight=ROOT/'yolo26n.pt';service=ROOT/'object-detection-demo/scene_profiles.json';original_weight=digest(weight);original_service=digest(service)
        model=YOLO(str(weight));stats={'batches':0,'optimizer_steps':0,'parameter_changed':False,'epochs':[]};initial={};report['stats']=stats
        def started(trainer):
            name,parameter=next((n,p) for n,p in trainer.model.named_parameters() if p.requires_grad and p.ndim==4)
            initial.update(name=name,parameter=parameter,before=parameter.detach().cpu().clone());guard.check(force=True)
        def batch(trainer):
            stats['batches']+=1
            if trainer.loss is not None and not bool(torch.isfinite(trainer.loss).all()):raise RuntimeError('Nonfinite training loss')
            parameter=initial['parameter'];delta=float((parameter.detach().cpu()-initial['before']).abs().max())
            stats['parameter_changed']|=delta>0;stats['maximum_parameter_delta']=max(stats.get('maximum_parameter_delta',0),delta)
            step=trainer.optimizer.state.get(parameter,{}).get('step',0);stats['optimizer_steps']=max(stats['optimizer_steps'],int(step))
            guard.check()
        def epoch(trainer):
            values=trainer.tloss.items() if isinstance(trainer.tloss,dict) else enumerate(trainer.tloss)
            losses={str(k):float(v.detach().cpu() if torch.is_tensor(v) else v) for k,v in values}
            if not all(math.isfinite(v) for v in losses.values()):raise RuntimeError('Nonfinite epoch loss')
            stats['epochs'].append({'epoch':trainer.epoch+1,'losses':losses});guard.check(force=True)
        model.add_callback('on_train_start',started);model.add_callback('on_train_batch_end',batch);model.add_callback('on_train_epoch_end',epoch);model.add_callback('on_model_save',lambda *_:guard.check(force=True));model.add_callback('on_val_batch_end',guard.check)
        metrics=model.train(trainer=LocalTrainer,data=str(data),epochs=2,imgsz=416,batch=1,workers=0,device=0,cache=False,
            project=str(run),name='train',exist_ok=False,save=True,save_period=-1,plots=False,amp=True,optimizer='AdamW',nbs=1,
            lr0=.001,seed=42,deterministic=True,mosaic=0,mixup=0,copy_paste=0,close_mosaic=0,compile=False,verbose=False)
        guard.check(force=True)
        assert stats['batches']==16 and stats['optimizer_steps']>0 and stats['parameter_changed'],'No verified training parameter update'
        assert len(stats['epochs'])==2 and digest(weight)==original_weight and digest(service)==original_service
        artifacts={}
        for name in ('best.pt','last.pt'):
            source=run/'train/weights'/name;assert source.is_file();shutil.copy2(source,workspace/name);artifacts[name]={'bytes':source.stat().st_size,'sha256':digest(source)}
        reloaded=YOLO(str(workspace/'best.pt'));assert list(reloaded.names.values())==['lighter'];predictions=reloaded.predict(str(next((run/'data/valid/images').glob('*.jpg'))),device=0,imgsz=416,verbose=False,save=False)
        assert len(predictions)==1,'Saved checkpoint did not reload/infer'
        report.update(status='GPU_SMOKE_PASS',stats=stats,artifacts=artifacts,model_sha256=original_weight,service_config_unchanged=True,
            training=dict(epochs=2,imgsz=416,batch=1,workers=0,cache=False,save_period=-1,amp=bool(model.trainer.amp),nbs=1),
            provisional_val_metrics=metrics.results_dict if metrics else {},dataset_manifest=manifest,reload_and_inference_pass=True)
        for name in ('results.csv','args.yaml'):
            source=run/'train'/name
            if source.is_file():shutil.copy2(source,workspace/name)
        print('YOLO_GPU_TRAINING_SMOKE_PASS: real optimizer steps, finite loss, saved/reloaded weights; not accuracy acceptance or deployment')
    except BaseException as error:
        report.update(status='FAILED',error=type(error).__name__+': '+str(error));raise
    finally:
        signal.alarm(0)
        if torch is not None and torch.cuda.is_available():report.update(torch_peak_allocated_bytes=torch.cuda.max_memory_allocated(),torch_peak_reserved_bytes=torch.cuda.max_memory_reserved())
        if guard is not None:report.update(resource_samples=list(guard.samples),run_bytes=directory_bytes(run),resource_scope='Callback/5-second host+GPU snapshots plus PyTorch allocator peak; not a continuous complete device peak')
        (workspace/'report.json').write_text(json.dumps(report,indent=2,default=float))
        (run/'report.json').write_text(json.dumps(report,indent=2,default=float))

if __name__=='__main__':
    from yolo_training_logs import run_with_bounded_logs
    run_with_bounded_logs(main)
