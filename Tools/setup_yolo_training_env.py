"""Create an owned Linux training venv that reads verified CUDA dependencies without modifying F2G."""
import argparse
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import platform
import subprocess
import sys
import venv

ROOT=Path(__file__).resolve().parents[1]
DEST=Path('/opt/magicmr-yolo')
BASE=Path('/opt/f2g/venv/lib/python3.10/site-packages')
FILES=('torch/__init__.py','torchvision/__init__.py','ultralytics/__init__.py','numpy/__init__.py','cv2/__init__.py')

def fingerprint():
    return {name:hashlib.sha256((BASE/name).read_bytes()).hexdigest() for name in FILES}

def check():
    import torch
    import torchvision
    import numpy as np
    import ultralytics
    torch.set_num_threads(1);torch.set_num_interop_threads(1)
    assert sys.prefix==str(DEST/'venv'),sys.prefix
    assert torch.__version__=='2.4.0+cu124' and ultralytics.__version__=='8.4.212'
    assert torch.cuda.is_available(),'CUDA unavailable'
    torch.cuda.set_per_process_memory_fraction(.55,0)
    torch.from_numpy(np.zeros((2,2),dtype=np.float32))
    sample=torch.ones((16,16),device='cuda',requires_grad=True)
    (sample.square().mean()).backward();torch.cuda.synchronize()
    free,total=torch.cuda.mem_get_info()
    data={'python':platform.python_version(),'prefix':sys.prefix,'base_prefix':sys.base_prefix,'torch':torch.__version__,
          'torchvision':torchvision.__version__,'ultralytics':ultralytics.__version__,'numpy':np.__version__,
          'gpu':torch.cuda.get_device_name(),'cuda':torch.version.cuda,'cuda_backward_pass':sample.grad is not None,
          'gpu_free_bytes':free,'gpu_total_bytes':total,'allocator_fraction':.55,'torch_threads':torch.get_num_threads(),
          'shared_dependencies':str(BASE),'writes_to_f2g_packages':False,'bytecode_writes_disabled':sys.dont_write_bytecode,
          'training_started':False,'dependencies':{name:importlib.metadata.version(name) for name in ('torch','torchvision','numpy','ultralytics','opencv-python','polars','nvidia-ml-py')}}
    output=ROOT/'.codex-tmp/yolo-training';output.mkdir(parents=True,exist_ok=True)
    (output/'linux-preflight.json').write_text(json.dumps(data,indent=2),encoding='utf-8')
    print(json.dumps(data));return

def setup():
    if sys.platform!='linux' or sys.version_info[:2]!=(3,10):raise RuntimeError('Run with existing Linux Python 3.10')
    marker=DEST/'magicmr-owned-environment.json'
    if DEST.exists() and not marker.is_file():raise RuntimeError('Refusing to overwrite an unowned environment directory')
    before=fingerprint()
    if not DEST.exists():
        DEST.mkdir();marker.write_text(json.dumps({'owner':'MagicMR YOLO training','base_read_path':str(BASE)},indent=2))
        venv.EnvBuilder(with_pip=False).create(DEST/'venv')
    shared=DEST/'venv/lib/python3.10/site-packages/magicmr_shared_cuda.pth'
    if shared.exists() and shared.read_text().strip()!=str(BASE):raise RuntimeError('Unexpected existing shared dependency path')
    shared.write_text(str(BASE)+'\n')
    environment=os.environ.copy()
    environment.update(PYTHONDONTWRITEBYTECODE='1',OMP_NUM_THREADS='1',MKL_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',
                       YOLO_AUTOINSTALL='False',YOLO_OFFLINE='True',WANDB_MODE='disabled',MPLCONFIGDIR=str(DEST/'mpl'),
                       YOLO_CONFIG_DIR=str(DEST/'config'))
    subprocess.run([str(DEST/'venv/bin/python'),str(Path(__file__).resolve()),'--check'],env=environment,check=True)
    subprocess.run([str(DEST/'venv/bin/python'),'-m','pip','check'],env=environment,check=True)
    after=fingerprint();assert before==after,'Shared F2G package files changed'
    (ROOT/'.codex-tmp/yolo-training/shared-base-integrity.json').write_text(json.dumps({'before':before,'after':after,'unchanged':True},indent=2))
    print('YOLO_TRAINING_ENVIRONMENT_PASS: owned venv, shared CUDA imports/backward/pip check; base files unchanged; no new framework download')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
    check() if args.check else setup()
