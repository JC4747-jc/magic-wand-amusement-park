"""Optional inference orientation; public boxes always use original JPEG edges."""
import numpy as np


def inference_image(image, rotation):
    if rotation == 0:
        return image
    if rotation == 180:
        return np.ascontiguousarray(image[::-1, ::-1])
    raise ValueError('Only 0 or 180 degrees are supported')


def original_detections(detections, width, height, rotation):
    if rotation not in (0, 180):
        raise ValueError('Only 0 or 180 degrees are supported')
    if rotation == 0:
        return detections
    restored = []
    for detection in detections:
        d = dict(detection)
        x1, y1, x2, y2 = (d[k] for k in ('x1', 'y1', 'x2', 'y2'))
        d.update(x1=width-x2, y1=height-y2, x2=width-x1, y2=height-y1)
        d['bbox'] = [d[k] for k in ('x1', 'y1', 'x2', 'y2')]
        d['centerX'] = (d['x1']+d['x2'])/2
        d['centerY'] = (d['y1']+d['y2'])/2
        restored.append(d)
    return restored
