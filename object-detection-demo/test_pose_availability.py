import json
import socket
import threading
import time
import unittest
from pose_availability import PoseAvailability


class AvailabilityTests(unittest.TestCase):
    def test_detector_mismatch_blocks_real_pose_but_keeps_record_mode(self):
        expected = dict(sha256='new-model', conf=.399, imgsz=640, iou=.7)
        cases = [(expected, True, True),
                 ({**expected, 'sha256':'old-model'}, True, False),
                 ({**expected, 'conf':.5}, True, False),
                 (None, True, False), (None, False, True)]
        for identity, pose_ready, available in cases:
            with self.subTest(identity=identity, pose_ready=pose_ready):
                listener = socket.socket()
                listener.bind(('127.0.0.1', 0)); listener.listen(1); listener.settimeout(3)
                monitor = PoseAvailability(port=listener.getsockname()[1], expected_detector=expected)
                def serve_once():
                    peer, _ = listener.accept()
                    with peer:
                        peer.sendall((json.dumps(dict(version=2, image_ready=True,
                            pose_ready=pose_ready, detector=identity))+'\n').encode())
                worker = threading.Thread(target=serve_once)
                worker.start(); monitor.start()
                try:
                    deadline = time.monotonic()+2
                    while monitor.status[2] == 0 and time.monotonic() < deadline:
                        time.sleep(.01)
                    self.assertGreater(monitor.status[2], 0)
                    self.assertEqual(monitor.snapshot()['pose_available'], available)
                    self.assertEqual(monitor.snapshot()['pose_model_ready'], available and pose_ready)
                finally:
                    monitor.stop.set(); worker.join(3); listener.close()

    def test_snapshot_has_no_io_and_expires(self):
        monitor=PoseAvailability()
        self.assertFalse(monitor.snapshot()['pose_available'])
        monitor.status=(True,False,time.monotonic())
        self.assertEqual(monitor.snapshot(),dict(pose_available=True,pose_model_ready=False))
        monitor.status=(True,True,time.monotonic()-1)
        self.assertFalse(monitor.snapshot()['pose_available'])

    def test_poll_recovers_then_withdraws_after_service_stops(self):
        listener=socket.socket(); listener.bind(('127.0.0.1',0)); listener.listen(1)
        monitor=PoseAvailability(port=listener.getsockname()[1])
        def serve_once():
            peer,_=listener.accept()
            with peer:
                peer.sendall((json.dumps(dict(version=2,image_ready=True,pose_ready=True))+'\n').encode())
        worker=threading.Thread(target=serve_once); worker.start(); monitor.start()
        try:
            deadline=time.monotonic()+2
            while not monitor.snapshot()['pose_model_ready'] and time.monotonic()<deadline: time.sleep(.01)
            self.assertTrue(monitor.snapshot()['pose_model_ready'])
            worker.join(1); listener.close()
            deadline=time.monotonic()+2
            while monitor.snapshot()['pose_available'] and time.monotonic()<deadline: time.sleep(.01)
            self.assertFalse(monitor.snapshot()['pose_available'])
        finally: monitor.stop.set(); listener.close()


if __name__=='__main__': unittest.main()
