"""Render bounded headset evidence without changing detector/matcher thresholds.

Input is stereo-diagnostics pulled from the position repair app's persistentDataPath.
PGM row zero and JSON boxes use the native stereo coordinates, before JPEG encoding.
"""
import argparse
from collections import Counter
import json
from pathlib import Path

import cv2
import numpy as np


def inspect(directory, output):
    records = []
    for path in sorted(directory.glob('*.json')):
        data = json.loads(path.read_text(encoding='utf-8-sig'))
        if 'session' in data:
            records.append((path, data))
    if not records:
        raise SystemExit('No stereo evidence JSON files found')
    # Slots are reused on each launch; do not mix a previous launch's unused slots.
    session = max(data['session'] for _, data in records)
    records = [(path, data) for path, data in records if data['session'] == session]
    output.mkdir(parents=True, exist_ok=True)
    summary = {'session': session, 'frames': len(records), 'states': {}, 'sample_rejections': {}, 'measurements': []}
    states, rejections = Counter(), Counter()
    for path, data in records:
        states[data['status']] += 1
        left = cv2.imread(str(path.with_name(path.stem + '-left.pgm')), cv2.IMREAD_GRAYSCALE)
        right = cv2.imread(str(path.with_name(path.stem + '-right.pgm')), cv2.IMREAD_GRAYSCALE)
        if left is None or right is None or left.shape != (480, 640) or right.shape != (480, 640):
            raise SystemExit(f'Missing or invalid stereo pair: {path}')
        canvas = cv2.cvtColor(np.concatenate((left, right), axis=1), cv2.COLOR_GRAY2BGR)
        if data['hasDetection']:
            cv2.rectangle(canvas, (round(data['x1']), round(data['y1'])),
                          (round(data['x2']), round(data['y2'])), (255, 255, 0), 2)
        for i, sample in enumerate(data.get('samples') or []):
            color = (0, 255, 0) if sample['inlier'] else (0, 165, 255) if sample['valid'] else (0, 0, 255)
            point = (sample['x'], sample['y'])
            cv2.circle(canvas, point, 3, color, -1)
            cv2.putText(canvas, str(i), (point[0] + 4, point[1]), cv2.FONT_HERSHEY_SIMPLEX, .3, color, 1)
            if sample['valid']:
                other = (round(640 + sample['x'] - sample['disparity']), sample['y'] + sample['verticalOffset'])
                cv2.circle(canvas, other, 3, color, -1)
                cv2.putText(canvas, str(i), (other[0] + 4, other[1]), cv2.FONT_HERSHEY_SIMPLEX, .3, color, 1)
            else:
                rejections[sample['reason']] += 1
        estimate = data.get('estimate') or {}
        title = (f"frame {data['frameId']} {data['status']} conf={data['confidence']:.2f} "
                 f"age={data['captureAgeMs']:.0f}ms gap={data['pairGapMs']:.2f}ms")
        detail = (f"{estimate.get('reason', '')} matches={estimate.get('matchedSamples', 0)}/9 "
                  f"inliers={estimate.get('acceptedSamples', 0)} d={estimate.get('disparityPixels', 0):.2f}px "
                  f"z={estimate.get('depthMeters', 0):.3f}m")
        canvas = cv2.copyMakeBorder(canvas, 60, 0, 0, 0, cv2.BORDER_CONSTANT)
        cv2.putText(canvas, title, (10, 23), cv2.FONT_HERSHEY_SIMPLEX, .55, (255, 255, 255), 1)
        cv2.putText(canvas, detail, (10, 47), cv2.FONT_HERSHEY_SIMPLEX, .55, (255, 255, 255), 1)
        destination = output / f'{path.stem}-frame-{data["frameId"]}.png'
        if not cv2.imwrite(str(destination), canvas):
            raise SystemExit(f'Could not write {destination}')
        summary['measurements'].append({key: data[key] for key in
            ('frameId', 'status', 'confidence', 'captureAgeMs', 'pairGapMs', 'x1', 'y1', 'x2', 'y2', 'estimate')})
    summary['states'] = dict(states)
    summary['sample_rejections'] = dict(rejections)
    (output / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    print(json.dumps({key: value for key, value in summary.items() if key != 'measurements'}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    inspect(args.directory, args.output)
