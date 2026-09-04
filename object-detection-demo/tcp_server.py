"""
TCP bridge: receive PICO Camera JPEG frames from Unity, run YOLO26n v2, return detections.

Protocol (Unity -> Python), little-endian:
  magic      : 4 bytes b'PICF'
  version    : u8  (=1)
  frame_id   : u32
  timestamp  : i64  (ms, Unity/PICO capture clock)
  width      : u32  (pixels of the JPEG image)
  height     : u32
  jpeg_len   : u32
  jpeg_bytes : jpeg_len bytes

Response (Python -> Unity): one UTF-8 JSON line per frame:
  {"frame_id":N,"timestamp":T,"width":W,"height":H,"detections":[...]}

Detection fields (unchanged semantics, pixel space = PICO JPEG):
  class, confidence, centerX, centerY, x1, y1, x2, y2

NO PC Webcam. YOLO input is PICO Camera frames only.

Architecture (Batch D): TCP receiver and YOLO worker are decoupled via a latest-frame
bounded buffer. When inference lags, new frames overwrite stale ones (low latency).

Usage (headset needs adb reverse if Host is 127.0.0.1):
    adb reverse tcp:5005 tcp:5005
    python tcp_server.py
    python tcp_server.py --headless
"""

from __future__ import annotations

import argparse
import json
import platform
import socket
import struct
import subprocess
import threading
import time
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np
import torch
from ultralytics import YOLO

HOST = "0.0.0.0"
PORT = 5005
MAGIC = b"PICF"
VERSION = 1

WEIGHTS_PATH = Path(__file__).resolve().parent / "weights" / "best_yolo26n_quad_v4_50e.pt"
ACTIVE_MODEL_LABEL = "YOLO26n QUAD v4 (50 epochs)"

UNITY_CLASS_ALIASES = {
    "lighter": "Lighter",
}


@dataclass(frozen=True)
class InferenceConfig:
  device: str
  half: bool
  imgsz: int
  conf: float
  iou: float
  max_det: int


@dataclass(frozen=True)
class DecodedFrame:
  frame_id: int
  timestamp: int
  width: int
  height: int
  image: np.ndarray


class LatestFrameBuffer:
  """Single-slot latest-frame buffer. New publishes overwrite unconsumed frames."""

  def __init__(self) -> None:
    self._lock = threading.Lock()
    self._cond = threading.Condition(self._lock)
    self._frame: DecodedFrame | None = None
    self._generation = 0
    self._has_unconsumed = False
    self.dropped_frames = 0
    self.input_frames = 0

  def publish(self, frame: DecodedFrame) -> None:
    with self._cond:
      if self._has_unconsumed:
        self.dropped_frames += 1
      self._frame = frame
      self._generation += 1
      self._has_unconsumed = True
      self.input_frames += 1
      self._cond.notify_all()

  def wait_for_newer(self, last_generation: int, stop_event: threading.Event) -> tuple[DecodedFrame | None, int]:
    with self._cond:
      while self._generation == last_generation and not stop_event.is_set():
        self._cond.wait(timeout=0.25)
      if self._generation == last_generation:
        return None, last_generation
      assert self._frame is not None
      return self._frame, self._generation

  def generation(self) -> int:
    with self._lock:
      return self._generation

  def mark_consumed(self, generation: int) -> None:
    with self._cond:
      if self._generation == generation:
        self._has_unconsumed = False


