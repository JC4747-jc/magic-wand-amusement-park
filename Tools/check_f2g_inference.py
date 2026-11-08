"""Run real F2G weights on the bundled ROPE RGB-D teapot, not a lighter-accuracy test."""
import argparse
import json
import os
from pathlib import Path
import time

import cv2
import numpy as np
import torch


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,default=Path('/opt/f2g/runtime'))
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    root=args.root
    os.environ['F2G_RADIO_REPOSITORY']=str(root/'RADIO')
    os.environ['F2G_RADIO_CHECKPOINT']=str(root/'weights/radio-v2.5-l_half.pth.tar')
    from f2g_pose import PoseEstimator
    sample=root/'F2G-Pose/examples/rope'
    rgb=cv2.cvtColor(cv2.imread(str(sample/'rgb.png')),cv2.COLOR_BGR2RGB)
    depth=np.load(sample/'depth.npy')
    mask=cv2.imread(str(sample/'mask.png'),cv2.IMREAD_GRAYSCALE)
    k=np.loadtxt(sample/'K.txt')
    torch.cuda.reset_peak_memory_stats()
    started=time.perf_counter()
    estimator=PoseEstimator(str(root/'weights/f2g-pose.pt'),device='cuda:0')
    load_seconds=time.perf_counter()-started
    timings=[]
    for i in range(5):
        torch.cuda.synchronize(); started=time.perf_counter()
        result=estimator.predict(rgb,depth,k,mask,depth_scale=1.0,return_shape=False)
        torch.cuda.synchronize(); elapsed=(time.perf_counter()-started)*1000
        print(f'Inference {i}: {elapsed:.1f} ms',flush=True)
        timings.append(elapsed)
        r=result['rotation']
        assert np.isfinite(r).all() and np.allclose(r.T@r,np.eye(3),atol=.015)
        assert abs(np.linalg.det(r)-1)<.015
    report=dict(sample='official ROPE teapot; not lighter accuracy validation',torch=torch.__version__,
                gpu=torch.cuda.get_device_name(0),load_seconds=load_seconds,
                first_inference_ms=timings[0],warm_inference_ms=timings[1:],
                peak_allocated_mb=torch.cuda.max_memory_allocated()/1024**2,
                peak_reserved_mb=torch.cuda.max_memory_reserved()/1024**2,
                pose={key:value.tolist() if isinstance(value,np.ndarray) else value for key,value in result.items()})
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(report,indent=2),encoding='utf-8')
    print('REAL_F2G_INFERENCE_PASS',flush=True)


if __name__=='__main__': main()
