"""Decode a bounded local Unity preview and compare its colors/motion with source PNGs."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import time

os.environ.setdefault('OMP_NUM_THREADS','1')
os.environ.setdefault('OPENBLAS_NUM_THREADS','1')
os.environ.setdefault('OPENCV_FFMPEG_THREADS','1')
import cv2
import numpy as np
from PIL import Image


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--folder',choices=['insect-motion','pet-motion','room-motion','bronze-motion'],required=True)
    parser.add_argument('--model',required=True)
    parser.add_argument('--frames',type=int,required=True)
    parser.add_argument('--fps',type=int,choices=[24,30],required=True)
    args=parser.parse_args()
    assert args.model.isalnum() and 2<=args.frames<=144
    root=Path(__file__).resolve().parents[1]/'.codex-tmp'/args.folder
    movie=root/(args.model+'.mp4');folder=root/args.model
    assert 0<movie.stat().st_size<8*1024**2
    sources=[folder/f'{i:03}.png' for i in range(args.frames)]
    assert all(p.is_file() for p in sources) and sum(p.stat().st_size for p in sources)<=24*1024**2
    cv2.setNumThreads(1)
    capture=cv2.VideoCapture(str(movie),cv2.CAP_FFMPEG)
    assert capture.isOpened()
    count=0;reference={};maximum_color_error=0;started=time.monotonic()
    try:
        assert abs(capture.get(cv2.CAP_PROP_FPS)-args.fps)<.05
        while True:
            assert time.monotonic()-started<30
            ok,frame=capture.read()
            if not ok:break
            assert count<args.frames and frame.shape==(640,640,3)
            if count in {0,args.frames//4,args.frames//2,args.frames-1}:
                rgb=cv2.cvtColor(frame,cv2.COLOR_BGR2RGB)
                with Image.open(sources[count]) as png:original=np.asarray(png.convert('RGB'))
                error=float(np.abs(rgb.astype(np.int16)-original.astype(np.int16)).mean())
                assert error<8, 'Movie color mismatch: '+str(error)
                maximum_color_error=max(maximum_color_error,error);reference[count]=rgb
            count+=1
    finally:capture.release()
    assert count==args.frames
    motion=max(float(np.abs(frame.astype(np.int16)-reference[0].astype(np.int16)).mean()) for i,frame in reference.items() if i)
    assert motion>.03, 'Preview is effectively static'
    report={'model':args.model,'frames_decoded':count,'fps':args.fps,'width':640,'height':640,'bytes':movie.stat().st_size,
        'sha256':hashlib.sha256(movie.read_bytes()).hexdigest(),'maximum_sampled_png_mean_color_error_255':maximum_color_error,
        'maximum_sampled_motion_mean_delta_255':motion,'scope':'Offline Unity pose/render sequence; actual scene lifecycle is verified separately; not headset footage.'}
    (root/(args.model+'-validation.json')).write_text(json.dumps(report,indent=2),encoding='utf8')
    print(json.dumps(report))


if __name__=='__main__':main()
