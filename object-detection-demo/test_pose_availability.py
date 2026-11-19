import json
import socket
import threading
import time
import unittest
from unittest.mock import patch
from pose_availability import PoseAvailability


class AvailabilityTests(unittest.TestCase):
    def test_invalid_health_replies_do_not_kill_polling(self):
        invalid = [b'[]\n', b'null\n', b'1\n', b'"ready"\n',
                   b'{broken}\n', b'{"version":2}', b'x'*1024]
        ready = b'{"version":2,"image_ready":true,"pose_ready":true}\n'
        for reply in invalid:
            with self.subTest(reply=reply[:40]):
                monitor = PoseAvailability()
                replies = iter((reply, ready))

                class Client:
                    def __enter__(self): return self
                    def __exit__(self, *args): pass
                    def settimeout(self, timeout): pass
                    def recv(self, size):
                        value = self.reply
                        self.reply = b''
                        return value

                def connect(*args, **kwargs):
                    client = Client()
                    client.reply = next(replies)
                    return client

                states = []
                def wait(_):
                    states.append(monitor.snapshot())
                    if len(states) == 2:
                        monitor.stop.set()

                with patch('pose_availability.socket.create_connection', side_effect=connect), \
                     patch.object(monitor.stop, 'wait', side_effect=wait):
                    monitor._poll()
                self.assertFalse(states[0]['pose_available'])
                self.assertTrue(states[1]['pose_model_ready'])

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

    def test_slow_drip_reply_has_total_deadline_and_poll_recovers(self):
        ready=b'{"version":2,"image_ready":true,"pose_ready":true}\n'
        listener=socket.socket();listener.bind(('127.0.0.1',0));listener.listen(2);listener.settimeout(2)
        stop_server=threading.Event();accepted=threading.Event()
        monitor=PoseAvailability(port=listener.getsockname()[1])
        def serve():
            try:
                peer,_=listener.accept()
                with peer:
                    accepted.set()
                    for byte in ready:
                        if stop_server.wait(.025):return
                        try:peer.sendall(bytes([byte]))
                        except OSError:break
                if not stop_server.is_set():
                    peer,_=listener.accept()
                    with peer:peer.sendall(ready)
            except OSError:pass
        worker=threading.Thread(target=serve);worker.start();monitor.start()
        try:
            self.assertTrue(accepted.wait(1),'Monitor did not connect to local fixture')
            deadline=time.monotonic()+.85
            while not monitor.snapshot()['pose_model_ready'] and time.monotonic()<deadline:time.sleep(.01)
            self.assertTrue(monitor.snapshot()['pose_model_ready'],'Slow drip blocked the next healthy poll')
        finally:
            monitor.stop.set();stop_server.set();listener.close();worker.join(2)
        self.assertFalse(worker.is_alive(),'Owned test server did not stop')

    def test_connect_and_reply_share_budget(self):
        monitor=PoseAvailability();now=[10.0]
        ready=b'{"version":2,"image_ready":true,"pose_ready":true}\n'
        class Client:
            def __enter__(self):return self
            def __exit__(self,*args):pass
            def settimeout(self,timeout):pass
            def recv(self,size):
                now[0]+=.06
                return ready
        def connect(*args,**kwargs):
            now[0]+=.12
            return Client()
        # Each phase fits .15 seconds alone, but the combined reply is late.
        with patch('pose_availability.time.monotonic',side_effect=lambda:now[0]), \
             patch('pose_availability.socket.create_connection',side_effect=connect), \
             patch.object(monitor.stop,'wait',side_effect=lambda _:monitor.stop.set()):
            monitor._poll()
            self.assertFalse(monitor.snapshot()['pose_model_ready'],'Late full reply was stamped fresh')

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
