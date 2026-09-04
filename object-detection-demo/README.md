# YOLOv8 Webcam Object Detection Demo

Standalone Python demo (not integrated with Unity).

## Setup

```bash
cd object-detection-demo
python -m venv .venv

# Windows
.venv\Scripts\activate

# macOS / Linux
# source .venv/bin/activate

pip install -r requirements.txt
```

## Run

```bash
python detect_webcam.py
```

- Loads custom weights from `weights/best.pt` (single class: `Lighter`).
- A window shows live detections (boxes + confidence + FPS).
- The terminal prints detected objects every frame.
- Press `q` in the video window to quit.

If the wrong camera opens, edit `cv2.VideoCapture(0)` in `detect_webcam.py` to `1` or `2`.

## Train (Lighter dataset)

```bash
# data.yaml paths were fixed to train/images, valid/images, test/images
python -c "from ultralytics import YOLO; YOLO('yolov8n.pt').train(data='../Lighter/data.yaml', epochs=50, imgsz=640, batch=8, device='cpu', project='runs', name='lighter', exist_ok=True, workers=0)"
# best weights copied to weights/best.pt
```
