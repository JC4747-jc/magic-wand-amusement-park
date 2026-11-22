"""Dependency-free protocol policy. IDs are session-local LEFT-image tracks, not stereo IDs."""
import json
import uuid
import struct
from pathlib import Path

SCENES = ('lighter','tree','bugs','statue','room','plant','pet','discovery')

def native_class_filter(names, profile):
    """Restrict inference before max_det, rather than dropping unrelated top boxes later."""
    items=names.items() if isinstance(names,dict) else enumerate(names)
    items=list(items); aliases=profile.get('aliases',{}); allowed=set(profile['allowed'])
    selected=[int(i) for i,name in items if aliases.get(str(name).lower(),str(name)) in allowed]
    if not selected:raise ValueError('SCENE_CLASS_MISMATCH: weight classes do not match scene aliases')
    return None if len(selected)==len(items) else selected

def read_packet(read_exact):
    if read_exact(4) != b'PICF': raise ValueError('Bad PICF magic')
    version=read_exact(1)[0]
    if version not in (1,2): raise ValueError('Unsupported PICF version')
    scene_id=read_exact(1)[0] if version==2 else 0
    if scene_id>=len(SCENES): raise ValueError('Unknown scene ID')
    frame_id,timestamp,width,height,length=struct.unpack('<IqIII',read_exact(24))
    if not 0<length<=20_000_000 or not 0<width<=8192 or not 0<height<=8192:
        raise ValueError('Invalid PICF dimensions or length')
    return frame_id,timestamp,width,height,read_exact(length),SCENES[scene_id] if version==2 else None

def load_profile(name, config=None):
    profiles = json.loads(Path(config or Path(__file__).with_name('scene_profiles.json')).read_text())
    if name not in profiles:
        raise ValueError('SCENE_NOT_CONFIGURED: ' + name)
    result = profiles[name]
    if not result.get('weights'):
        raise ValueError('SCENE_NOT_CONFIGURED: validated weights required for ' + name)
    confidence = result.get('confidence')
    if confidence is not None and (isinstance(confidence, bool) or not isinstance(confidence, (int, float)) or not 0 <= confidence <= 1):
        raise ValueError('Invalid scene confidence: ' + name)
    # Missing metadata must not promote a newly configured scene to the historical
    # lighter baseline. Both direct inference and discovery use this profile.
    if 'validation' not in result:
        result['validation'] = 'MAIN_BASELINE' if name == 'lighter' else 'UNVALIDATED'
    else:
        status = result['validation']
        # Explicit null/blank/non-text values are not the legacy omitted field.
        # Normalize before TCP serialization and discovery status aggregation.
        result['validation'] = status.strip() if isinstance(status, str) and status.strip() else 'UNVALIDATED'
    return result

def iou(a, b):
    intersection = max(0, min(a['x2'], b['x2'])-max(a['x1'], b['x1'])) * max(0, min(a['y2'], b['y2'])-max(a['y1'], b['y1']))
    area = lambda x: max(0, x['x2']-x['x1'])*max(0, x['y2']-x['y1'])
    return intersection / max(1e-9, area(a)+area(b)-intersection)

class InstanceTracker:
    def __init__(self, ttl=0.6):
        self.session = uuid.uuid4().hex[:12]
        self.ttl = ttl
        self.tracks = {}
        self.serial = 0
        self.last_frame = -1

    def assign(self, detections, frame_id, now):
        if frame_id <= self.last_frame:
            return []
        self.last_frame = frame_id
        self.tracks = {k:v for k,v in self.tracks.items() if now-v[1] <= self.ttl}
        edges = sorted([(iou(d,t[0]), n,k) for n,d in enumerate(detections)
                        for k,t in self.tracks.items() if d['class']==t[0]['class']], reverse=True)
        # Crossing/overlapping same-class objects cannot be resolved by IoU alone.
        # Break identity and require re-selection instead of silently stealing a lock.
        ambiguous=set()
        for n in range(len(detections)):
            scores=[e[0] for e in edges if e[1]==n and e[0]>=.25]
            if len(scores)>1 and scores[0]-scores[1]<.1: ambiguous.add(n)
        for k in self.tracks:
            candidates=[e for e in edges if e[2]==k and e[0]>=.25]
            if len(candidates)>1 and candidates[0][0]-candidates[1][0]<.1:
                ambiguous.update(e[1] for e in candidates)
        assignments, used = {}, set()
        for score,n,k in edges:
            if score >= 0.25 and n not in ambiguous and n not in assignments and k not in used:
                assignments[n]=k; used.add(k)
        for n,d in enumerate(detections):
            if n not in assignments:
                self.serial += 1
                assignments[n] = f'{self.session}:left:{self.serial}'
            d['instance_id'] = assignments[n]
            d['track_scope'] = 'session:left:temporal_iou'
            self.tracks[assignments[n]] = (dict(d), now)
        return detections
