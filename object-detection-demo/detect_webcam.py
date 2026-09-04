"""
Standalone webcam object detection demo (Ultralytics).

- Opens the default webcam
- Loads active detector weights from weights/best.pt (currently YOLO26n)
- Draws bounding boxes + confidence
- Overlays FPS
- Prints detected objects to the terminal each frame
"""

from __future__ import annotations

import time
from pathlib import Path

import cv2
from ultralytics import YOLO

# Same runtime weights as tcp_server.py (YOLO26n lighter detector).
WEIGHTS_PATH = Path(__file__).resolve().parent / "weights" / "best.pt"


def main() -> None:
    # 1) Load active custom detector (YOLO26n find-lighter weights).
    if WEIGHTS_PATH.is_file():
        print(f"[info] Loading detector: {WEIGHTS_PATH}")
        model = YOLO(str(WEIGHTS_PATH))
        print(f"[info] Model names={model.names}")
    else:
        print(
            f"[warn] {WEIGHTS_PATH} not found; falling back to yolov8n.pt (COCO classes)."
        )
        model = YOLO("yolov8n.pt")

    # 2) Open default webcam (index 0). Change to 1/2 if you have multiple cameras.
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        raise RuntimeError(
            "Could not open webcam (index 0). "
            "Check that a camera is connected and not used by another app."
        )

    # Optional: prefer a reasonable capture size for smoother demo FPS.
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)

    window_name = "YOLOv8 Webcam Detection (press q to quit)"
    prev_time = time.perf_counter()
    fps = 0.0

    print("[info] Webcam opened. Press 'q' in the video window to quit.")

    try:
        while True:
            ok, frame = cap.read()
            if not ok:
                print("[warn] Failed to read frame from webcam; stopping.")
                break

            # 3) Run detection on the current frame.
            #    verbose=False keeps Ultralytics from spamming its own logs.
            results = model(frame, verbose=False)
            result = results[0]

            # 4) Draw boxes, class names, and confidence on a copy of the frame.
            annotated = result.plot()

            # 5) Update FPS from wall-clock time between frames.
            now = time.perf_counter()
            dt = now - prev_time
            prev_time = now
            if dt > 0:
                fps = 1.0 / dt

            cv2.putText(
                annotated,
                f"FPS: {fps:.1f}",
                (12, 32),
                cv2.FONT_HERSHEY_SIMPLEX,
                1.0,
                (0, 255, 0),
                2,
                cv2.LINE_AA,
            )

            # 6) Print every detection for this frame to the terminal.
            names = result.names
            boxes = result.boxes
            if boxes is not None and len(boxes) > 0:
                detections = []
                for box in boxes:
                    cls_id = int(box.cls[0].item())
                    conf = float(box.conf[0].item())
                    label = names.get(cls_id, str(cls_id))
                    detections.append(f"{label} {conf:.2f}")
                print(f"[detect] {', '.join(detections)}")
            else:
                print("[detect] (none)")

            # 7) Show the annotated frame.
            cv2.imshow(window_name, annotated)

            # 8) Quit when user presses 'q'.
            if cv2.waitKey(1) & 0xFF == ord("q"):
                print("[info] Quit requested.")
                break
    finally:
        cap.release()
        cv2.destroyAllWindows()
        print("[info] Webcam released. Bye.")


if __name__ == "__main__":
    main()