class PerfAggregator:
  """Per-second aggregated timing and throughput stats."""

  def __init__(self) -> None:
    self._lock = threading.Lock()
    self._window_start = time.perf_counter()
    self.recv_ms = 0.0
    self.recv_n = 0
    self.decode_ms = 0.0
    self.decode_n = 0
    self.preprocess_ms = 0.0
    self.preprocess_n = 0
    self.infer_ms = 0.0
    self.infer_n = 0
    self.postprocess_ms = 0.0
    self.postprocess_n = 0
    self.total_inference_ms = 0.0
    self.total_inference_n = 0
    self.json_ms = 0.0
    self.json_n = 0
    self.send_ms = 0.0
    self.send_n = 0
    self.total_ms = 0.0
    self.total_n = 0
    self.input_frames = 0
    self.inference_frames = 0
    self.output_frames = 0
    self.dropped_frames = 0
    self.stale_skips = 0

  def record_recv(self, ms: float) -> None:
    with self._lock:
      self.recv_ms += ms
      self.recv_n += 1

  def record_decode(self, ms: float) -> None:
    with self._lock:
      self.decode_ms += ms
      self.decode_n += 1

  def record_preprocess(self, ms: float) -> None:
    with self._lock:
      self.preprocess_ms += ms
      self.preprocess_n += 1

  def record_infer(self, ms: float) -> None:
    with self._lock:
      self.infer_ms += ms
      self.infer_n += 1

  def record_postprocess(self, ms: float) -> None:
    with self._lock:
      self.postprocess_ms += ms
      self.postprocess_n += 1

  def record_total_inference(self, ms: float) -> None:
    with self._lock:
      self.total_inference_ms += ms
      self.total_inference_n += 1

  def record_json(self, ms: float) -> None:
    with self._lock:
      self.json_ms += ms
      self.json_n += 1

  def record_send(self, ms: float) -> None:
    with self._lock:
      self.send_ms += ms
      self.send_n += 1

  def record_total(self, ms: float) -> None:
    with self._lock:
      self.total_ms += ms
      self.total_n += 1

  def record_input(self) -> None:
    with self._lock:
      self.input_frames += 1

  def record_inference_done(self) -> None:
    with self._lock:
      self.inference_frames += 1

  def record_output(self) -> None:
    with self._lock:
      self.output_frames += 1

  def record_dropped(self, count: int) -> None:
    if count <= 0:
      return
    with self._lock:
      self.dropped_frames += count

  def record_stale_skip(self) -> None:
    with self._lock:
      self.stale_skips += 1

  def maybe_flush(self) -> None:
    with self._lock:
      now = time.perf_counter()
      elapsed = now - self._window_start
      if elapsed < 1.0:
        return

      def avg(total: float, n: int) -> float:
        return (total / n) if n else 0.0

      recv = avg(self.recv_ms, self.recv_n)
      decode = avg(self.decode_ms, self.decode_n)
      preprocess = avg(self.preprocess_ms, self.preprocess_n)
      infer = avg(self.infer_ms, self.infer_n)
      postprocess = avg(self.postprocess_ms, self.postprocess_n)
      total_inference = avg(self.total_inference_ms, self.total_inference_n)
      json_ms = avg(self.json_ms, self.json_n)
      send = avg(self.send_ms, self.send_n)
      total = avg(self.total_ms, self.total_n)
      output_fps = self.output_frames / elapsed
      input_fps = self.input_frames / elapsed
      inference_fps = self.inference_frames / elapsed

      print(
        f"[PERF] recv={recv:.1f}ms decode={decode:.1f}ms "
        f"preprocess={preprocess:.1f}ms infer={infer:.1f}ms postprocess={postprocess:.1f}ms "
        f"total_inference={total_inference:.1f}ms json={json_ms:.1f}ms send={send:.1f}ms "
        f"total={total:.1f}ms fps={output_fps:.1f} "
        f"input_fps={input_fps:.1f} inference_fps={inference_fps:.1f} "
        f"output_fps={output_fps:.1f} dropped_frames={self.dropped_frames} "
        f"stale_skips={self.stale_skips}"
      )

      self._window_start = now
      self.recv_ms = self.decode_ms = 0.0
      self.preprocess_ms = self.infer_ms = self.postprocess_ms = 0.0
      self.total_inference_ms = 0.0
      self.json_ms = self.send_ms = self.total_ms = 0.0
      self.recv_n = self.decode_n = 0
      self.preprocess_n = self.infer_n = self.postprocess_n = 0
      self.total_inference_n = 0
      self.json_n = self.send_n = self.total_n = 0
      self.input_frames = 0
      self.inference_frames = 0
      self.output_frames = 0
      self.dropped_frames = 0
      self.stale_skips = 0


def normalize_class_label(label: str) -> str:
  if not label:
    return label
  return UNITY_CLASS_ALIASES.get(label, UNITY_CLASS_ALIASES.get(label.lower(), label))


def _query_nvidia_gpu_name() -> str | None:
  try:
    proc = subprocess.run(
      ["nvidia-smi", "--query-gpu=name", "--format=csv,noheader"],
      capture_output=True,
      text=True,
      timeout=3,
      check=False,
    )
    if proc.returncode != 0:
      return None
    line = proc.stdout.strip().splitlines()
    return line[0].strip() if line else None
  except (FileNotFoundError, OSError, subprocess.TimeoutExpired):
    return None


def resolve_device(requested: str) -> str:
  if requested == "auto":
    return "0" if torch.cuda.is_available() else "cpu"
  return requested


