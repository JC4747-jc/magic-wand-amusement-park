"""Summarize saved pose replays and optional Unity logs without a GPU or headset."""
import argparse
from collections import Counter, defaultdict
import json
import math
from pathlib import Path
import re
import statistics
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'object-detection-demo'))
from pose_diagnostics import rejection_code


def timing_summary(values):
    values = sorted(value for value in values if isinstance(value, (float, int)) and math.isfinite(value) and value >= 0)
    if not values:
        return None
    return dict(samples=len(values), median_ms=statistics.median(values),
                p95_ms=values[math.ceil(len(values)*.95)-1], max_ms=values[-1])


def summarize(report, unity_log=None):
    reasons = Counter()
    stages = defaultdict(list)
    roundtrips = []
    valid_roundtrips = []
    valid = 0
    diagnostic_frames = 0
    rows = report.get('rows', [])
    for row in rows:
        pose = row.get('pose', {})
        reply = pose.get('reply', {})
        reasons[reply.get('reason_code') or rejection_code(reply.get('reason', ''))] += 1
        valid += reply.get('valid') is True
        if 'roundtrip_ms' in pose:
            roundtrips.append(pose['roundtrip_ms'])
            if reply.get('valid') is True:
                valid_roundtrips.append(pose['roundtrip_ms'])
        timings = reply.get('diagnostics', {}).get('timings_ms', {})
        diagnostic_frames += bool(timings)
        for name, elapsed in timings.items():
            stages[name].append(elapsed)
    return dict(frames=len(rows), valid_pose_frames=valid, reason_counts=dict(reasons),
                frames_with_stage_timings=diagnostic_frames,
                pc_roundtrip=timing_summary(roundtrips),
                pc_valid_pose_roundtrip=timing_summary(valid_roundtrips),
                stage_wall_times={name: timing_summary(values) for name, values in stages.items()},
                unity_decisions=None if unity_log is None else dict(Counter(
                    re.findall(r'\[PoseResult\][^\r\n]*\bdecision=([A-Z_]+)', unity_log))),
                scope='Saved replay diagnostics. PC roundtrip excludes headset capture and USB; stage times include host overhead.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('report', type=Path)
    parser.add_argument('--unity-log', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = json.loads(args.report.read_text(encoding='utf-8-sig'))
    unity_log = args.unity_log.read_text(encoding='utf-8-sig', errors='replace') if args.unity_log else None
    text = json.dumps(summarize(report, unity_log), ensure_ascii=False, indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text, encoding='utf-8')
    print(text)


if __name__ == '__main__':
    main()
