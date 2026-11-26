"""Regression checks for offline report math; no detector/GPU required."""
import json
from pathlib import Path
import tempfile
import unittest

from compare_follow_continuity import continuity, read_frames


def row(timestamp, score=None, x=0):
    return dict(timestamp_s=timestamp, width=100, height=100,
                detections=[] if score is None else [dict(confidence=score, xyxy=[x, 0, x+10, 20])])


class ContinuityChecks(unittest.TestCase):
    def test_missing_runs_include_leading_and_trailing_misses(self):
        result = continuity([row(0), row(.1), row(.2, .8), row(.3), row(.6), row(.9)], .28)
        self.assertEqual(result["detected_frames"], 1)
        self.assertEqual([run["frames"] for run in result["no_detection_runs"]], [2, 3])
        self.assertEqual(result["longest_no_detection_run_frames"], 3)
        self.assertAlmostEqual(result["longest_sampled_no_detection_span_s"], .6)

    def test_thresholds_count_candidates_without_claiming_recall(self):
        sample = row(0, .3)
        sample["detections"].append(dict(confidence=.6, xyxy=[20, 0, 30, 20]))
        low = continuity([sample, row(.1, .3)], .28)
        high = continuity([sample, row(.1, .3)], .5)
        self.assertEqual(low["candidate_count"]["maximum"], 2)
        self.assertEqual(low["detected_frames"], 2)
        self.assertEqual(high["detected_frames"], 1)
        self.assertEqual(high["longest_sampled_no_detection_span_s"], 0)

    def test_box_steps_do_not_bridge_missing_frames(self):
        result = continuity([row(0, .9), row(.1, .9, x=10), row(.2), row(.4, .9, x=80)], .28)
        self.assertEqual(result["adjacent_top_box_center_step_image_diagonals"]["count"], 1)
        self.assertAlmostEqual(result["adjacent_top_box_center_step_image_diagonals"]["mean"], .0707106781)
        self.assertAlmostEqual(result["detected_sample_gap_s"]["maximum"], .3)

    def test_empty_and_all_missing(self):
        self.assertIsNone(continuity([], .28)["detected_frame_fraction"])
        result = continuity([row(0), row(1)], .28)
        self.assertEqual(result["longest_no_detection_run_frames"], 2)
        self.assertEqual(result["longest_sampled_no_detection_span_s"], 1)
        self.assertIsNone(result["top_confidence"]["mean"])

    def test_manifest_keeps_frame_identity_and_rejects_duplicate_times(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "frame.jpg").write_bytes(b"placeholder")
            path = root / "frames.jsonl"
            sample = dict(image="frame.jpg", timestamp_s=1, frame_id=42)
            path.write_text(json.dumps(sample), encoding="utf-8")
            self.assertEqual(read_frames(path, None)[0]["frame_id"], 42)
            path.write_text(json.dumps(sample)+"\n"+json.dumps(sample), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "strictly increasing"):
                read_frames(path, None)

    def test_rejects_missing_image_nonfinite_time_and_mixed_sessions(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            path = root / "frames.jsonl"
            sample = dict(image="frame.jpg", timestamp_s=1, session="a")
            path.write_text(json.dumps(sample), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Missing image"):
                read_frames(path, None)
            (root / "frame.jpg").write_bytes(b"placeholder")
            path.write_text(json.dumps(dict(sample, timestamp_s=float("nan"))), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "finite"):
                read_frames(path, None)
            path.write_text(json.dumps(sample)+"\n"+json.dumps(dict(sample, timestamp_s=2, session="b")), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "one recording session"):
                read_frames(path, None)
            selected = read_frames(path, None, "b")
            self.assertEqual(len(selected), 1)
            self.assertEqual(selected[0]["timestamp_s"], 2)


if __name__ == "__main__":
    unittest.main()
