"""Optional Pet head service for capture-bound stereo crops."""
import argparse
import hashlib
import math
from pathlib import Path
import socket
import struct

import cv2
import numpy as np

from pet_head_geometry import SIZE, head_pixels, match_face_patches, pxr_luminance, stereo_nose_disparity
from pose_bridge import receive_json, recv_exact, send_json


MODEL_SHA256 = '1cfd1c86e0d9e5d5f95178bcd95ee9a4e8386a624cd3c57519f27ff58cac7f28'
HEIGHT, WIDTH = 480, 640
IDENTITY_KEYS = ('schema', 'session', 'frame_id', 'timestamp', 'instance_id')


def load_model(path):
    with path.open('rb') as stream:
        actual = hashlib.file_digest(stream,'sha256').hexdigest()
    if actual != MODEL_SHA256:
        raise ValueError(f'Pet head model SHA256 does not match the verified AP-10K export: {actual}')
    cv2.setNumThreads(2)
    network = cv2.dnn.readNetFromONNX(str(path))
    network.setInput(np.zeros((1,3,SIZE,SIZE), dtype=np.float32))
    network.forward(('simcc_x', 'simcc_y'))
    return network


def validate(header):
    if not isinstance(header,dict):
        raise ValueError('Pet stereo request must be a JSON object')
    if (header['schema']!='pet-head-v1' or (header['width'],header['height'])!=(WIDTH,HEIGHT) or
        not isinstance(header['session'],str) or len(header['session'])!=32 or
        not isinstance(header['instance_id'],str) or not 0<len(header['instance_id'])<=128 or
        type(header['frame_id']) is not int or header['frame_id']<=0 or
        type(header['timestamp']) is not int or header['timestamp']<=0):
        raise ValueError('Invalid Pet stereo request identity')
    x1,y1,x2,y2,d,f,b = (header[name] for name in ('x1','y1','x2','y2','disparity','focal','baseline'))
    if not (all(type(v) in (int,float) and math.isfinite(v) for v in (x1,y1,x2,y2,d,f,b)) and
            0<=x1<x2<=WIDTH and 0<=y1<y2<=HEIGHT and min(x2-x1,y2-y1)>=12 and
            2<=d<120 and f>0 and b>0 and .2<=f*b/d<=3.):
        raise ValueError('Invalid Pet stereo calibration or box')
    return np.array([x1,y1,x2,y2]),d,f*b


def read_crop(client, header, eye, channels):
    x,w,y,h = (header[k] for k in (f'{eye}_x',f'{eye}_w','top','crop_h'))
    if (not all(type(v) is int for v in (x,w,y,h)) or not (w>=12 and h>=12 and
            0<=x<x+w<=WIDTH and 0<=y<y+h<=HEIGHT)):
        raise ValueError('Invalid Pet head crop dimensions')
    count = struct.unpack('<I',recv_exact(client,4))[0]
    if count!=w*h*channels:
        raise ValueError('Incorrect Pet head crop data size')
    crop = np.frombuffer(recv_exact(client,count),np.uint8).reshape(h,w,channels)
    image = np.zeros((HEIGHT,WIDTH,channels),dtype=np.uint8)
    image[y:y+h,x:x+w] = crop
    return image if channels>1 else image[:,:,0]


def estimate(net, header, box, disparity, scale, rgba, gray_right):
    left = cv2.cvtColor(rgba,cv2.COLOR_RGBA2BGR)
    right = cv2.cvtColor(gray_right,cv2.COLOR_GRAY2BGR)
    left_pose = head_pixels(net,left,box)
    right_pose = head_pixels(net,right,box-[disparity,0,disparity,0])
    record = {key:header[key] for key in IDENTITY_KEYS}
    record['verified'] = False
    nose = stereo_nose_disparity(left_pose,right_pose,120,scale,scale/disparity)
    if nose is None:
        return record
    matches = match_face_patches(left_pose,right_pose,(pxr_luminance(left),gray_right),nose,120)
    if 'nose' in matches or len(matches)>=2:
        x,y = left_pose['nose']['xy']
        d = round(x)-matches['nose'][0] if 'nose' in matches else nose
        record.update(verified=True,x=x,y=y,disparity=d)
    return record


def serve(network, host, port, *, before_reply=None):
    with socket.socket() as listener:
        listener.bind((host,port))
        listener.listen(1)
        print(f'PET HEAD READY {listener.getsockname()[1]}',flush=True)
        while True:
            client,_ = listener.accept()
            with client:
                client.settimeout(2)
                while True:
                    try:
                        header = receive_json(client)
                        box, disparity, scale = validate(header)
                        left = read_crop(client,header,'left',4)
                        right = read_crop(client,header,'right',1)
                        reply = estimate(network,header,box,disparity,scale,left,right)
                        if before_reply is not None:
                            before_reply(reply)
                        send_json(client,reply)
                    except (EOFError,OSError,ValueError,TypeError,KeyError,OverflowError):
                        break


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model',type=Path,required=True,help='Local, verified AP-10K end2end.onnx')
    parser.add_argument('--host',default='127.0.0.1')
    parser.add_argument('--port',type=int,default=15006)
    args = parser.parse_args()
    if not 1<=args.port<=65535:
        parser.error('Port must be in 1..65535')
    try:
        serve(load_model(args.model),args.host,args.port)
    except (OSError,ValueError,cv2.error) as error:
        parser.exit(1,f'{error}\n')
    except KeyboardInterrupt:
        pass


if __name__=='__main__':
    main()
