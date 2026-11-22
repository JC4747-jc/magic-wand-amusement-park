"""Session-local v2 routing. Keep the main model plus at most one auxiliary model."""
from collections import OrderedDict
from dataclasses import replace
import gc
import json
from pathlib import Path
import time

from discovery import DiscoveryPool, detection_limit, inference_limit, filter_candidates
from model_artifact import verify_artifact
from scene_protocol import InstanceTracker, load_profile, native_class_filter


# Park actors stop tracking after 0.65 s. Keep an overlapping detector ID
# long enough for a brief dropout and the next fresh observation.
PARK_TRACK_REACQUIRE_SECONDS = 1.5


class SceneRouter:
    def __init__(self, main_model, main_path, config_path, predict, decode, config, device, loader=None):
        self.main_model, self.main_path = main_model, Path(main_path).resolve()
        self.config_path = Path(config_path)
        self.base = self.config_path.parent
        self.predict, self.decode, self.config, self.device = predict, decode, config, device
        self.loader = loader or self._load
        self.models = OrderedDict()
        self.trackers = {}
        self.pool = None

    @staticmethod
    def _load(path):
        from ultralytics import YOLO
        return YOLO(str(path))

    @staticmethod
    def _signature(path):
        stat = path.stat()
        metadata = path.parent / 'model.json'
        sidecar = metadata.stat() if metadata.is_file() else None
        return stat.st_size, stat.st_mtime_ns, (sidecar.st_size, sidecar.st_mtime_ns) if sidecar else None

    def _model(self, path, profile):
        path = Path(path).resolve()
        if not path.is_file():
            raise ValueError('SCENE_NOT_CONFIGURED: missing weights ' + str(path))
        signature = self._signature(path)
        key = json.dumps(profile, sort_keys=True)
        if path in self.models:
            entry = self.models[path]
            if signature != entry['signature']:
                raise ValueError('MODEL_CHANGED_RESTART_REQUIRED: ' + str(path))
        else:
            # Evict before loading, keeping only the externally owned main model
            # and one additional predictor resident. No automatic weight downloads.
            for previous in list(self.models) if path != self.main_path else ():
                if previous != self.main_path:
                    del self.models[previous]
                    gc.collect()
                    if str(self.device) != 'cpu':
                        import torch
                        if torch.cuda.is_available():
                            torch.cuda.empty_cache()
            checked, digest = verify_artifact(path, profile)
            model = self.main_model if path == self.main_path else self.loader(path)
            entry = dict(model=model, digest=digest, signature=signature, policies={key: checked})
            self.models[path] = entry
        if key not in entry['policies']:
            checked, _ = verify_artifact(path, profile, loaded_hash=entry['digest'])
            entry['policies'][key] = checked
        self.models.move_to_end(path)
        return entry['model'], entry['policies'][key], entry['digest']

    def infer(self, frame):
        scene = frame.scene
        try:
            if scene == 'discovery':
                if self.pool is None:
                    self.pool = DiscoveryPool(self.config_path, self.base)
                source = self.pool.next_source()
                provisional = dict(allowed=list(source['allowed']), aliases=source['aliases'], validation='DISCOVERY')
                model, _, digest = self._model(source['path'], provisional)
                profile = self.pool.profile(source, model.names)
                thresholds = {label: value if value is not None else self.config.conf for label, value in profile['class_confidence'].items()}
                limit = detection_limit(profile['max_det'])
                config = replace(self.config, conf=min(thresholds.values()), max_det=inference_limit(thresholds, limit))
                validation = '; '.join(f'{s}:{v}' for s, v in sorted(source['validation'].items()))
                tracker = source['tracker']
                path = source['path']
            else:
                profile = load_profile(scene, self.config_path)
                path = (self.base / profile['weights']).resolve()
                model, profile, digest = self._model(path, profile)
                config = replace(self.config, conf=profile.get('confidence', self.config.conf), max_det=detection_limit(profile.get('max_det', 8)))
                validation = profile['validation']
                tracker = self.trackers.get(scene)
                if tracker is None:
                    tracker = self.trackers[scene] = InstanceTracker(ttl=PARK_TRACK_REACQUIRE_SECONDS)
            classes = native_class_filter(model.names, profile)
            result = self.predict(model, frame.image, self.device, config, classes)[0]
            detections = []
            for detection in self.decode(result):
                label = profile.get('aliases', {}).get(detection['class'].lower(), detection['class'])
                if label not in profile['allowed']:
                    continue
                detection.update({'class': label, 'class_name': label, 'bbox': [detection[k] for k in ('x1', 'y1', 'x2', 'y2')]})
                detections.append(detection)
            if scene == 'discovery':
                detections = filter_candidates(detections, thresholds, limit)
            detections = tracker.assign(detections, frame.frame_id, time.monotonic())
            for detection in detections:
                detection['target_scene'] = profile['routes'][detection['class']] if scene == 'discovery' else scene
            return dict(scene=scene, status='OK', detections=detections, weights=str(path), weights_sha256=digest,
                        model_validation=validation, discovery_model_budget=1, resident_model_limit=2,
                        pose_available=False, pose_model_ready=False), result
        except (ValueError, OSError) as error:
            return dict(scene=scene, status=str(error), detections=[], pose_available=False, pose_model_ready=False), None
