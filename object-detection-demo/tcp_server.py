"""
TCP bridge: receive PICO Camera JPEG frames from Unity, run YOLO26n v2, return detections.

Protocol (Unity -> Python), little-endian:
  magic      : 4 bytes b'PICF'
  version    : u8  (=1)
  frame_id   : u32
  timestamp  : i64  (UTC send time in ms; frame_id joins the headset capture-pose sidecar)
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
import hashlib
import json
import platform
import socket
import struct
import subprocess
import threading
import time
import uuid
from record_journal import append_response
from image_orientation import inference_image, original_detections
from discovery import DiscoveryPool, detection_limit, inference_limit, filter_candidates
from dataclasses import dataclass, replace
from pathlib import Path

import cv2
import numpy as np
import torch
from ultralytics import YOLO
from scene_protocol import load_profile, InstanceTracker, read_packet, native_class_filter
from model_artifact import verify_artifact

ACTIVE_PROFILE = None
TRACKER = InstanceTracker()
PROFILE_CONFIG = None
START_SCENE = 'lighter'
MAX_DET_OVERRIDE = None
CPU_THREADS_OVERRIDE = None
RECORD_DIR = None
LAST_RECORD_AT = 0.0
RECORD_SESSION = "not-connected"

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
  image_rotation: int = 0


@dataclass(frozen=True)
class DecodedFrame:
  frame_id: int
  timestamp: int
  width: int
  height: int
  image: np.ndarray
  scene: str | None = None


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
  print("[Python] Input source = PICF JPEG client (PICO camera expected; no PC webcam capture)")


def load_model(config: InferenceConfig) -> tuple[YOLO, str]:
  resolved_device = resolve_device(config.device)
  if config.half and resolved_device == "cpu":
    print("[Python] WARN: --half ignored on CPU (FP16 requires CUDA).")

  if WEIGHTS_PATH.is_file():
    print(f"[Python] Loading detector weights: {WEIGHTS_PATH}")
    model = YOLO(str(WEIGHTS_PATH))
  else:
    raise FileNotFoundError(f"SCENE_NOT_CONFIGURED: {WEIGHTS_PATH}; lighter must never fall back to a COCO model")

  print(f"[Python] Model names={model.names}")
  print(f"[Python] SHA256={hashlib.sha256(WEIGHTS_PATH.read_bytes()).hexdigest()}")
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


def read_pico_frame(conn: socket.socket) -> tuple[int, int, int, int, bytes, str | None]:
  """Read v1 lighter or v2 per-frame scene selection."""
  return read_packet(lambda count: read_exact(conn,count))


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
    label = (ACTIVE_PROFILE or {}).get("aliases", {}).get(str(raw_label).lower(), normalize_class_label(str(raw_label)))
    if ACTIVE_PROFILE and label not in ACTIVE_PROFILE["allowed"]:
      continue
    x1, y1, x2, y2 = box.xyxy[0].tolist()
    payloads.append(
      {
        "class": label,
        "class_id": cls_id,
        "class_name": label,
        "bbox": [x1, y1, x2, y2],
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
  scene: str | None = None,
) -> DecodedFrame | None:
  global LAST_RECORD_AT
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

  if RECORD_DIR is not None and time.monotonic() - LAST_RECORD_AT >= 1.0:
    LAST_RECORD_AT = time.monotonic()
    filename = f"{RECORD_SESSION}-frame-{frame_id:010d}.jpg"
    (RECORD_DIR / filename).write_bytes(jpeg)
    with (RECORD_DIR / "captures.jsonl").open("a", encoding="utf-8") as journal:
      journal.write(json.dumps({"image":filename,"session":RECORD_SESSION,"frame_id":frame_id,"send_utc_ms":timestamp,
        "width":width,"height":height,"source":"PICF client JPEG","origin_verified":False,"group":RECORD_DIR.name,
        "labels":"NOT_ANNOTATED","scene":scene or START_SCENE,"capture_pose":"join by frame_id to headset poses.jsonl; UTC send time is NOT capture time"}) + "\n")

  return DecodedFrame(
    frame_id=int(frame_id),
    timestamp=int(timestamp),
    width=width,
    height=height,
    image=frame,
    scene=scene,
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
      frame_id, timestamp, hdr_w, hdr_h, jpeg, scene = read_pico_frame(conn)
      recv_ms = (time.perf_counter() - t_recv0) * 1000.0
      perf.record_recv(recv_ms)

      t_decode0 = time.perf_counter()
      decoded = decode_jpeg_frame(frame_id, timestamp, hdr_w, hdr_h, jpeg, scene)
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
  class_filter=None,
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
    "classes": class_filter,
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
  if resolved_device == 'cpu':
    if CPU_THREADS_OVERRIDE is not None:
      torch.set_num_threads(CPU_THREADS_OVERRIDE)
    print(f'[Python] YOLO worker CPU threads={torch.get_num_threads()}', flush=True)
  last_generation = 0
  global ACTIVE_PROFILE, WEIGHTS_PATH, ACTIVE_MODEL_LABEL, TRACKER
  ACTIVE_PROFILE=load_profile(START_SCENE,PROFILE_CONFIG)
  WEIGHTS_PATH=(Path(__file__).parent/ACTIVE_PROFILE['weights']).resolve()
  ACTIVE_MODEL_LABEL=START_SCENE
  active_scene=START_SCENE
  models={WEIGHTS_PATH:model}
  ACTIVE_PROFILE, starting_hash = verify_artifact(WEIGHTS_PATH, ACTIVE_PROFILE)
  model_hashes={WEIGHTS_PATH:starting_hash}
  discovery_pool=DiscoveryPool(PROFILE_CONFIG,Path(__file__).parent)
  discovery_models={WEIGHTS_PATH:model}
  try:
    while not stop_event.is_set():
      frame, generation = buffer.wait_for_newer(last_generation, stop_event)
      if frame is None:
        break

      t_total0 = time.perf_counter()

      requested=frame.scene or START_SCENE
      if requested=='discovery':
        source=discovery_pool.next_source();path=source['path']
        if path not in discovery_models:
          discovery_models[path]=YOLO(str(path))
          print(f'[Python] Discovery weights={path} SHA256={hashlib.sha256(path.read_bytes()).hexdigest()} classes={discovery_models[path].names}',flush=True)
        discovery_model=discovery_models[path]
        previous_profile=ACTIVE_PROFILE
        try:
          ACTIVE_PROFILE=discovery_pool.profile(source,discovery_model.names)
          thresholds={label:(value if value is not None else config.conf) for label,value in ACTIVE_PROFILE['class_confidence'].items()}
          output_limit=detection_limit(MAX_DET_OVERRIDE if MAX_DET_OVERRIDE is not None else ACTIVE_PROFILE['max_det'])
          discovery_config=replace(config,conf=min(thresholds.values()),max_det=inference_limit(thresholds,output_limit))
          results=run_yolo_predict(discovery_model,inference_image(frame.image,config.image_rotation),resolved_device,discovery_config,native_class_filter(discovery_model.names,ACTIVE_PROFILE))
          detections=original_detections(detections_from_result(results[0]),frame.width,frame.height,config.image_rotation)
          detections=filter_candidates(detections,thresholds,output_limit)
          detections=source['tracker'].assign(detections,frame.frame_id,time.monotonic())
          for d in detections:d['target_scene']=ACTIVE_PROFILE['routes'][d['class']]
          payload=dict(scene='discovery',status='OK',frame_id=frame.frame_id,timestamp=frame.timestamp,width=frame.width,height=frame.height,
                       detections=detections,weights=str(path),inference_rotation_degrees=config.image_rotation,
                       weights_sha256=source['sha256'],
                       discovery_model_budget=1,discovery_model_count=len(discovery_pool.sources),
                       detection_limit=output_limit,inference_max_det=discovery_config.max_det,
                       class_confidence=thresholds,
                       model_validation='; '.join(f'{scene}:{state}' for scene,state in sorted(source['validation'].items())))
        finally:
          ACTIVE_PROFILE=previous_profile
        if buffer.generation()==generation:
          with send_lock:conn.sendall((json.dumps(payload)+'\n').encode())
          append_response(RECORD_DIR,RECORD_SESSION,payload)
        else:perf.record_stale_skip()
        buffer.mark_consumed(generation);last_generation=generation
        continue
      try:
        profile=load_profile(requested,PROFILE_CONFIG)
        weight=Path(profile['weights'])
        requested_path=(Path(__file__).parent/weight).resolve() if not weight.is_absolute() else weight.resolve()
        if requested!=active_scene or requested_path!=WEIGHTS_PATH:
          profile, artifact_hash = verify_artifact(requested_path, profile, loaded_hash=model_hashes.get(requested_path))
          if requested_path not in models:
            if not requested_path.is_file(): raise FileNotFoundError('SCENE_NOT_CONFIGURED: '+str(requested_path))
            models[requested_path]=YOLO(str(requested_path))
            model_hashes[requested_path]=artifact_hash
          WEIGHTS_PATH=requested_path
          model=models[requested_path]; ACTIVE_PROFILE=profile; ACTIVE_MODEL_LABEL=requested
          TRACKER=InstanceTracker(); active_scene=requested
          print(f'[Python] Scene={requested} weights={WEIGHTS_PATH} SHA256={model_hashes[WEIGHTS_PATH]} classes={model.names}',flush=True)
        class_filter=native_class_filter(model.names,ACTIVE_PROFILE)
      except (ValueError,FileNotFoundError) as error:
        failure=dict(frame_id=frame.frame_id,timestamp=frame.timestamp,width=frame.width,height=frame.height,scene=requested,status=str(error),detections=[])
        with send_lock:
          conn.sendall((json.dumps(failure)+'\n').encode())
        append_response(RECORD_DIR,RECORD_SESSION,failure)
        buffer.mark_consumed(generation); last_generation=generation
        continue

      t_infer0 = time.perf_counter()
      scene_config=replace(config,max_det=MAX_DET_OVERRIDE or ACTIVE_PROFILE['max_det'],
                           conf=ACTIVE_PROFILE.get('confidence',config.conf))
      results = run_yolo_predict(model, inference_image(frame.image, config.image_rotation), resolved_device, scene_config,class_filter)
      total_inference_ms = (time.perf_counter() - t_infer0) * 1000.0
      record_yolo_speed(perf, results[0], total_inference_ms)
      perf.record_inference_done()

      if buffer.generation() != generation:
        perf.record_stale_skip()
        last_generation = generation
        perf.maybe_flush()
        continue

      result = results[0]
      detections = detections_from_result(result)
      detections = original_detections(detections, frame.width, frame.height, config.image_rotation)

      detections = TRACKER.assign(detections, frame.frame_id, time.monotonic())

      payload = {
        "weights": str(WEIGHTS_PATH),
        "weights_sha256": model_hashes[WEIGHTS_PATH],
        "model_validation": ACTIVE_PROFILE.get('validation','MAIN_BASELINE'),
        "inference_rotation_degrees": config.image_rotation,
        "scene": active_scene,
        "status": "OK",
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
      append_response(RECORD_DIR,RECORD_SESSION,payload)
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
        annotated = inference_image(annotated, config.image_rotation)
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
  parser.add_argument('--host', default=HOST, help='Bind address; defaults to existing PICO listener')
  parser.add_argument('--port', type=int, default=PORT, help='TCP listener port; use a separate port for offline model validation')
  parser.add_argument('--image-rotation', type=int, choices=[0,180], default=0,
                      help='Optional inference rotation; returned boxes remain in original JPEG coordinates')
  parser.add_argument("--scene", default="lighter", choices=["lighter","tree","bugs","statue","room","plant","pet"])
  parser.add_argument("--profiles", type=Path, default=Path(__file__).with_name("scene_profiles.json"))
  parser.add_argument("--record-dir", type=Path, help="Save real PICO JPEGs at 1 Hz for manual annotation; empty session directory required")
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
    default=None,
    help="Override maximum detections per scene/discovery frame (default: scene profile)",
  )
  parser.add_argument('--cpu-threads', type=int, help='Explicit PyTorch intra-op threads in the inference worker; omitted preserves library defaults')
  args=parser.parse_args()
  if args.max_det is not None and args.max_det < 1:parser.error('--max-det must be positive')
  if args.cpu_threads is not None and args.cpu_threads < 1:parser.error('--cpu-threads must be positive')
  if not 1<=args.port<=65535:parser.error('--port must be between 1 and 65535')
  return args


def main() -> None:
  global WEIGHTS_PATH, ACTIVE_PROFILE, ACTIVE_MODEL_LABEL, TRACKER, RECORD_DIR, RECORD_SESSION, LAST_RECORD_AT
  args = parse_args()
  global PROFILE_CONFIG, START_SCENE, MAX_DET_OVERRIDE, CPU_THREADS_OVERRIDE
  CPU_THREADS_OVERRIDE=args.cpu_threads
  PROFILE_CONFIG=args.profiles
  START_SCENE=args.scene
  MAX_DET_OVERRIDE=args.max_det
  ACTIVE_PROFILE = load_profile(args.scene, args.profiles)
  if args.record_dir:
    RECORD_DIR = args.record_dir.resolve()
    if RECORD_DIR.exists() and any(RECORD_DIR.iterdir()):
      raise ValueError("Use a fresh capture session directory to prevent frame-name collisions")
    RECORD_DIR.mkdir(parents=True, exist_ok=True)
  weight = ACTIVE_PROFILE["weights"]
  WEIGHTS_PATH = (Path(__file__).parent / weight).resolve()
  ACTIVE_MODEL_LABEL = args.scene + ": " + weight
  # Pretrained models may download by name; explicitly selected profiles only.
  if not WEIGHTS_PATH.exists():
    raise FileNotFoundError('SCENE_NOT_CONFIGURED: '+str(WEIGHTS_PATH))
  headless = args.headless
  config = InferenceConfig(
    device=args.device,
    half=args.half,
    imgsz=args.imgsz,
    conf=args.conf,
    iou=args.iou,
    max_det=max(1, args.max_det or ACTIVE_PROFILE["max_det"]),
    image_rotation=args.image_rotation,
  )
  print(f'[Python] Inference rotation={config.image_rotation}; TCP boxes=original JPEG coordinates')
  print(f'[Python] Effective starting scene confidence={ACTIVE_PROFILE.get("confidence",config.conf)}; profile confidence overrides base confidence per frame')

  ACTIVE_PROFILE, artifact_hash = verify_artifact(WEIGHTS_PATH, ACTIVE_PROFILE)
  print(f"[Python] Artifact SHA256={artifact_hash} validation={ACTIVE_PROFILE['validation']}", flush=True)
  model, resolved_device = load_model(config)
  warmup_model(model, config, resolved_device)

  server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
  server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
  server.bind((args.host, args.port))
  server.listen(1)

  print(f"[Python] TCP server listening on {args.host}:{args.port}")
  print("[Python] Waiting for Unity (PICO) client to send PICF JPEG frames...")
  print("[Python] Pipeline: receiver thread + latest-frame buffer + YOLO worker")
  if headless:
    print("[Python] Headless mode: debug window disabled.")
  else:
    print("[Python] Debug window open. Press Q to quit.")

  while True:
    conn, addr = server.accept()
    RECORD_SESSION = uuid.uuid4().hex
    LAST_RECORD_AT = 0.0
    print(f"[Python] Client connected: {addr}")

    TRACKER = InstanceTracker()
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
      print("[Python] Client session closed; waiting for reconnect.")


if __name__ == "__main__":
  main()
