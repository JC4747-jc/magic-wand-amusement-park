"""PC-only health port. Its timeouts never execute in the YOLO inference thread or cross ADB."""
import json
import socket
import threading
import time


class PoseAvailability:
    def __init__(self, host='127.0.0.1', port=5007, expected_detector=None):
        self.host, self.port = host, port
        self.status = (False, False, 0.0)
        self.stop = threading.Event()
        self.expected_detector = expected_detector
        self.detector_mismatch = False

    def start(self):
        threading.Thread(target=self._poll, daemon=True, name='pose-health-monitor').start()

    def snapshot(self):
        image, pose, at = self.status
        fresh = time.monotonic()-at < .75
        return dict(pose_available=image and fresh, pose_model_ready=pose and fresh)

    def _poll(self):
        while not self.stop.is_set():
            try:
                with socket.create_connection((self.host,self.port),timeout=.15) as client:
                    data=bytearray()
                    while b'\n' not in data and len(data)<1024:
                        part=client.recv(1024-len(data))
                        if not part: break
                        data.extend(part)
                    value=json.loads(data)
                    valid=value.get('version')==2
                    mismatch = (self.expected_detector is not None and value.get('pose_ready') is True
                                and value.get('detector') != self.expected_detector)
                    if mismatch != self.detector_mismatch:
                        print("[PoseAvailability] " + ("DETECTOR_MISMATCH: match YOLO weights/conf/imgsz/iou and restart services"
                              if mismatch else "Detector configuration matches again"), flush=True)
                    self.detector_mismatch = mismatch
                    valid = valid and not mismatch
                    self.status=(valid and value.get('image_ready') is True,
                                 valid and value.get('pose_ready') is True,time.monotonic())
            except (OSError, ValueError):
                self.status=(False,False,time.monotonic())
            self.stop.wait(.25)


def serve_health(listener, pose_ready, detector_identity=None):
    """Listener is bound by the parent before accepting image clients; no image/GPU work here."""
    payload=(json.dumps(dict(version=2,image_ready=True,pose_ready=pose_ready,
                            detector=detector_identity))+'\n').encode()
    while True:
        client,_=listener.accept()
        with client:
            client.settimeout(.2)
            try: client.sendall(payload)
            except OSError: pass