def print_runtime_audit(config: InferenceConfig, resolved_device: str) -> None:
  weights_mb = WEIGHTS_PATH.stat().st_size / (1024 * 1024) if WEIGHTS_PATH.is_file() else 0.0
  gpu_name = torch.cuda.get_device_name(0) if torch.cuda.is_available() else None
  nvidia_gpu = _query_nvidia_gpu_name()
  dtype = "FP16" if config.half and resolved_device != "cpu" else "FP32"

  print(f"[Python] CPU: {platform.processor() or platform.machine()} ({platform.system()})")
  print(f"[Python] PyTorch: {torch.__version__}")
  print(f"[Python] CUDA available: {torch.cuda.is_available()}")
  if gpu_name:
    print(f"[Python] GPU (torch): {gpu_name}")
  elif nvidia_gpu:
    print(f"[Python] GPU (nvidia-smi): {nvidia_gpu}")
    if not torch.cuda.is_available():
      print(
        "[Python] WARN: NVIDIA GPU detected but PyTorch is CPU-only; "
        "install CUDA-enabled PyTorch to use the GPU."
      )
  else:
    print("[Python] GPU: N/A")
  print(f"[Python] Weights: {WEIGHTS_PATH.name} ({weights_mb:.2f} MB)")
  print(f"[Python] ACTIVE MODEL = {ACTIVE_MODEL_LABEL}")
  print(
    f"[Python] Inference: device={resolved_device} imgsz={config.imgsz} "
    f"half={config.half} dtype={dtype} conf={config.conf} "
    f"iou={config.iou} max_det={config.max_det}"
  )
  print("[Python] Input source = PICO Camera JPEG (no PC Webcam)")


def load_model(config: InferenceConfig) -> tuple[YOLO, str]:
  resolved_device = resolve_device(config.device)
  if config.half and resolved_device == "cpu":
    print("[Python] WARN: --half ignored on CPU (FP16 requires CUDA).")

  if WEIGHTS_PATH.is_file():
    print(f"[Python] Loading detector weights: {WEIGHTS_PATH}")
    model = YOLO(str(WEIGHTS_PATH))
  else:
    print(f"[Python] WARN {WEIGHTS_PATH} not found; falling back to yolov8n.pt")
    model = YOLO("yolov8n.pt")

  print(f"[Python] Model names={model.names}")
  print_runtime_audit(config, resolved_device)
  return model, resolved_device


def warmup_model(model: YOLO, config: InferenceConfig, resolved_device: str) -> None:
  dummy = np.zeros((480, 640, 3), dtype=np.uint8)
  use_half = config.half and resolved_device != "cpu"
  print("[Python] Warming up YOLO predictor...")
  predict_kwargs: dict = {
    "source": dummy,
    "verbose": False,
    "device": resolved_device,
    "imgsz": config.imgsz,
    "conf": config.conf,
    "iou": config.iou,
    "max_det": config.max_det,
  }
  if use_half:
    predict_kwargs["half"] = True
  with torch.inference_mode():
    model.predict(**predict_kwargs)
  if model.predictor is not None:
    print(f"[Python] Predictor device: {model.predictor.device}")


def read_exact(conn: socket.socket, n: int) -> bytes:
  buf = bytearray()
  while len(buf) < n:
    chunk = conn.recv(n - len(buf))
    if not chunk:
      raise ConnectionError("client closed while reading")
    buf.extend(chunk)
  return bytes(buf)


def read_pico_frame(conn: socket.socket) -> tuple[int, int, int, int, bytes]:
  """Returns frame_id, timestamp_ms, width, height, jpeg_bytes."""
  magic = read_exact(conn, 4)
  if magic != MAGIC:
    raise ValueError(f"bad magic {magic!r}; expected {MAGIC!r}")

  version = read_exact(conn, 1)[0]
  if version != VERSION:
    raise ValueError(f"unsupported protocol version {version}")

  frame_id, timestamp, width, height, jpeg_len = struct.unpack(
    "<IqIII", read_exact(conn, 4 + 8 + 4 + 4 + 4)
  )
  if jpeg_len <= 0 or jpeg_len > 20_000_000:
    raise ValueError(f"invalid jpeg_len={jpeg_len}")

  jpeg = read_exact(conn, jpeg_len)
  return frame_id, timestamp, width, height, jpeg


