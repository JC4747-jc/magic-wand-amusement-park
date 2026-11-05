"""Actual discovery inference/routing, public JPEG, synthetic local clock; no headset pose."""
import argparse
import hashlib
import json
import socket
import struct
import sys
import time
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'object-detection-demo'))
from discovery import DiscoveryPool


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--port',type=int,default=5017)
    parser.add_argument('--out',default='Reports/cockroach-v16-discovery-cuda-smoke.json')
    args=parser.parse_args()
    out=ROOT/args.out
    assert not out.exists()
    profile=ROOT/'object-detection-demo/scene_profiles.cockroach_v16_public_pilot.json'
    pool=DiscoveryPool(profile,profile.parent)
    data=ROOT/'Datasets/cockroach-fresh-test-v17'
    row=next(r for r in json.loads((data/'audit.json').read_text())['samples'] if r['cohort']=='positive')
    path=data/row['image'];jpeg=path.read_bytes()
    with Image.open(path) as image:w,h=image.size
    results=[];bug_detections=0
    for session in range(2):
        with socket.create_connection(('127.0.0.1',args.port),timeout=10) as sock:
            sock.settimeout(120);stream=sock.makefile('rb')
            for index in range(len(pool.sources)*3):
                stamp=time.monotonic_ns();frame=index+1;start=time.perf_counter()
                sock.sendall(b'PICF\x02\x07'+struct.pack('<IqIII',frame,stamp,w,h,len(jpeg))+jpeg)
                reply=json.loads(stream.readline());source=pool.sources[index%len(pool.sources)]
                assert reply['status']=='OK' and reply['scene']=='discovery'
                assert (reply['frame_id'],reply['timestamp'],reply['width'],reply['height'])==(frame,stamp,w,h)
                assert Path(reply['weights']).resolve()==source['path'] and reply['weights_sha256']==source['sha256']
                assert reply['discovery_model_budget']==1 and reply['discovery_model_count']==len(pool.sources)
                for detection in reply['detections']:
                    assert detection['class_name']!='Beetle' and detection['target_scene']!='room'
                    if 'bugs' in source['scenes']:
                        assert detection['class_name']=='Cockroach' and detection['target_scene']=='bugs'
                        assert detection['confidence']>=.65
                        bug_detections+=1
                if 'bugs' in source['scenes']:
                    assert 'PICO_PENDING' in reply['model_validation']
                results.append(dict(session=session,response=reply,tcp_ms=(time.perf_counter()-start)*1000))
    assert bug_detections==6, 'Expected a bug result in each of six actual bug-model slots'
    report=dict(status='PASS_DISCOVERY_TCP',profile_sha256=hashlib.sha256(profile.read_bytes()).hexdigest(),
        source_image=row['image'],sent_jpeg_sha256=hashlib.sha256(jpeg).hexdigest(),results=results,
        bug_detections=bug_detections,model_budget=1,model_sources=len(pool.sources),
        scope='Actual public JPEG inference, model rotation and bugs routing across reconnects; known replay image, not new accuracy evaluation',
        unity=False,pico=False,stereo=False,animation=False)
    out.write_text(json.dumps(report,indent=2));print(json.dumps(dict(status=report['status'],responses=len(results),bug_detections=bug_detections)))


if __name__=='__main__':main()
