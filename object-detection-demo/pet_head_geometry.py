"""AP-10K facial landmarks and mutually checked stereo patches for Pet heads."""
import math

import cv2
import numpy as np


LANDMARKS = ('left_eye', 'right_eye', 'nose')
SIZE = 256
PADDING = 1.25
MEAN = np.array((123.675, 116.28, 103.53), dtype=np.float32)
STD = np.array((58.395, 57.12, 57.375), dtype=np.float32)
MODEL_SCORE = .50
FACE_EYE_SPAN_PX = 4.0
NOSE_ALONG_EYES = (-.25, 1.25)
NOSE_BELOW_EYES = (.12, 1.5)


def pxr_luminance(bgr):
    data = bgr.astype(np.uint16)
    return ((29*data[:,:,0] + 150*data[:,:,1] + 77*data[:,:,2]) >> 8).astype(np.uint8)


def head_pixels(net, image, bbox):
    x0, y0, x1, y1 = bbox
    side = max(x1-x0, y1-y0)*PADDING
    origin = np.array(((x0+x1-side)*.5, (y0+y1-side)*.5), dtype=np.float32)
    scale = SIZE/side
    projection = np.array([[scale, 0, -origin[0]*scale], [0, scale, -origin[1]*scale]], dtype=np.float32)
    patch = cv2.warpAffine(image, projection, (SIZE, SIZE), flags=cv2.INTER_LINEAR)
    rgb = patch[...,::-1].astype(np.float32)
    net.setInput(((rgb-MEAN)/STD).transpose(2,0,1)[None].copy())
    horizontal, vertical = net.forward(('simcc_x', 'simcc_y'))
    if horizontal.shape != (1,17,512) or vertical.shape != horizontal.shape:
        raise ValueError('Unexpected AP-10K SimCC output dimensions')
    coordinates = np.stack((horizontal[0].argmax(axis=1), vertical[0].argmax(axis=1)), axis=-1)*(.5/scale)+origin
    scores = np.minimum(horizontal[0].max(axis=1), vertical[0].max(axis=1))
    return {name: {'xy': coordinates[index].tolist(), 'score': float(scores[index])} for index,name in enumerate(LANDMARKS)}


def frontal_geometry(view):
    if any(view[key]['score'] < MODEL_SCORE for key in LANDMARKS):
        return False
    start, end = sorted((np.asarray(view['left_eye']['xy']), np.asarray(view['right_eye']['xy'])),
                        key=lambda point: point[0])
    axis = end - start
    length_sq = float(axis @ axis)
    if length_sq < FACE_EYE_SPAN_PX**2:
        return False
    offset = np.asarray(view['nose']['xy']) - start
    along = float(offset @ axis)/length_sq
    below = float(axis[0]*offset[1] - axis[1]*offset[0])/length_sq
    return NOSE_ALONG_EYES[0] <= along <= NOSE_ALONG_EYES[1] and NOSE_BELOW_EYES[0] <= below <= NOSE_BELOW_EYES[1]


def stereo_nose_disparity(left, right, ceiling, scale=None, body_depth=None):
    if not (frontal_geometry(left) and frontal_geometry(right)):
        return None
    offsets = {name: left[name]['xy'][0]-right[name]['xy'][0] for name in LANDMARKS}
    if (not all(2<=offsets[name]<ceiling and abs(left[name]['xy'][1]-right[name]['xy'][1])<=3
                for name in LANDMARKS) or max(offsets.values())-min(offsets.values())>4):
        return None
    nose = offsets['nose']
    if scale and not abs(scale/nose-body_depth)<=.70:
        return None
    return nose


def local_peak(source, target, point, expected):
    x, y = map(round, point)
    tx, ty = map(round, expected)
    if not (4<=x<636 and 3<=y<477 and 7<=tx<633 and 6<=ty<474):
        return None
    template = source[y-3:y+4,x-4:x+5].astype(np.float32)
    template -= template.mean()
    norm = np.linalg.norm(template)
    if norm < 6*math.sqrt(template.size):
        return None
    candidates = []
    for ry in range(max(y-3,ty-3), min(y+3,ty+3)+1):
        for rx in range(tx-3,tx+4):
            patch = target[ry-3:ry+4,rx-4:rx+5].astype(np.float32)
            patch -= patch.mean()
            patch_norm = np.linalg.norm(patch)
            if patch_norm>=6*math.sqrt(patch.size):
                correlation = float(np.sum(template*patch)/(norm*patch_norm))
                candidates.append((correlation,rx,ry))
    if not candidates:
        return None
    score, rx, ry = max(candidates)
    competitor = max((value for value,cx,cy in candidates if max(abs(rx-cx),abs(ry-cy))>1), default=-1.)
    if score<.70 or score-competitor<.04:
        return None
    return rx, ry


def match_face_patches(left, right, images, nose_disparity, ceiling):
    good = {}
    for name in LANDMARKS:
        a, b = left[name]['xy'], right[name]['xy']
        match = local_peak(images[0], images[1], a, b)
        if match is None:
            continue
        reverse = local_peak(images[1], images[0], match, a)
        if reverse is not None and math.dist(reverse,a)<=1.5 and math.dist(match,b)<=3:
            disparity = round(a[0])-match[0]
            if 2<=disparity<ceiling and abs(disparity-nose_disparity)<=4:
                good[name] = list(match)
    return good
