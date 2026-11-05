"""Actual local-image inference over PICF; never creates headset poses."""
import argparse
import hashlib
import json
import socket
import struct
import time
import math
from pathlib import Path
from PIL import Image

parser=argparse.ArgumentParser()
parser.add_argument('--data',required=True)
parser.add_argument('--weights',required=True)
parser.add_argument('--scene',required=True,choices=['lighter','tree','bugs','statue','room','plant','pet'])
parser.add_argument('--port',type=int,default=5006)
parser.add_argument('--out',required=True)
parser.add_argument('--allowed-classes',nargs='+',help='Verify the active scene class allowlist in actual TCP responses')
parser.add_argument('--expected-validation',help='Require an exact model validation marker')
args=parser.parse_args()
data=Path(args.data)
audit=json.loads((data/'audit.json').read_text(encoding='utf-8'))
weights=Path(args.weights).resolve()
sha=hashlib.sha256(weights.read_bytes()).hexdigest()
scene_id=['lighter','tree','bugs','statue','room','plant','pet'].index(args.scene)
rows=[row for row in audit['samples'] if row['split']=='test']
if not rows:raise ValueError('No test images')
records=[]
session_ids=[]
for session in range(2):
    observed_ids=set()
    with socket.create_connection(('127.0.0.1',args.port),timeout=5) as sock:
        sock.settimeout(30)
        stream=sock.makefile('rb')
        for frame,row in enumerate(rows,1):
            path=data/row['image']
            jpeg=path.read_bytes()
            with Image.open(path) as image:
                if image.format!='JPEG':raise ValueError('This replay requires original JPEG images')
                width,height=image.size
            timestamp=time.monotonic_ns()
            started=time.perf_counter()
            sock.sendall(b'PICF\x02'+bytes([scene_id])+struct.pack('<IqIII',frame,timestamp,width,height,len(jpeg))+jpeg)
            line=stream.readline()
            if not line:raise RuntimeError('Server disconnected without response')
            reply=json.loads(line)
            assert reply['frame_id']==frame and reply['timestamp']==timestamp
            assert reply['scene']==args.scene and reply['status']=='OK'
            assert reply['width']==width and reply['height']==height
            assert Path(reply['weights']).resolve()==weights
            assert reply['weights_sha256']==sha
            if args.expected_validation:assert reply.get('model_validation')==args.expected_validation
            for detection in reply['detections']:
                if args.allowed_classes:assert detection['class'] in args.allowed_classes
                x1,y1,x2,y2=(detection[k] for k in ('x1','y1','x2','y2'))
                assert all(math.isfinite(v) for v in (x1,y1,x2,y2,detection['confidence']))
                assert 0<=x1<x2<=width and 0<=y1<y2<=height
                assert 0<=detection['confidence']<=1
                observed_ids.add(detection['instance_id'])
            if args.scene=='statue':assert all(d['class']=='Statue' for d in reply['detections'])
            records.append(dict(session=session,image=row['image'],response=reply,
                                tcp_ms=(time.perf_counter()-started)*1000))
    session_ids.append(observed_ids)
assert not session_ids[0].intersection(session_ids[1]), 'Target identity leaked across reconnect sessions'
report=dict(source='Original public JPEGs; synthetic local monotonic capture clock',
    scope='Actual best.pt inference + TCP frame/dimension/model identity and reconnect verification',
    headset_images=False,unity_stereo=False,animation=False,
    deployment_accepted=False,weight_sha256=sha,results=records)
Path(args.out).write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(dict(tcp_responses=len(records),reconnect_sessions=2,
    detections_per_response=[len(r['response']['detections']) for r in records],
    weight_sha256=sha,headset_e2e=False)))
