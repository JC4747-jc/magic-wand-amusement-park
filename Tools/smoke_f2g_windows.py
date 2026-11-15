"""Verify Windows production health monitoring against an owned, bounded WSL smoke."""
import argparse
import json
import os
from pathlib import Path
import platform
import socket
import struct
import subprocess
import sys
import time


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--distro',default='Ubuntu-24.04')
    args=parser.parse_args()
    if platform.system()!='Windows':
        parser.error('Use the Windows project interpreter')
    root=Path(__file__).resolve().parents[1]
    sys.path.insert(0,str(root/'object-detection-demo'))
    from pose_availability import PoseAvailability
    from smoke_f2g_runtime import receive_exact
    with socket.socket() as probe:
        probe.bind(('127.0.0.1',5007))
    wsl=Path(os.environ['ProgramFiles'])/'WSL/wsl.exe'
    linux_script='/mnt/'+root.drive[0].lower()+root.as_posix()[2:]+'/Tools/smoke_f2g_runtime.py'
    output=root/'.codex-tmp'
    output.mkdir(exist_ok=True)
    with (output/'f2g-cross-windows.log').open('w',encoding='utf-8') as log:
        process=subprocess.Popen([str(wsl),'-d',args.distro,'-u','root','--exec',
            '/opt/f2g/venv/bin/python',linux_script,'--hold-seconds','10'],stdout=log,stderr=subprocess.STDOUT)
        monitor=PoseAvailability(expected_detector=dict(sha256='999bca4f5bf96aeed3ac0f1047e31530e4eb4584873c85dd5bd00031fc71fb3a',
            conf=.399,imgsz=640,iou=.7))
        monitor.start()
        try:
            deadline=time.monotonic()+160
            while True:
                state=monitor.snapshot()
                if state['pose_available'] and state['pose_model_ready']:
                    break
                if process.poll() is not None:
                    raise RuntimeError('WSL smoke ended before Windows observed matching readiness')
                if time.monotonic()>deadline:
                    raise TimeoutError('Windows-to-WSL health forwarding failed')
                time.sleep(.1)
            with socket.create_connection(('127.0.0.1',5006),timeout=2) as image:
                body=b'{"op":"health","version":2}'
                image.sendall(struct.pack('<I',len(body))+body)
                size=struct.unpack('<I',receive_exact(image,4))[0]
                if not 0<size<=65536:
                    raise ValueError('Unbounded cross-system image-port reply')
                image_health=json.loads(receive_exact(image,size))
            assert image_health.get('pose_ready') and image_health.get('mode')=='F2G_POSE',image_health
            report=dict(passed=True,windows_pose_availability=state,
                windows_image_port_health=image_health,
                scope='Windows production health monitor to WSL YOLO/SAM/F2G; no headset pose accuracy claim')
            (output/'f2g-cross-windows.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
            print(json.dumps(report,indent=2))
        finally:
            monitor.stop.set()
            # Linux terminates only its own model child, after its bounded startup/hold.
            process.wait(timeout=210)
            if process.returncode:
                raise RuntimeError('Linux smoke failed; inspect f2g-cross-windows.log')


if __name__=='__main__':
    main()
