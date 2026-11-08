"""Replay recorded PICO pairs through both live local services; no headset rotation claim."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import gzip
import hashlib
import json
from pathlib import Path
import socket
import statistics
import struct
import sys
import time
import uuid

import cv2

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'object-detection-demo'))
import detector_config
import pose_bridge


def request_position(connection, reader, image, frame_id, timestamp):
    # Unity's flipV=true cancels EncodeToJPG's bottom-up storage conversion.
    # Reproduce the configured position sender's native rows and JPEG quality.
    ok, jpeg = cv2.imencode('.jpg', image, [cv2.IMWRITE_JPEG_QUALITY, 90])
    if not ok:
        raise ValueError('JPEG encode failed')
    height, width = image.shape[:2]
    packet = b'PICF' + bytes([1]) + struct.pack('<IqIII', frame_id, timestamp // 1_000_000,
                                              width, height, jpeg.size)
    started = time.perf_counter()
    connection.sendall(packet + jpeg.tobytes())
    reply = json.loads(reader.readline())
    if reply['frame_id'] != frame_id:
        raise ValueError('Position frame identity mismatch')
    return dict(reply=reply, roundtrip_ms=(time.perf_counter()-started)*1000)


def request_pose(connection, header, image, right):
    rgba = cv2.cvtColor(image, cv2.COLOR_BGR2RGBA)
    header = {**header, 'version': 2, 'pixel_format': 'native_top_down_gzip_rgba_gray'}
    started = time.perf_counter()
    pose_bridge.send_json(connection, header)
    for pixels in (rgba, right):
        packed = gzip.compress(pixels.tobytes(), compresslevel=1)
        connection.sendall(struct.pack('<I', len(packed)) + packed)
    reply = pose_bridge.receive_json(connection)
    for key in ('session', 'frame_id', 'timestamp_ns'):
        if reply[key] != header[key]:
            raise ValueError('Pose frame identity mismatch: ' + key)
    return dict(reply=reply, roundtrip_ms=(time.perf_counter()-started)*1000)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--captures', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    frames = sorted(args.captures.glob('*/header.json'))
    if not frames:
        parser.error('No recorded frame directories found')
    with socket.create_connection(('127.0.0.1', 5007), timeout=3) as health_socket:
        with health_socket.makefile('rb') as health_reader:
            health = json.loads(health_reader.readline())
    sha = hashlib.sha256(detector_config.WEIGHTS_PATH.read_bytes()).hexdigest()
    expected = dict(sha256=sha, conf=detector_config.DEFAULT_CONF,
                    imgsz=detector_config.DEFAULT_IMGSZ, iou=detector_config.DEFAULT_IOU)
    if not health['pose_ready'] or health.get('detector') != expected:
        raise ValueError('Pose service is not using the expected detector')
    rows = []
    session = uuid.uuid4().hex
    with socket.create_connection(('127.0.0.1', 5005), timeout=5) as position, \
         socket.create_connection(('127.0.0.1', 5006), timeout=5) as pose, \
         position.makefile('rb') as reader, ThreadPoolExecutor(max_workers=2) as pool:
        for index, path in enumerate(frames, 1):
            header = json.loads(path.read_text(encoding='utf-8'))
            header.update(session=session, frame_id=index)
            image = cv2.imread(str(path.parent / 'left.png'))
            right = cv2.imread(str(path.parent / 'right.png'), cv2.IMREAD_GRAYSCALE)
            position_result = pool.submit(request_position, position, reader, image, index, header['timestamp_ns'])
            pose_result = pool.submit(request_pose, pose, header, image, right)
            row = dict(frame=path.parent.name, position=position_result.result(), pose=pose_result.result())
            rows.append(row)
            print(f"frame={index} detections={len(row['position']['reply']['detections'])} "
                  f"pose={row['pose']['reply']['reason']}", flush=True)
    successful = [row['pose']['roundtrip_ms'] for row in rows if row['pose']['reply']['valid']]
    summary = dict(frames=len(rows), position_detected=sum(bool(row['position']['reply']['detections']) for row in rows),
                   pose_valid=len(successful), readiness_matches=sum(row['position']['reply']['pose_model_ready'] for row in rows),
                   pose_valid_median_roundtrip_ms=statistics.median(successful) if successful else None)
    report = dict(scope='Recorded pair replay on PC, concurrent position and pose requests; no headset/USB or accuracy validation',
                  detector=expected, summary=summary, rows=rows)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(summary), flush=True)


if __name__ == '__main__':
    main()
