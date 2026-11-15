"""Standalone PICO PICF debug viewer. No webcam and no deployment changes."""
import argparse
import json
import socket
import threading
import time
from pathlib import Path

import cv2
import numpy as np
import tcp_server as bridge

ROOT = Path(__file__).resolve().parents[1]
DEFAULT = ROOT / 'Find lighter/runs/detect/lighter-yolo26n-assgn2-v6-50e/weights/best.pt'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--weights', type=Path, default=DEFAULT)
    parser.add_argument('--conf', type=float, default=.399)
    parser.add_argument('--port', type=int, default=5005)
    parser.add_argument('--device', default='auto')
    args = parser.parse_args()
    if not args.weights.is_file():
        parser.error(f'Missing weights: {args.weights}')
    if not 0 <= args.conf <= 1:
        parser.error('confidence must be between 0 and 1')
    server = socket.socket()
    try:
        server.bind(('0.0.0.0', args.port))
    except OSError:
        server.close()
        parser.error(f'Port {args.port} unavailable. Stop the existing YOLO server first.')
    server.listen(1)
    server.settimeout(.3)
    stop = threading.Event()
    lock = threading.Lock()
    state = {'image': None, 'status': 'Loading model...', 'updated': 0.0}
    config = bridge.InferenceConfig(args.device, False, 640, args.conf, .7, 1)

    def status(text):
        with lock:
            if state['status'] == text:
                return
            state['status'] = text
        print(text, flush=True)

    def work():
        try:
            bridge.WEIGHTS_PATH = args.weights.resolve()
            bridge.ACTIVE_MODEL_LABEL = args.weights.parent.parent.name
            model, device = bridge.load_model(config)
            bridge.warmup_model(model, config, device)
            while not stop.is_set():
                status('Waiting for PICO connection...')
                try:
                    conn, address = server.accept()
                except socket.timeout:
                    continue
                status(f'Connected: {address[0]}')
                buffer = bridge.LatestFrameBuffer()
                session_stop = threading.Event()
                perf = bridge.PerfAggregator()
                receiver = threading.Thread(target=bridge.receiver_loop,
                    args=(conn, buffer, perf, session_stop), daemon=True)
                receiver.start()
                generation = 0
                previous = time.perf_counter()
                try:
                    while not stop.is_set() and not session_stop.is_set():
                        frame, generation = buffer.wait_for_newer(generation, session_stop)
                        if frame is None:
                            break
                        start = time.perf_counter()
                        result = bridge.run_yolo_predict(model, frame.image, device, config)[0]
                        buffer.mark_consumed(generation)
                        infer_ms = (time.perf_counter()-start)*1000
                        detections = bridge.detections_from_result(result)
                        conn.sendall((json.dumps(dict(frame_id=frame.frame_id,
                            timestamp=frame.timestamp, width=frame.width,
                            height=frame.height, detections=detections))+'\n').encode())
                        now = time.perf_counter()
                        fps = 1/max(now-previous, 1e-6)
                        previous = now
                        picture = result.plot()
                        lines = [f'{frame.width}x{frame.height}  frame {frame.frame_id}  output {fps:.1f} FPS',
                            f'Predict {infer_ms:.1f} ms | conf {args.conf} | max_det 1 | 2D pixels only']
                        if not detections:
                            lines.append('NO LIGHTER DETECTED')
                        for d in detections:
                            lines.append(f"{d['class']} {d['confidence']:.3f} center=({d['centerX']:.1f},{d['centerY']:.1f})")
                            lines.append(f"box=({d['x1']:.0f},{d['y1']:.0f})-({d['x2']:.0f},{d['y2']:.0f})")
                        picture = cv2.copyMakeBorder(picture, 110, 0, 0, max(0,800-picture.shape[1]),cv2.BORDER_CONSTANT)
                        for i,line in enumerate(lines):
                            cv2.putText(picture,line,(10,22+i*23),cv2.FONT_HERSHEY_SIMPLEX,.52,(255,255,255),1)
                        with lock:
                            state.update(image=picture,updated=now,status='Receiving PICO frames')
                except (OSError, ConnectionError) as exc:
                    status(f'Disconnected: {exc}')
                finally:
                    session_stop.set()
                    bridge.shutdown_client(conn)
                    with buffer._cond:
                        buffer._cond.notify_all()
                    receiver.join(timeout=2)
                    conn.close()
        except Exception as exc:
            status(f'ERROR: {type(exc).__name__}: {exc}')

    worker = threading.Thread(target=work,daemon=True)
    worker.start()
    title = 'PICO Real Lighter Debug - Q: quit, S: save screenshot'
    cv2.namedWindow(title,cv2.WINDOW_NORMAL)
    cv2.resizeWindow(title,960,720)
    try:
        while True:
            with lock:
                picture = state['image'].copy() if state['image'] is not None else np.zeros((590,800,3),np.uint8)
                message = state['status']
                age = time.perf_counter()-state['updated'] if state['updated'] else None
            if age is not None and age > 1:
                message += f' | STALE: last frame {age:.1f}s ago'
            picture = cv2.copyMakeBorder(picture,0,65,0,0,cv2.BORDER_CONSTANT)
            cv2.putText(picture,message[:110],(10,picture.shape[0]-40),cv2.FONT_HERSHEY_SIMPLEX,.45,(0,220,255),1)
            cv2.putText(picture,args.weights.parent.parent.name,(10,picture.shape[0]-15),cv2.FONT_HERSHEY_SIMPLEX,.45,(255,255,255),1)
            cv2.imshow(title,picture)
            key = cv2.waitKey(30)&255
            if key in (ord('q'),27) or cv2.getWindowProperty(title,cv2.WND_PROP_VISIBLE)<1:
                break
            if key==ord('s') and age is not None:
                folder=ROOT/'Find lighter/debug_captures'
                folder.mkdir(exist_ok=True)
                path=folder/f'pico_{time.time_ns()}.png'
                if cv2.imwrite(str(path),picture):
                    print(f'Saved {path}',flush=True)
    finally:
        stop.set()
        server.close()
        cv2.destroyAllWindows()


if __name__=='__main__':
    main()
