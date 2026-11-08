import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import detector_config as config
import pose_bridge
import tcp_server


class DetectorConfigTests(unittest.TestCase):
    def test_normal_entry_points_share_defaults(self):
        with patch.object(sys, 'argv', ['tcp_server.py']):
            position = tcp_server.parse_args()
        with patch.object(sys, 'argv', ['pose_bridge.py', '--record-only']), patch.object(pose_bridge, 'serve') as serve:
            pose_bridge.main()
        pose = serve.call_args.args[0]
        self.assertEqual(pose.yolo, tcp_server.WEIGHTS_PATH)
        for key in ('conf', 'imgsz', 'iou'):
            self.assertEqual(getattr(position, key), getattr(pose, key))

    def test_missing_weights_fail_without_loading_fallback(self):
        with tempfile.TemporaryDirectory() as folder, patch('ultralytics.YOLO') as load:
            with self.assertRaises(FileNotFoundError):
                config.load_detector(Path(folder)/'missing.pt', conf=.399, imgsz=640, iou=.7)
            load.assert_not_called()

    def test_class_and_content_identity_validation(self):
        with tempfile.TemporaryDirectory() as folder:
            weights = Path(folder)/'model.pt'
            weights.write_bytes(b'test detector weights')
            with patch('ultralytics.YOLO', return_value=SimpleNamespace(names={0:'person'})):
                with self.assertRaisesRegex(ValueError, 'no lighter class'):
                    config.load_detector(weights, conf=.399, imgsz=640, iou=.7)
            with patch('ultralytics.YOLO', side_effect=lambda _: SimpleNamespace(names={0:'Lighter'})):
                first = config.load_detector(weights, conf=.399, imgsz=640, iou=.7)
                weights.write_bytes(b'replaced detector weights')
                second = config.load_detector(weights, conf=.399, imgsz=640, iou=.7)
                self.assertNotEqual(first.magicmr_detector_identity, second.magicmr_detector_identity)


if __name__ == '__main__':
    unittest.main()
