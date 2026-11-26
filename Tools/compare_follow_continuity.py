"""Offline v4/v6 detection continuity comparison on identical recorded frames.

Use --captures DIR for one pose recording containing */header.json and left.png,
or --manifest frames.jsonl with {"image":"relative/path.jpg","timestamp_s":1.23}
per line. Include every captured frame, including misses; do not preselect detections.
Input images must use the same orientation as the TCP detector. --jpeg-quality 90
reproduces its JPEG encoding for raw PNG captures (use 0 for already encoded JPEGs).
No service connections or deployment changes are made.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.metadata
import json
import math
from pathlib import Path
import statistics


ROOT = Path(__file__).resolve().parents[1]
WEIGHTS = ROOT / "object-detection-demo" / "weights"
PROFILES = {"baseline": {"conf": .399, "max_det": 1},
            "candidates": {"conf": .28, "max_det": 5}}


def read_frames(manifest: Path | None, captures: Path | None, session: str | None = None) -> list[dict]:
    frames = []
    if manifest is not None:
        for line in manifest.read_text(encoding="utf-8-sig").splitlines():
            if not line.strip():
                continue
            data = json.loads(line)
            frames.append(dict(image=(manifest.parent / data["image"]).resolve(),
                               timestamp_s=float(data["timestamp_s"]),
                               session=data.get("session", "manifest"),
                               frame_id=data.get("frame_id")))
    else:
        for path in sorted(captures.glob("*/header.json")):
            data = json.loads(path.read_text(encoding="utf-8-sig"))
            frames.append(dict(image=(path.parent / "left.png").resolve(),
                               timestamp_s=data["timestamp_ns"] / 1e9,
                               session=data["session"], frame_id=data["frame_id"]))
        frames.sort(key=lambda frame: frame["timestamp_s"])
    if session is not None:
        frames = [frame for frame in frames if frame["session"] == session]
    if not frames:
        raise ValueError("No frames found")
    if len({frame["session"] for frame in frames}) != 1:
        raise ValueError("Choose one recording session with --session; mixing sessions hides discontinuities")
    previous = -math.inf
    for frame in frames:
        timestamp = frame["timestamp_s"]
        if not math.isfinite(timestamp) or timestamp <= previous:
            raise ValueError("Frame timestamps must be finite and strictly increasing")
        if not frame["image"].is_file():
            raise ValueError(f"Missing image: {frame['image']}")
        previous = timestamp
    return frames


def distribution(values: list[float]) -> dict:
    if not values:
        return dict(count=0, mean=None, median=None, p95=None, maximum=None)
    ordered = sorted(values)
    return dict(count=len(values), mean=statistics.mean(values),
                median=statistics.median(values),
                p95=ordered[math.ceil(.95 * len(values)) - 1], maximum=max(values))


def continuity(rows: list[dict], threshold: float) -> dict:
    """Presence of any predicted lighter, not recall or verified identity continuity."""
    present, counts, scores, steps, size_changes, observed_gaps, runs = [], [], [], [], [], [], []
    run_start = None
    previous_box = None
    last_detection_at = None
    for index, row in enumerate(rows):
        candidates = [box for box in row["detections"] if box["confidence"] >= threshold]
        counts.append(len(candidates))
        present.append(bool(candidates))
        if not candidates:
            if run_start is None:
                run_start = index
            previous_box = None
            continue
        if run_start is not None:
            runs.append(dict(start_index=run_start, end_index=index-1, frames=index-run_start,
                             sampled_span_s=rows[index-1]["timestamp_s"]-rows[run_start]["timestamp_s"]))
            run_start = None
        box = max(candidates, key=lambda candidate: candidate["confidence"])
        scores.append(box["confidence"])
        if last_detection_at is not None:
            observed_gaps.append(row["timestamp_s"] - last_detection_at)
        last_detection_at = row["timestamp_s"]
        if previous_box is not None:
            a, b = previous_box["xyxy"], box["xyxy"]
            center_distance = math.hypot((a[0]+a[2]-b[0]-b[2])/2,
                                         (a[1]+a[3]-b[1]-b[3])/2)
            steps.append(center_distance / math.hypot(row["width"], row["height"]))
            size_changes.append(max(abs(math.log((b[2]-b[0])/(a[2]-a[0]))),
                                    abs(math.log((b[3]-b[1])/(a[3]-a[1])))))
        previous_box = box
    if run_start is not None:
        runs.append(dict(start_index=run_start, end_index=len(rows)-1, frames=len(rows)-run_start,
                         sampled_span_s=rows[-1]["timestamp_s"]-rows[run_start]["timestamp_s"]))
    return dict(confidence_threshold=threshold, frames=len(rows), detected_frames=sum(present),
                detected_frame_fraction=sum(present)/len(rows) if rows else None,
                candidate_count=distribution(counts), top_confidence=distribution(scores),
                longest_no_detection_run_frames=max((run["frames"] for run in runs), default=0),
                longest_sampled_no_detection_span_s=max((run["sampled_span_s"] for run in runs), default=0),
                no_detection_runs=runs, detected_sample_gap_s=distribution(observed_gaps),
                adjacent_top_box_center_step_image_diagonals=distribution(steps),
                adjacent_top_box_size_log_change=distribution(size_changes))


def extract_boxes(result) -> list[dict]:
    boxes = []
    if result.boxes is None:
        return boxes
    for box in result.boxes:
        label = str(result.names[int(box.cls[0].item())]).strip().lower()
        # Match the bridge's case-insensitive Lighter class.
        if label != "lighter":
            continue
        confidence = float(box.conf[0].item())
        xyxy = [float(value) for value in box.xyxy[0].tolist()]
        if not all(math.isfinite(value) for value in [confidence, *xyxy]):
            continue
        if xyxy[2] <= xyxy[0] or xyxy[3] <= xyxy[1]:
            continue
        boxes.append(dict(confidence=confidence, xyxy=xyxy))
    return sorted(boxes, key=lambda box: box["confidence"], reverse=True)


def compare(args) -> dict:
    # Keep summary/testing usable without torch or a GPU installation.
    import cv2
    from ultralytics import YOLO

    frames = read_frames(args.manifest, args.captures, args.session)
    if not args.v4.is_file() or not args.v6.is_file():
        raise ValueError("Both local model weights must exist; no weights are downloaded")
    images, image_hashes = [], []
    for frame in frames:
        image = cv2.imread(str(frame["image"]), cv2.IMREAD_COLOR)
        if image is None:
            raise ValueError(f"Unreadable image: {frame['image']}")
        if args.jpeg_quality:
            ok, packed = cv2.imencode(".jpg", image, [cv2.IMWRITE_JPEG_QUALITY, args.jpeg_quality])
            if not ok:
                raise ValueError(f"JPEG encoding failed: {frame['image']}")
            image = cv2.imdecode(packed, cv2.IMREAD_COLOR)
        images.append(image)
        image_hashes.append(hashlib.sha256(image.tobytes()).hexdigest())
    if len({image.shape for image in images}) != 1:
        raise ValueError("All frames must have the same image dimensions")
    args.output.mkdir(parents=True, exist_ok=True)
    report = dict(
        scope="Offline predictions on identical images; no ground truth or Unity stereo/follow replay",
        recording_note=args.recording_note,
        limitations=[
            "Detected-frame fraction is presence of a prediction, not accuracy or recall.",
            "False positives, target switches and actual object motion can affect box-step statistics.",
            "Box steps compare the top-confidence boxes only on adjacent detected input frames.",
            "Missing-run spans cover sampled timestamps only; a single missing sample has span zero.",
            "Sparse/selected recordings cannot establish live continuity or headset freeze duration.",
            "No depth quality, registration, hand occlusion annotation or final position-update rate is evaluated.",
        ],
        input=dict(frames=len(frames), session=frames[0]["session"],
                   duration_s=frames[-1]["timestamp_s"]-frames[0]["timestamp_s"],
                   sample_gap_s=distribution([b["timestamp_s"]-a["timestamp_s"] for a,b in zip(frames, frames[1:])]),
                   jpeg_quality=args.jpeg_quality),
        inference=dict(imgsz=args.imgsz, iou=args.iou, device=args.device, half=False),
        packages={name: importlib.metadata.version(name) for name in ("ultralytics", "torch", "numpy")},
        models={})
    frame_manifest = [dict(**{key: str(value) if isinstance(value, Path) else value for key, value in frame.items()},
                           inference_pixels_sha256=digest) for frame, digest in zip(frames, image_hashes)]
    (args.output / "input-frames.json").write_text(json.dumps(frame_manifest, indent=2), encoding="utf-8")
    for name, weights in (("v4", args.v4), ("v6", args.v6)):
        model = YOLO(str(weights))
        model_report = dict(weights=str(weights.resolve()), sha256=hashlib.sha256(weights.read_bytes()).hexdigest(), profiles={})
        for profile, settings in PROFILES.items():
            rows = []
            for index, (frame, image) in enumerate(zip(frames, images)):
                result = model.predict(image, conf=settings["conf"], max_det=settings["max_det"],
                                       imgsz=args.imgsz, iou=args.iou, device=args.device,
                                       verbose=False)[0]
                rows.append(dict(index=index, frame_id=frame["frame_id"], timestamp_s=frame["timestamp_s"],
                                 width=image.shape[1], height=image.shape[0], detections=extract_boxes(result)))
            filename = f"{name}-{profile}-frames.jsonl"
            (args.output / filename).write_text("".join(json.dumps(row)+"\n" for row in rows), encoding="utf-8")
            model_report["profiles"][profile] = dict(**settings, per_frame_file=filename,
                thresholds=[continuity(rows, threshold) for threshold in sorted({settings["conf"], .399, .5}) if threshold >= settings["conf"]])
            print(f"{name}/{profile}: {sum(bool(row['detections']) for row in rows)}/{len(rows)} frames with predictions", flush=True)
        report["models"][name] = model_report
        del model
    (args.output / "summary.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--captures", type=Path, help="One capture session directory containing */header.json and left.png")
    source.add_argument("--manifest", type=Path, help="JSONL of images and strictly increasing timestamp_s")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--recording-note", required=True, help="Recording date/scenario and sampling limitations")
    parser.add_argument("--session", help="Select one session ID when a directory spans reconnects")
    parser.add_argument("--v4", type=Path, default=WEIGHTS / "best_yolo26n_quad_v4_50e.pt")
    parser.add_argument("--v6", type=Path, default=WEIGHTS / "best_yolo26n_assgn2_v6_50e.pt")
    parser.add_argument("--device", default="cpu", help="Use cpu to avoid competing with a running GPU detector, or explicitly choose 0")
    parser.add_argument("--imgsz", type=int, default=640)
    parser.add_argument("--iou", type=float, default=.7)
    parser.add_argument("--jpeg-quality", type=int, default=90)
    args = parser.parse_args()
    if not 0 <= args.jpeg_quality <= 100 or args.imgsz <= 0 or not 0 <= args.iou <= 1:
        parser.error("jpeg-quality must be 0..100; imgsz positive; iou 0..1")
    compare(args)
    print(args.output / "summary.json")


if __name__ == "__main__":
    main()
