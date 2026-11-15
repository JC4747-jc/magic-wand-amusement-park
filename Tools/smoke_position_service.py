"""Exercise deployed main/auxiliary weights and v1/v2 switching; no camera or training."""
import json
from pathlib import Path
import socket
import struct
import subprocess
import sys
import time

import cv2
import numpy as np


def main():
    root = Path(__file__).resolve().parents[1]
    # Do not connect to, terminate, or replace an existing detector service.
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 5005))
    output = root / '.codex-tmp/position-smoke'
    output.mkdir(parents=True, exist_ok=True)
    ok, jpeg = cv2.imencode('.jpg', np.zeros((480, 640, 3), dtype=np.uint8))
    if not ok:
        raise RuntimeError('JPEG encoding failed')
    replies = []
    with (output / 'server.log').open('w', encoding='utf-8') as log:
        process = subprocess.Popen([sys.executable, '-u',
            str(root / 'object-detection-demo/tcp_server.py'),
            '--headless', '--host', '127.0.0.1', '--device', 'cpu'],
            cwd=root, stdout=log, stderr=subprocess.STDOUT)
        try:
            deadline = time.monotonic() + 30
            for session in (0, 1):
                while True:
                    if process.poll() is not None:
                        raise RuntimeError(f'Server exited; inspect {output / "server.log"}')
                    try:
                        client = socket.create_connection(('127.0.0.1', 5005), timeout=1)
                        break
                    except OSError:
                        if time.monotonic() >= deadline:
                            raise TimeoutError('Position service startup/reconnect timed out')
                        time.sleep(.05)
                with client:
                    client.settimeout(10)
                    with client.makefile('rb') as response:
                        requests = [(9, None), (10, 4), (11, 5), (12, 6), (13, 7), (14, 7),
                                    (15, 1), (16, 2), (17, 3), (18, None)] if session == 0 else [(1, None)]
                        for frame_id, profile in requests:
                            client.sendall(b'PICF' + (bytes([1]) if profile is None else bytes([2, profile])) + struct.pack(
                                '<IqIII', frame_id, 1000, 640, 480, len(jpeg)) + jpeg.tobytes())
                            reply = json.loads(response.readline(65536))
                            expected_scene = ('lighter','tree','bugs','statue','room','plant','pet','discovery')[profile or 0]
                            if (reply.get('frame_id') != frame_id or reply.get('timestamp') != 1000
                                    or reply.get('width') != 640 or reply.get('height') != 480
                                    or reply.get('scene') != expected_scene
                                    or not isinstance(reply.get('detections'), list)):
                                raise AssertionError(f'Invalid frame reply: {reply}')
                            if profile in (1,2,3):
                                assert reply['status'].startswith('SCENE_NOT_CONFIGURED:'), reply
                            else:
                                assert reply['status'] == 'OK', reply
                            if profile in (4,5,6):
                                assert reply['model_validation'] == 'PUBLIC_BASELINE_ONLY_PICO_PENDING', reply
                            if profile is not None:
                                assert not reply['pose_available'] and not reply['pose_model_ready'], reply
                            replies.append(reply)
            (output / 'replies.json').write_text(json.dumps(replies, indent=2), encoding='utf-8')
            print(json.dumps({'passed': True, 'replies': replies}, indent=2))
        finally:
            if process.poll() is None:
                process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)


if __name__ == '__main__':
    main()
