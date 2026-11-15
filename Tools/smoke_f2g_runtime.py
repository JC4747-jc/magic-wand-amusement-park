"""Linux-only real-model warmup/readiness probe. Owns and stops only its child server."""
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


def receive_exact(client, count):
    data = bytearray()
    while len(data) < count:
        part = client.recv(count-len(data))
        if not part:
            raise EOFError('Truncated image-port health reply')
        data.extend(part)
    return bytes(data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', type=Path, default=Path('/opt/f2g/runtime'))
    parser.add_argument('--hold-seconds', type=int, choices=range(31), default=0,
                        help='Bounded readiness window for the Windows-to-WSL probe (0-30 seconds)')
    args = parser.parse_args()
    if platform.system() != 'Linux':
        parser.error('Run with the dedicated Linux CUDA interpreter')
    root = Path(__file__).resolve().parents[1]
    output = root / '.codex-tmp/f2g-runtime-smoke'
    output.mkdir(parents=True, exist_ok=True)
    for port in (5006, 5007):
        with socket.socket() as probe:
            probe.bind(('127.0.0.1', port))
    env = os.environ.copy()
    env.update(F2G_RADIO_REPOSITORY=str(args.runtime/'RADIO'),
               F2G_RADIO_CHECKPOINT=str(args.runtime/'weights/radio-v2.5-l_half.pth.tar'),
               OMP_NUM_THREADS='2', MKL_NUM_THREADS='2', MAX_JOBS='1')
    command = [sys.executable, '-u', str(root/'object-detection-demo/pose_bridge.py'),
               '--host','127.0.0.1','--port','5006','--health-port','5007',
               '--checkpoint',str(args.runtime/'weights/f2g-pose.pt'),
               '--sam',str(args.runtime/'weights/sam_vit_b_01ec64.pth'),
               '--native-bottom-up','--warmup-sample',str(args.runtime/'F2G-Pose/examples/rope')]
    with (output/'server.log').open('w', encoding='utf-8') as log:
        process = subprocess.Popen(command, cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            deadline = time.monotonic()+180
            while True:
                if process.poll() is not None:
                    raise RuntimeError('Model server exited; inspect '+str(output/'server.log'))
                try:
                    client = socket.create_connection(('127.0.0.1',5007),timeout=1)
                    break
                except OSError:
                    if time.monotonic()>deadline:
                        raise TimeoutError('Combined real-model warmup/readiness exceeded 180 seconds')
                    time.sleep(.2)
            with client:
                client.settimeout(2)
                with client.makefile('rb') as stream:
                    health = json.loads(stream.readline(65536))
            assert health['version']==2 and health['pose_ready'] and health['image_ready'], health
            assert health['detector']['sha256']=='999bca4f5bf96aeed3ac0f1047e31530e4eb4584873c85dd5bd00031fc71fb3a', health
            with socket.create_connection(('127.0.0.1',5006),timeout=2) as image:
                body = b'{"op":"health","version":2}'
                image.sendall(struct.pack('<I',len(body))+body)
                size = struct.unpack('<I',receive_exact(image,4))[0]
                if not 0 < size <= 65536:
                    raise ValueError('Unbounded image-port health reply')
                image_health = json.loads(receive_exact(image,size))
            assert image_health.get('pose_ready') and image_health.get('mode')=='F2G_POSE', image_health
            report = dict(passed=True,health=health,image_port_health=image_health,
                          scope='Official ROPE sample plus actual YOLO/SAM/F2G warmup and readiness; no PICO pose accuracy claim')
            (output/'result.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
            print(json.dumps(report,indent=2))
            if args.hold_seconds:
                time.sleep(args.hold_seconds)
        finally:
            if process.poll() is None:
                process.terminate()
            try:
                process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)


if __name__ == '__main__':
    main()
