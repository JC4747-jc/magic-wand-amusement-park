"""CPU regression checks; no downloaded weights or pretend model inference."""
import json
import gzip
import socket
import struct
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from pathlib import Path

import cv2
import numpy as np

import pose_bridge as bridge


def header():
    return dict(version=1, pixel_format="native_top_down_rgba_gray", width=640, height=480,
                left_bytes=640*480*4, right_bytes=640*480, session="test-session", frame_id=1,
                timestamp_ns=1_000_000_000, right_timestamp_ns=1_001_000_000,
                left_k=[320,0,320,0,320,240,0,0,1], right_k=[320,0,320,0,320,240,0,0,1],
                right_from_left_r=np.eye(3).reshape(-1).tolist(), right_from_left_t=[-.06,0,0])


def stereo_pair():
    gray = np.random.default_rng(42).integers(0,256,(480,640),dtype=np.uint8)
    gray = cv2.GaussianBlur(gray,(3,3),.6)
    right = np.zeros_like(gray)
    right[:,:-16] = gray[:,16:]
    return cv2.cvtColor(gray,cv2.COLOR_GRAY2RGB),right


class PoseBridgeTests(unittest.TestCase):
    def test_native_detector_box_maps_to_upright_sam_and_back(self):
        from types import SimpleNamespace
        from unittest.mock import Mock
        rgb = np.arange(40*60*3, dtype=np.uint8).reshape(40,60,3)
        coordinates = Mock()
        coordinates.cpu.return_value.numpy.return_value = np.array([10.,6.,20.,16.])
        box = SimpleNamespace(cls=np.array(0), xyxy=[coordinates])
        detector = Mock()
        detector.predict.return_value = [SimpleNamespace(names={0:'lighter'}, boxes=[box])]
        upright_mask = np.zeros((40,60), dtype=bool)
        upright_mask[23:34,10:21] = True
        sam = Mock()
        sam.predict.return_value = (np.array([upright_mask]), np.array([.99]), None)
        models = bridge.Models.__new__(bridge.Models)
        models.detector, models.sam = detector, sam
        models.native_bottom_up, models.device = True, 'cpu'
        models.detector_options = dict(conf=.399, imgsz=640, iou=.7)
        mask = models.segment(rgb)
        np.testing.assert_array_equal(detector.predict.call_args.kwargs['source'], cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR))
        np.testing.assert_array_equal(sam.set_image.call_args.args[0], rgb[::-1])
        np.testing.assert_array_equal(sam.predict.call_args.kwargs['box'], [10,23,20,33])
        expected = np.zeros((40,60), dtype=bool)
        expected[6:17,10:21] = True
        np.testing.assert_array_equal(mask, expected)

    def test_bottom_up_model_inputs_and_native_rotation(self):
        from types import SimpleNamespace
        rgb = np.arange(4*6*3, dtype=np.uint8).reshape(4,6,3)
        depth = np.arange(24, dtype=np.float32).reshape(4,6)
        mask = depth > 10
        k = np.array([[10.,0,2.5],[0,11.,1.2],[0,0,1.]])
        rotation, _ = cv2.Rodrigues(np.array([.2,.3,-.1]))
        def predict(actual_rgb, actual_depth, actual_k, actual_mask, **kwargs):
            np.testing.assert_array_equal(actual_rgb, rgb[::-1])
            np.testing.assert_array_equal(actual_depth, depth[::-1])
            np.testing.assert_array_equal(actual_mask, mask[::-1])
            self.assertAlmostEqual(actual_k[1,2], 1.8)
            return {"rotation": rotation}
        models = bridge.Models.__new__(bridge.Models)
        models.native_bottom_up = True
        models.pose = SimpleNamespace(predict=predict)
        result = models.estimate(rgb, depth, k, mask)["rotation"]
        reflection = np.diag([1.,-1.,1.])
        point = np.array([.1,.2,1.])
        np.testing.assert_allclose(result @ (reflection @ point), reflection @ rotation @ point)
        self.assertAlmostEqual(np.linalg.det(result), 1.)
        self.assertAlmostEqual(k[1,2], 1.2)

    def test_partial_client_does_not_block_second_health_client(self):
        with socket.socket() as reserve:
            reserve.bind(('127.0.0.1',0)); port=reserve.getsockname()[1]
        process=subprocess.Popen([sys.executable,str(Path(bridge.__file__)), '--record-only','--port',str(port)],
                                 stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
        first=None
        try:
            deadline=time.monotonic()+10
            while first is None:
                try: first=socket.create_connection(('127.0.0.1',port),timeout=1)
                except OSError:
                    if process.poll() is not None or time.monotonic()>deadline: self.fail('Server unavailable')
                    time.sleep(.05)
            first.sendall(b'\x10') # incomplete prefix, intentionally holds the first client
            with socket.create_connection(('127.0.0.1',port),timeout=1) as second:
                bridge.send_json(second,dict(version=2,op='health'))
                self.assertFalse(bridge.receive_json(second)['pose_ready'])
        finally:
            if first: first.close()
            process.terminate(); process.wait(timeout=5)

    def test_compressed_frame_preserves_native_pixels(self):
        a,b = socket.socketpair()
        rng = np.random.default_rng(12)
        left = rng.integers(0,256,(480,640,4),dtype=np.uint8)
        right = rng.integers(0,256,(480,640),dtype=np.uint8)
        h = header() | dict(version=2, pixel_format="native_top_down_gzip_rgba_gray")
        def send():
            bridge.send_json(a,h)
            for pixels in (left,right):
                packed=gzip.compress(pixels.tobytes(),compresslevel=1)
                a.sendall(struct.pack('<I',len(packed))+packed)
        try:
            worker=threading.Thread(target=send); worker.start()
            _, rgb, actual_right=bridge.receive_frame(b)
            worker.join(3)
            self.assertFalse(worker.is_alive())
            np.testing.assert_array_equal(rgb,left[:,:,:3])
            np.testing.assert_array_equal(actual_right,right)
        finally: a.close(); b.close()

    def test_compressed_size_limit(self):
        a,b=socket.socketpair()
        try:
            packed=gzip.compress(bytes(10000))
            a.sendall(struct.pack('<I',len(packed))+packed)
            with self.assertRaises(ValueError): bridge.receive_compressed(b,100)
        finally: a.close(); b.close()

    def test_record_server_roundtrip_and_reconnect(self):
        with socket.socket() as reservation:
            reservation.bind(("127.0.0.1",0))
            port=reservation.getsockname()[1]
        with tempfile.TemporaryDirectory() as directory:
            with open(Path(directory)/"server.log","w+") as log:
                process=subprocess.Popen([sys.executable,str(Path(bridge.__file__)),"--record-only",
                    "--record-dir",directory,"--port",str(port)],stdout=log,stderr=log)
                try:
                    deadline=time.monotonic()+10
                    while True:
                        try:
                            client=socket.create_connection(("127.0.0.1",port),timeout=2)
                            break
                        except OSError:
                            if process.poll() is not None or time.monotonic()>deadline:
                                log.seek(0); self.fail("Server failed to start: "+log.read())
                            time.sleep(.05)
                    rgb,right=stereo_pair()
                    rgba=cv2.cvtColor(rgb,cv2.COLOR_RGB2RGBA)
                    for index in range(2):
                        if index:
                            client=socket.create_connection(("127.0.0.1",port),timeout=3)
                        with client:
                            bridge.send_json(client,dict(version=2,op="health"))
                            health=bridge.receive_json(client)
                            self.assertFalse(health['pose_ready'])
                            self.assertTrue(health['image_ready'])
                            self.assertEqual(health['mode'],'RECORD_ONLY')
                            bridge.send_json(client,header() | dict(frame_id=index+1))
                            client.sendall(rgba.tobytes()+right.tobytes())
                            reply=bridge.receive_json(client)
                            self.assertEqual(reply["frame_id"],index+1)
                            self.assertEqual(reply["session"],"test-session")
                            self.assertFalse(reply["valid"])
                    self.assertEqual(len(list(Path(directory).rglob("depth_m.npy"))),2)
                finally:
                    process.terminate()
                    process.wait(timeout=5)

    def test_native_frame_transport_and_identity(self):
        left = np.zeros((480,640,4),np.uint8)
        left[0,0] = [255,2,3,255]
        right = np.zeros((480,640),np.uint8)
        h = header()
        a,b = socket.socketpair()
        try:
            def send():
                bridge.send_json(a,h)
                a.sendall(left.tobytes()+right.tobytes())
            thread = threading.Thread(target=send); thread.start()
            received,rgb,_ = bridge.receive_frame(b)
            thread.join(3)
            self.assertFalse(thread.is_alive())
            self.assertEqual(received["timestamp_ns"],h["timestamp_ns"])
            np.testing.assert_array_equal(rgb[0,0],[255,2,3])
        finally:
            a.close(); b.close()

    def test_truncated_payload(self):
        a,b = socket.socketpair()
        a.sendall(b"ab"); a.close()
        try:
            with self.assertRaises(EOFError): bridge.recv_exact(b,4)
        finally: b.close()

    def test_oversized_json(self):
        a,b = socket.socketpair()
        try:
            a.sendall(struct.pack("<I",bridge.MAX_JSON+1))
            with self.assertRaises(ValueError): bridge.receive_json(b)
        finally: a.close(); b.close()

    def test_reject_invalid_headers(self):
        changes = [dict(version=9),dict(left_bytes=1),dict(width=0),
                   dict(right_timestamp_ns=1_020_000_000),dict(right_from_left_t=[0,0,0]),
                   dict(left_k=[-1,0,320,0,320,240,0,0,1]),dict(session=""),dict(frame_id=0)]
        for change in changes:
            with self.subTest(change=change):
                with self.assertRaises(ValueError): bridge.validate_header(header() | change)

    def test_reject_improper_rotation(self):
        for bad in [np.diag([1,1,-1]),np.zeros((3,3)),np.full((3,3),np.nan)]:
            with self.assertRaises(ValueError): bridge.check_rotation(bad)

    def test_metric_depth_and_rgb_alignment(self):
        rgb,right = stereo_pair()
        stereo = bridge.StereoDepth()
        rect,rr = stereo.rectify(header(),rgb,right)
        depth = stereo.compute(rect,rr)
        region = depth[60:420,170:460]
        valid = region[region>0]
        self.assertGreater(valid.size,region.size*.8)
        # z=fx*baseline/disparity=320*.06/16=1.2 metres.
        self.assertAlmostEqual(float(np.median(valid)),1.2,places=2)
        np.testing.assert_allclose(rect[100,250],rgb[100,250],atol=1)

    def test_reject_reversed_stereo(self):
        rgb,right = stereo_pair()
        with self.assertRaises(ValueError):
            bridge.StereoDepth().rectify(header() | dict(right_from_left_t=[.06,0,0]),rgb,right)

    def test_mask_depth_gate(self):
        mask = np.zeros((100,100),bool); mask[20:80,20:80] = True
        for depth in [np.zeros((100,100)),np.full((100,100),np.nan)]:
            with self.assertRaises(ValueError): bridge.foreground_depth(depth,mask)
        depth,count,ratio = bridge.foreground_depth(np.ones((100,100)),mask)
        self.assertGreater(count,100); self.assertEqual(ratio,1)
        self.assertEqual(depth[0,0],0)
        self.assertEqual(depth[20,20],0)  # silhouette erosion

    def test_record_only_never_emits_pose(self):
        rgb,right = stereo_pair()
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)/"frame"
            bridge.save_capture(output,header(),rgb,right)
            result = bridge.process(header(),rgb,right,bridge.StereoDepth(),None,output)
            self.assertFalse(result["valid"])
            self.assertNotIn("rotation",result)
            self.assertTrue((output/"rectified_rgb.png").is_file())
            self.assertTrue((output/"depth_m.npy").is_file())
            self.assertEqual(json.loads((output/"header.json").read_text())["session"],"test-session")

    def test_rectified_rotation_returned_to_native_camera(self):
        # Explicit test double checks coordinate plumbing, NOT F2G-Pose accuracy.
        class FakeModels:
            def segment(self,rgb):
                mask=np.zeros(rgb.shape[:2],bool); mask[100:350,200:400]=True
                return mask
            def estimate(self,rgb,depth,k,mask):
                return {"rotation":np.eye(3)}
        rgb,right=stereo_pair()
        stereo=bridge.StereoDepth()
        angle=.02
        r=cv2.Rodrigues(np.array([0,angle,0]))[0]
        h=header() | dict(right_from_left_r=r.reshape(-1).tolist())
        # Warp correspondence for a rotated right camera from the original rectified plane.
        k=np.array(h["right_k"]).reshape(3,3)
        rotated=cv2.warpPerspective(right,k@r@np.linalg.inv(k),(640,480))
        result=bridge.process(h,rgb,rotated,stereo,FakeModels())
        self.assertTrue(result["valid"])
        np.testing.assert_allclose(np.array(result["rotation"]).reshape(3,3),stereo.r1.T,atol=1e-6)


if __name__ == "__main__":
    unittest.main(verbosity=2)
