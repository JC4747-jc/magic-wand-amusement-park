"""One model per frame discovery budget, using configured usable model paths only."""
import json
import hashlib
from pathlib import Path
from scene_protocol import InstanceTracker, load_profile
from model_artifact import verify_artifact

ROUTES={'lighter':'lighter','tree':'tree','cockroach':'bugs','spider':'bugs',
        'statue':'statue','sculpture':'statue','bronze beast':'statue','bronzebeast':'statue','potted plant':'plant','pottedplant':'plant',
        'cat':'pet','dog':'pet'}


def detection_limit(value):
    if isinstance(value, bool) or not isinstance(value, int) or value < 1:
        raise ValueError('Discovery max_det must be a positive integer')
    return value


def inference_limit(thresholds, output_limit):
    """Leave room for per-class threshold filtering before the output budget."""
    output_limit = detection_limit(output_limit)
    return max(300, output_limit) if len(set(thresholds.values())) > 1 else output_limit


def filter_candidates(detections, thresholds, output_limit):
    return [d for d in detections if d['confidence'] >= thresholds[d['class']]][:detection_limit(output_limit)]


class DiscoveryPool:
    def __init__(self, config, base):
        profiles=json.loads(Path(config).read_text(encoding='utf-8'))
        self.sources=[];self.cursor=0
        for scene,p in profiles.items():
            if scene=='room' or not p.get('weights'):continue
            p=load_profile(scene,config)
            path=(Path(base)/p['weights']).resolve()
            if not path.is_file():continue
            p, digest = verify_artifact(path, p)
            source=next((s for s in self.sources if s['path']==path),None)
            if source is None:
                source={'path':path,'sha256':digest,'aliases':{},'allowed':set(),'scenes':set(),'validation':{},'confidence':{},'limits':{},'tracker':InstanceTracker(ttl=3.0)}
                self.sources.append(source)
            source['aliases'].update(p.get('aliases',{}));source['scenes'].add(scene)
            source['allowed'].update(p.get('allowed',p.get('aliases',{}).values()))
            source['validation'][scene]=p.get('validation','MAIN_BASELINE')
            source['confidence'][scene]=p.get('confidence')
            # Direct lighter tracking historically uses one box, discovery uses eight.
            # Preserve that distinction while allowing an explicit discovery budget.
            source['limits'][scene]=detection_limit(p.get('discovery_max_det',max(8,detection_limit(p.get('max_det',8)))))
        if not self.sources:raise ValueError('DISCOVERY_NOT_CONFIGURED')

    def next_source(self):
        source=self.sources[self.cursor%len(self.sources)];self.cursor+=1
        return source

    @staticmethod
    def profile(source, names):
        aliases={};allowed=[];routes={}
        for name in names.values():
            label=source['aliases'].get(str(name).lower(),str(name))
            scene=ROUTES.get(str(label).lower()) or ROUTES.get(str(name).lower())
            if scene in source['scenes'] and label in source.get('allowed',{label}):
                aliases[str(name).lower()]=label;allowed.append(label);routes[label]=scene
        if not allowed:raise ValueError('DISCOVERY_CLASS_MISMATCH')
        return {'aliases':aliases,'allowed':allowed,'routes':routes,
                'max_det':max(source.get('limits',{}).get(scene,8) for scene in routes.values()),
                'class_confidence':{label:source['confidence'].get(scene) for label,scene in routes.items()}}
