"""Shared detector defaults and startup validation for position and pose services."""
import hashlib
from pathlib import Path


WEIGHTS_PATH = Path(__file__).resolve().parent / "weights/best_yolo26n_assgn2_v6_50e.pt"
DEFAULT_CONF = 0.399
DEFAULT_IMGSZ = 640
DEFAULT_IOU = 0.7


def load_detector(weights, *, conf, imgsz, iou):
    weights = Path(weights).resolve()
    if not weights.is_file():
        raise FileNotFoundError(f"Configured lighter detector is missing: {weights}")
    if not 0 <= conf <= 1 or not 0 <= iou <= 1 or imgsz <= 0:
        raise ValueError("Detector requires conf and iou between 0 and 1 and positive imgsz")

    from ultralytics import YOLO
    model = YOLO(str(weights))
    names = model.names.values() if isinstance(model.names, dict) else model.names
    if "lighter" not in [name.lower() for name in names]:
        raise ValueError(f"Detector {weights.name} has no lighter class: {model.names}")
    digest = hashlib.sha256()
    with weights.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    identity = dict(sha256=digest.hexdigest(), conf=conf, imgsz=imgsz, iou=iou)
    print(f"[Detector] weights={weights} sha256={identity['sha256']} "
          f"conf={conf} imgsz={imgsz} iou={iou} names={model.names}", flush=True)
    model.magicmr_detector_identity = identity
    return model
