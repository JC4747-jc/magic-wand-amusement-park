import sys
import unittest
from pathlib import Path
from types import SimpleNamespace

import numpy as np
import pose_bridge
from pose_diagnostics import measure_stage, rejection_code

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'Tools'))
from analyze_pose_diagnostics import summarize


class PoseDiagnosticsTests(unittest.TestCase):
    def test_failed_stage_preserves_exception_and_records_time(self):
        diagnostics = {}
        error = ValueError('Require exactly one lighter, detected 0')
        with self.assertRaises(ValueError) as caught:
            with measure_stage(diagnostics, 'detection_and_segmentation'):
                raise error
        self.assertIs(caught.exception, error)
        self.assertEqual(diagnostics['last_stage'], 'detection_and_segmentation')
        self.assertGreaterEqual(diagnostics['timings_ms']['detection_and_segmentation'], 0)
        self.assertEqual(rejection_code(str(error)), 'NO_TARGET')

    def test_process_failure_retains_stage_without_producing_rotation(self):
        image = np.zeros((20,20,3), dtype=np.uint8)
        depth = np.zeros((20,20), dtype=np.float32)
        stereo = SimpleNamespace(rectify=lambda *args: (image, depth), compute=lambda *args: depth)
        models = SimpleNamespace(segment=lambda rgb: np.ones((20,20), dtype=bool))
        diagnostics = {}
        with self.assertRaisesRegex(ValueError, 'Insufficient foreground depth'):
            pose_bridge.process({}, image, depth, stereo, models, diagnostics=diagnostics)
        self.assertEqual(diagnostics['last_stage'], 'foreground_depth')
        self.assertNotIn('f2g', diagnostics['timings_ms'])

    def test_legacy_and_new_replies_remain_reportable(self):
        report = dict(rows=[
            dict(pose=dict(reply=dict(valid=False, reason='ValueError: Require exactly one lighter, detected 0'))),
            dict(pose=dict(roundtrip_ms=420, reply=dict(valid=True, reason='OK', reason_code='OK',
                diagnostics=dict(timings_ms=dict(f2g=100))))),
        ])
        result = summarize(report)
        self.assertEqual(result['reason_counts'], dict(NO_TARGET=1, OK=1))
        self.assertEqual(result['frames_with_stage_timings'], 1)
        self.assertIsNone(result['unity_decisions'])
        self.assertEqual(summarize(report, '[PoseResult] frame=1 decision=STALE_RESULT')['unity_decisions'],
                         dict(STALE_RESULT=1))


if __name__ == '__main__':
    unittest.main()
