"""Smoke-test the custom Lighter weights (test images + short webcam run)."""

from __future__ import annotations

from pathlib import Path

import cv2
from ultralytics import YOLO

WEIGHTS = Path(__file__).resolve().parent / "weights" / "best.pt"
TEST_DIR = Path(__file__).resolve().parent.parent / "Lighter" / "test" / "images"


def main() -> None:
    model = YOLO(str(WEIGHTS))
    print("model.names =", model.names)
    if list(model.names.values()) != ["Lighter"]:
        raise SystemExit(f"Expected only ['Lighter'], got {model.names}")

    images = sorted(TEST_DIR.glob("*"))
    print(f"test images: {len(images)}")
    non_lighter: set[str] = set()
    lighter_hits = 0
    for img_path in images:
        result = model(str(img_path), verbose=False)[0]
        if result.boxes is None or len(result.boxes) == 0:
            print(f"  {img_path.name}: (none)")
            continue
        for box in result.boxes:
            label = result.names[int(box.cls[0].item())]
            conf = float(box.conf[0].item())
            if label == "Lighter":
                lighter_hits += 1
            else:
                non_lighter.add(label)
            print(f"  {img_path.name}: {label} {conf:.2f}")
    print("lighter detections:", lighter_hits)
    print("non-Lighter labels seen:", non_lighter or "(none)")

    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("WEBCAM_OPEN=False")
        return

    print("WEBCAM_OPEN=True")
    seen: set[str] = set()
    for i in range(20):
        ok, frame = cap.read()
        if not ok:
            print("frame read failed at", i)
            break
        result = model(frame, verbose=False)[0]
        if result.boxes is not None and len(result.boxes):
            labels = []
            for box in result.boxes:
                label = result.names[int(box.cls[0].item())]
                seen.add(label)
                labels.append(f"{label} {float(box.conf[0]):.2f}")
            print(f"[webcam {i}] {', '.join(labels)}")
        else:
            print(f"[webcam {i}] (none)")
    cap.release()
    print("webcam labels seen:", seen or "(none)")
    unexpected = seen - {"Lighter"}
    print("UNEXPECTED_CLASSES=", unexpected or "(none)")
    if unexpected:
        raise SystemExit(f"Model predicted unexpected classes: {unexpected}")


if __name__ == "__main__":
    main()
