"""Windows production monitor and framed-health check; no model or synthetic scene input."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import socket
import struct
import sys
import time

ROOT = Path(__file__).resolve().parents[1]


def receive_exact(client, count):
    value = bytearray()
    while len(value) < count:
        part = client.recv(count - len(value))
        if not part:
            raise EOFError('Incomplete framed health response')
        value.extend(part)
    return bytes(value)


def framed_health():
    with socket.create_connection(('127.0.0.1', 5006), timeout=1) as client:
        client.settimeout(1)
        body = b'{"op":"health","version":2}'
        client.sendall(struct.pack('<I', len(body)) + body)
        size = struct.unpack('<I', receive_exact(client, 4))[0]
        if not 0 < size <= 65536:
            raise ValueError('Unbounded image-port health packet')
        reply = json.loads(receive_exact(client, size))
    if reply.get('pose_ready') is not True or reply.get('mode') != 'F2G_POSE':
        raise ValueError('Production image port not ready')
    return reply


def write_once(folder, name, value):
    target = folder / name
    if target.exists():
        raise FileExistsError('Preserve the existing client receipt')
    temporary = target.with_suffix('.pending')
    temporary.write_text(json.dumps(value, indent=2) + '\n', 'utf-8')
    temporary.replace(target)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--folder', type=Path, required=True)
    parser.add_argument('--nonce', required=True)
    parser.add_argument('--unit', required=True)
    args = parser.parse_args()
    folder = args.folder.resolve()
    if not folder.is_relative_to((ROOT / '.codex-tmp/f2g-server-guard').resolve()):
        parser.error('Use the owned server folder')
    path = folder / 'ready.json'
    if path.stat().st_size > 16384:
        raise ValueError('Oversized server ready marker')
    ready = json.loads(path.read_text('utf-8-sig'))
    if ready.get('nonce') != args.nonce or ready.get('unit') != args.unit or ready.get('portsOwnedByChild') != [5006, 5007]:
        raise ValueError('Ready marker ownership mismatch')
    gate = folder / 'client-admit.json'
    deadline = time.monotonic() + 3
    while not gate.exists():
        if time.monotonic() > deadline:
            raise TimeoutError('Windows Job admission was not published')
        time.sleep(.02)
    if gate.stat().st_size > 16384:
        raise ValueError('Oversized Windows admission marker')
    admitted = json.loads(gate.read_text('utf-8-sig'))
    if (admitted.get('nonce') != args.nonce or admitted.get('unit') != args.unit
            or admitted.get('pid') != os.getpid()
            or admitted.get('sourceSha256') != hashlib.sha256(Path(__file__).read_bytes()).hexdigest()):
        raise ValueError('Client admission identity/source mismatch')
    seconds = admitted.get('sessionSeconds', 0)
    if type(seconds) is not int or not 0 <= seconds <= 600:
        raise ValueError('Invalid bounded Windows watch duration')
    sys.path.insert(0, str(ROOT / 'object-detection-demo'))
    from pose_availability import PoseAvailability
    from detector_config import WEIGHTS_PATH, DEFAULT_CONF, DEFAULT_IMGSZ, DEFAULT_IOU
    value = hashlib.sha256()
    with WEIGHTS_PATH.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    expected = {'sha256': value.hexdigest(), 'conf': DEFAULT_CONF, 'imgsz': DEFAULT_IMGSZ, 'iou': DEFAULT_IOU}
    monitor = PoseAvailability(expected_detector=expected)
    monitor.start()
    try:
        deadline = time.monotonic() + 4
        while True:
            state = monitor.snapshot()
            if state['pose_available'] and state['pose_model_ready']:
                break
            if time.monotonic() > deadline:
                raise TimeoutError('Production Windows monitor did not see matching readiness')
            time.sleep(.05)
        reply = framed_health()
        if any(name in sys.modules for name in ('torch', 'cv2', 'numpy')):
            raise RuntimeError('Windows monitor unexpectedly imported heavy libraries')
        report = {'passed': True, 'nonce': args.nonce, 'unit': args.unit,
                  'productionWindowsMonitor': state, 'expectedDetector': expected,
                  'framedImagePortHealth': reply, 'heavyModulesLoaded': False,
                  'scope': 'Current production loopback readiness, identity and framing; no stereo frame/PICO/accuracy claim'}
        write_once(folder, 'windows-initial.json', {**report, 'pid': os.getpid()})
        watched = time.monotonic()
        checks = 1
        recent = []
        while time.monotonic() - watched < seconds:
            time.sleep(min(2, max(0, seconds - (time.monotonic() - watched))))
            state = monitor.snapshot()
            if not state['pose_available'] or not state['pose_model_ready']:
                raise RuntimeError('Production monitor lost readiness during bounded watch')
            reply = framed_health()
            checks += 1
            recent.append({'sinceReadySeconds': round(time.monotonic() - watched, 3),
                           'monitorReady': True, 'framedPortReady': True})
            recent = recent[-10:]
            if checks == 2:
                write_once(folder, 'windows-active.json', {'nonce': args.nonce, 'unit': args.unit,
                           'pid': os.getpid(), 'successfulHealthChecks': checks})
        report.update(sessionSeconds=seconds, observedWatchSeconds=time.monotonic() - watched,
                      successfulHealthChecks=checks, recentChecks=recent,
                      productionWindowsMonitor=state, framedImagePortHealth=reply)
    finally:
        monitor.stop.set()
    write_once(folder, 'windows-client.json', report)
    print(json.dumps({'passed': True, 'productionWindowsMonitorReady': True, 'framedPortReady': True}))


if __name__ == '__main__':
    main()
