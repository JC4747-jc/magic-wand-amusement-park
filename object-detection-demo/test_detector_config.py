import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock, patch

import numpy as np

import detector_config as config
import pose_bridge
import tcp_server


class DetectorConfigTests(unittest.TestCase):
    def test_position_candidates_keep_shared_model_and_pose_defaults(self):
        with patch.object(sys, 'argv', ['tcp_server.py']):
            position = tcp_server.parse_args()
        with patch.object(sys, 'argv', ['pose_bridge.py', '--record-only']), patch.object(pose_bridge, 'serve') as serve:
            pose_bridge.main()
        pose = serve.call_args.args[0]
        self.assertEqual(pose.yolo, tcp_server.WEIGHTS_PATH)
        for key in ('imgsz', 'iou'):
            self.assertEqual(getattr(position, key), getattr(pose, key))
        self.assertEqual(position.conf, .28)
        self.assertEqual(position.max_det, 5)
        self.assertEqual(pose.conf, .399)
        self.assertEqual(config.DEFAULT_CONF, .399)

    def test_candidate_settings_reach_warmup_and_live_predict_with_overrides(self):
        for arguments, expected_conf, expected_max in [([], .28, 5),
                (['--conf', '.399', '--max-det', '1'], .399, 1)]:
            with self.subTest(arguments=arguments), patch.object(sys, 'argv', ['tcp_server.py'] + arguments):
                args = tcp_server.parse_args()
                settings = tcp_server.InferenceConfig('cpu', False, args.imgsz,
                    args.conf, args.iou, args.max_det)
                model = SimpleNamespace(predict=Mock(return_value=[]), predictor=None)
                tcp_server.warmup_model(model, settings, 'cpu')
                tcp_server.run_yolo_predict(model, np.zeros((48, 64, 3), np.uint8), 'cpu', settings)
                self.assertEqual(model.predict.call_count, 2)
                for call in model.predict.call_args_list:
                    self.assertEqual(call.kwargs['conf'], expected_conf)
                    self.assertEqual(call.kwargs['max_det'], expected_max)

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