def detections_from_result(result) -> list[dict]:
  payloads: list[dict] = []
  names = result.names
  boxes = result.boxes
  if boxes is None or len(boxes) == 0:
    return payloads

  for box in boxes:
    cls_id = int(box.cls[0].item())
    conf = float(box.conf[0].item())
    raw_label = names.get(cls_id, str(cls_id))
    label = normalize_class_label(str(raw_label))
    x1, y1, x2, y2 = box.xyxy[0].tolist()
    payloads.append(
      {
        "class": label,
        "confidence": conf,
        "centerX": (x1 + x2) * 0.5,
        "centerY": (y1 + y2) * 0.5,
        "x1": x1,
        "y1": y1,
        "x2": x2,
        "y2": y2,
      }
    )
  return payloads


def decode_jpeg_frame(
  frame_id: int,
  timestamp: int,
  hdr_w: int,
  hdr_h: int,
  jpeg: bytes,
) -> DecodedFrame | None:
  arr = np.frombuffer(jpeg, dtype=np.uint8)
  frame = cv2.imdecode(arr, cv2.IMREAD_COLOR)
  if frame is None:
    print(f"[Python] Failed to decode JPEG frame_id={frame_id}")
    return None

  h, w = frame.shape[:2]
  width, height = int(w), int(h)
  if (hdr_w, hdr_h) != (width, height):
    print(
      f"[Python] WARN header size {hdr_w}x{hdr_h} != decoded {width}x{height}; "
      f"using decoded size for detections."
    )

  return DecodedFrame(
    frame_id=int(frame_id),
    timestamp=int(timestamp),
    width=width,
    height=height,
    image=frame,
  )


def receiver_loop(
  conn: socket.socket,
  buffer: LatestFrameBuffer,
  perf: PerfAggregator,
  stop_event: threading.Event,
) -> None:
  try:
    while not stop_event.is_set():
      t_recv0 = time.perf_counter()
      frame_id, timestamp, hdr_w, hdr_h, jpeg = read_pico_frame(conn)
      recv_ms = (time.perf_counter() - t_recv0) * 1000.0
      perf.record_recv(recv_ms)

      t_decode0 = time.perf_counter()
      decoded = decode_jpeg_frame(frame_id, timestamp, hdr_w, hdr_h, jpeg)
      decode_ms = (time.perf_counter() - t_decode0) * 1000.0
      perf.record_decode(decode_ms)
      if decoded is None:
        continue

      dropped_before = buffer.dropped_frames
      buffer.publish(decoded)
      perf.record_input()
      newly_dropped = buffer.dropped_frames - dropped_before
      perf.record_dropped(newly_dropped)
      perf.maybe_flush()
  except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError, ConnectionError) as e:
    print(f"[Python] Client disconnected: {e}")
  except ValueError as e:
    print(f"[Python] Protocol error: {e}")
  finally:
    stop_event.set()
    with buffer._cond:
      buffer._cond.notify_all()


def run_yolo_predict(
  model: YOLO,
  image: np.ndarray,
  resolved_device: str,
  config: InferenceConfig,
):
  use_half = config.half and resolved_device != "cpu"
  predict_kwargs: dict = {
    "source": image,
    "verbose": False,
    "device": resolved_device,
    "imgsz": config.imgsz,
    "conf": config.conf,
    "iou": config.iou,
    "max_det": config.max_det,
  }
  if use_half:
    predict_kwargs["half"] = True
  with torch.inference_mode():
    return model.predict(**predict_kwargs)


def record_yolo_speed(perf: PerfAggregator, result, total_inference_ms: float) -> None:
  speed = getattr(result, "speed", None) or {}
  perf.record_preprocess(float(speed.get("preprocess", 0.0)))
  perf.record_infer(float(speed.get("inference", 0.0)))
  perf.record_postprocess(float(speed.get("postprocess", 0.0)))
  perf.record_total_inference(total_inference_ms)


