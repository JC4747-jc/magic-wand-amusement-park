import socket
import struct
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import numpy as np

import pet_head_bridge as bridge


def header():
    return dict(schema='pet-head-v1', width=640, height=480, session='f'*32, instance_id='tracker:left:1',
                frame_id=3, timestamp=10000, x1=220, y1=180, x2=320, y2=340, disparity=80,
                focal=425, baseline=.3, left_x=180, left_w=170, right_x=100, right_w=170, top=150, crop_h=210)


class PetHeadProtocolTests(unittest.TestCase):
    def test_rejects_nonobject_invalid_boxes_and_nonfinite_disparity(self):
        for value in ([], header() | {'x1':-1}, header() | {'disparity':float('nan')},
                      header() | {'frame_id':True}, header() | {'width':1920}):
            with self.subTest(value=value), self.assertRaises(ValueError):
                bridge.validate(value)

    def test_crop_restores_native_coordinates(self):
        sender, receiver = socket.socketpair()
        with sender, receiver:
            packet = np.zeros((210,170),dtype=np.uint8)
            packet[110,77] = 211
            data = packet.tobytes()
            sender.sendall(struct.pack('<I',len(data))+data)
            rebuilt = bridge.read_crop(receiver,header(),'right',1)
            self.assertEqual(rebuilt.shape,(480,640))
            self.assertEqual(rebuilt[260,177],211)
            self.assertEqual(np.count_nonzero(rebuilt),1)

    def test_invalid_length_is_rejected_before_payload_read(self):
        sender, receiver = socket.socketpair()
        with sender, receiver:
            receiver.settimeout(.5)
            sender.sendall(struct.pack('<I',2**24))
            with self.assertRaises(ValueError):
                bridge.read_crop(receiver,header(),'left',4)

    def test_rejects_wrong_hash_before_opening_onnx(self):
        with tempfile.TemporaryDirectory() as folder:
            model = Path(folder)/'wrong.onnx'
            model.write_bytes(b'test only')
            with mock.patch.object(bridge.cv2.dnn,'readNetFromONNX') as read_model:
                with self.assertRaisesRegex(ValueError,'SHA256'):
                    bridge.load_model(model)
                read_model.assert_not_called()


if __name__=='__main__':
    unittest.main()
