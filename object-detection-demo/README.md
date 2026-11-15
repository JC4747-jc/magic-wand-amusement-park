# PC detection and pose services

`tcp_server.py` receives PICO JPEG frames from Unity on port 5005. It does not open the PC camera. `pose_bridge.py` is the independent experimental pose/recording sidecar on localhost 5006, with PC-only health on 5007. See the root README and `F2G_POSE.md` for device configuration and current validation limits.

## Current deployment

Both services use `detector_config.py`: `weights/best_yolo26n_assgn2_v6_50e.pt`, conf `0.399`, imgsz `640`, iou `0.7`. Missing weights fail startup. Confirm the startup SHA-256 rather than relying on a training output folder.

From the project root, after installing the requirements in an isolated environment:

```powershell
.venv/Scripts/python.exe object-detection-demo/tcp_server.py --headless --host 127.0.0.1 --device cpu
.venv/Scripts/python.exe -m unittest discover -s object-detection-demo -v
.venv/Scripts/python.exe Tools/smoke_position_service.py
```

The CPU command is a functional startup option, not a real-time performance guarantee. GPU deployment requires a compatible CUDA PyTorch environment. Unit tests exercise real local sockets and recording subprocesses with explicit inference doubles; they do not train or establish detection accuracy. The separate smoke command needs port 5005 free (stop the manual server first): it starts its own CPU service, uses actual deployment weights on a blank JPEG across two sessions, and terminates its own process on exit. It does not open a camera or establish accuracy. Logs are written to `.codex-tmp/position-smoke/server.log`. An environment that blocks local TCP must allow those sockets before running the checks.

Use `pose_preflight.py` to enumerate missing live F2G dependencies. `--record-only` does not provide model poses. Do not forward PC health port 5007 through ADB.

## Integrated Park protocol (PICF v2)

The same `tcp_server.py` now accepts legacy PICF v1 and v2 packets on one connection. V2 adds one profile byte after the version: `0=lighter, 1=tree, 2=bugs, 3=statue, 4=room, 5=plant, 6=pet, 7=discovery`. The existing little-endian frame header and JPEG follow unchanged. Replies include the requested scene, validation status, and session-local LEFT-image instance IDs; these IDs do not establish stereo correspondence or a 3D pose.

`--profiles path/to/scene_profiles.json` selects a local configuration. The default preserves the v6 lighter detector. Room, plant and pet reuse the repository's local COCO `yolov8n.pt` and report `PUBLIC_BASELINE_ONLY_PICO_PENDING`; no detection-accuracy claim follows from successful inference. Tree, bugs and statue have no accepted weights and return an empty `SCENE_NOT_CONFIGURED` reply. Rejected archived training runs are not enabled. Missing weights never trigger an implicit download.

Discovery evaluates one configured model per frame in rotation, combines profiles sharing weights, and excludes room furniture from automatic scene selection. Native class filtering happens before the output limit. The service retains the main predictor and at most one auxiliary predictor, releasing an old auxiliary before loading another; CUDA cache release applies when running a CUDA environment. The current workspace environment is CPU-only. Replaced weight files or metadata invalidate a cached predictor and require a service restart.

`Tools/smoke_position_service.py` exercises eleven real CPU requests: v1, three COCO scene profiles, two discovery sources, three explicitly unconfigured scenes, return to v1, and a fresh reconnect starting at frame 1. It uses blank JPEGs and saves bounded replies to `.codex-tmp/position-smoke/replies.json`. It does not measure target accuracy. The full regression suite currently has 51 passing tests, including protocol boundaries, class policy, weight mutation, cache limits, socket reconnect and the pre-existing pose-health cases.

## Legacy webcam demo

The webcam commands below are separate from the PICO deployment. They use the older `weights/best.pt`; running them does not validate the currently deployed v6 model.

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