def inference_loop(
  conn: socket.socket,
  model: YOLO,
  buffer: LatestFrameBuffer,
  perf: PerfAggregator,
  stop_event: threading.Event,
  headless: bool,
  send_lock: threading.Lock,
  resolved_device: str,
  config: InferenceConfig,
) -> None:
  last_generation = 0
  try:
    while not stop_event.is_set():
      frame, generation = buffer.wait_for_newer(last_generation, stop_event)
      if frame is None:
        break

      t_total0 = time.perf_counter()

      t_infer0 = time.perf_counter()
      results = run_yolo_predict(model, frame.image, resolved_device, config)
      total_inference_ms = (time.perf_counter() - t_infer0) * 1000.0
      record_yolo_speed(perf, results[0], total_inference_ms)
      perf.record_inference_done()

      if buffer.generation() != generation:
        perf.record_stale_skip()
        last_generation = buffer.generation()
        perf.maybe_flush()
        continue

      result = results[0]
      detections = detections_from_result(result)

      payload = {
        "frame_id": frame.frame_id,
        "timestamp": frame.timestamp,
        "width": frame.width,
        "height": frame.height,
        "detections": detections,
      }

      t_json0 = time.perf_counter()
      line = json.dumps(payload) + "\n"
      encoded = line.encode("utf-8")
      json_ms = (time.perf_counter() - t_json0) * 1000.0
      perf.record_json(json_ms)

      t_send0 = time.perf_counter()
      with send_lock:
        conn.sendall(encoded)
      send_ms = (time.perf_counter() - t_send0) * 1000.0
      perf.record_send(send_ms)

      total_ms = (time.perf_counter() - t_total0) * 1000.0
      perf.record_total(total_ms)
      perf.record_output()
      buffer.mark_consumed(generation)
      last_generation = generation
      perf.maybe_flush()

      if not headless:
        annotated = result.plot()
        cv2.imshow("YOLO Debug (PICO Camera)", annotated)
        if cv2.waitKey(1) & 0xFF == ord("q"):
          print("[Python] Quit requested (Q).")
          stop_event.set()
          with buffer._cond:
            buffer._cond.notify_all()
          break
  except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError, ConnectionError) as e:
    print(f"[Python] Send failed / client disconnected: {e}")
  finally:
    stop_event.set()
    with buffer._cond:
      buffer._cond.notify_all()


def parse_args() -> argparse.Namespace:
  parser = argparse.ArgumentParser(description="PICO PICF JPEG -> YOLO TCP bridge")
  parser.add_argument(
    "--headless",
    action="store_true",
    help="Skip cv2.imshow / result.plot debug window (YOLO + JSON unchanged)",
  )
  parser.add_argument(
    "--device",
    default="auto",
    help="Inference device: auto (CUDA if available else CPU), cpu, cuda, or GPU index (default: auto)",
  )
  parser.add_argument(
    "--half",
    action="store_true",
    help="Use FP16 on CUDA (ignored on CPU; default FP32)",
  )
  parser.add_argument(
    "--imgsz",
    type=int,
    default=640,
    help="YOLO letterbox inference size (default: 640)",
  )
  parser.add_argument(
    "--conf",
    type=float,
    default=0.399,
    help="Minimum detection confidence (default: 0.399)",
  )
  parser.add_argument(
    "--iou",
    type=float,
    default=0.7,
    help="NMS IoU threshold (default: 0.7)",
  )
  parser.add_argument(
    "--max-det",
    type=int,
    default=1,
    help="Maximum detections returned per frame (default: 1)",
  )
  return parser.parse_args()


def main() -> None:
  args = parse_args()
  headless = args.headless
  config = InferenceConfig(
    device=args.device,
    half=args.half,
    imgsz=args.imgsz,
    conf=args.conf,
    iou=args.iou,
    max_det=max(1, args.max_det),
  )

  model, resolved_device = load_model(config)
  warmup_model(model, config, resolved_device)

  server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
  server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
  server.bind((HOST, PORT))
  server.listen(1)

  print(f"[Python] TCP server listening on {HOST}:{PORT}")
  print("[Python] Waiting for Unity (PICO) client to send PICF JPEG frames...")
  print("[Python] Pipeline: receiver thread + latest-frame buffer + YOLO worker")
  if headless:
    print("[Python] Headless mode: debug window disabled.")
  else:
    print("[Python] Debug window open. Press Q to quit.")

  conn, addr = server.accept()
  print(f"[Python] Client connected: {addr}")

  buffer = LatestFrameBuffer()
  perf = PerfAggregator()
  stop_event = threading.Event()
  send_lock = threading.Lock()

  receiver = threading.Thread(
    target=receiver_loop,
    name="picf-receiver",
    args=(conn, buffer, perf, stop_event),
    daemon=False,
  )
  worker = threading.Thread(
    target=inference_loop,
    name="yolo-worker",
    args=(
      conn,
      model,
      buffer,
      perf,
      stop_event,
      headless,
      send_lock,
      resolved_device,
      config,
    ),
    daemon=False,
  )

  receiver.start()
  worker.start()

  try:
    receiver.join()
    worker.join()
  finally:
    stop_event.set()
    if not headless:
      cv2.destroyAllWindows()
    try:
      conn.close()
    except Exception:
      pass
    try:
      server.close()
    except Exception:
      pass
    print("[Python] Server closed (no webcam was used).")


if __name__ == "__main__":
  main()
