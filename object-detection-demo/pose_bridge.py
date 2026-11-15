"""Independent F2G-Pose sidecar. Existing tcp_server.py remains the position service.

Wire protocol: uint32 LE JSON length, UTF-8 header, native top-down left RGBA
and right uint8 grayscale. Reply: uint32 LE JSON length followed by JSON.
One request in flight per client. No synthetic depth/rotation fallback.
"""
from __future__ import annotations

import argparse
import json
import socket
import struct
import time
import zlib
import threading
from pose_availability import serve_health
from detector_config import WEIGHTS_PATH, DEFAULT_CONF, DEFAULT_IMGSZ, DEFAULT_IOU, load_detector
from pathlib import Path

import cv2
import numpy as np

MAX_JSON = 65536
MAX_PIXELS = 1280 * 960


def recv_exact(sock, count):
    chunks = bytearray()
    while len(chunks) < count:
        data = sock.recv(count - len(chunks))
        if not data:
            raise EOFError("Peer closed the connection")
        chunks.extend(data)
    return bytes(chunks)


def receive_json(sock):
    size = struct.unpack("<I", recv_exact(sock, 4))[0]
    if not 0 < size <= MAX_JSON:
        raise ValueError("Invalid JSON length")
    return json.loads(recv_exact(sock, size))


def send_json(sock, value):
    data = json.dumps(value, allow_nan=False, separators=(",", ":")).encode()
    if len(data) > MAX_JSON:
        raise ValueError("Reply too large")
    sock.sendall(struct.pack("<I", len(data)) + data)


def matrix(values, shape):
    out = np.asarray(values, dtype=np.float64).reshape(shape)
    if not np.isfinite(out).all():
        raise ValueError("Nonfinite calibration")
    return out


def check_rotation(rotation):
    rotation = matrix(rotation, (3, 3))
    if not np.allclose(rotation.T @ rotation, np.eye(3), atol=0.015) or abs(np.linalg.det(rotation) - 1) > 0.015:
        raise ValueError("Invalid rotation matrix")
    return rotation


def validate_header(header):
    if (header.get("version"), header.get("pixel_format")) not in (
        (1, "native_top_down_rgba_gray"), (2, "native_top_down_gzip_rgba_gray")):
        raise ValueError("Unsupported pose protocol")
    w, h = int(header["width"]), int(header["height"])
    if w < 64 or h < 48 or w * h > MAX_PIXELS:
        raise ValueError("Invalid image dimensions")
    if header["left_bytes"] != w * h * 4 or header["right_bytes"] != w * h:
        raise ValueError("Invalid payload lengths")
    if not isinstance(header["session"], str) or not 0 < len(header["session"]) <= 64 or int(header["frame_id"]) <= 0:
        raise ValueError("Invalid frame identity")
    if abs(int(header["timestamp_ns"]) - int(header["right_timestamp_ns"])) > 5_000_000:
        raise ValueError("Unsynchronized stereo pair")
    for key in ("left_k", "right_k"):
        k = matrix(header[key], (3, 3))
        if k[0, 0] <= 0 or k[1, 1] <= 0 or not np.allclose(k[2], [0, 0, 1]) or abs(k[0, 1]) > 1e-6:
            raise ValueError("Invalid pinhole intrinsics")
    check_rotation(header["right_from_left_r"])
    baseline = np.linalg.norm(matrix(header["right_from_left_t"], (3,)))
    if not 0.01 < baseline < 0.5:
        raise ValueError("Invalid stereo baseline")
    return w, h


def receive_compressed(sock, expected):
    size = struct.unpack("<I", recv_exact(sock, 4))[0]
    if not 0 < size <= expected + 65536:
        raise ValueError("Invalid compressed payload length")
    decoder = zlib.decompressobj(16 + zlib.MAX_WBITS)
    raw = decoder.decompress(recv_exact(sock, size), expected + 1)
    if len(raw) != expected or not decoder.eof or decoder.unused_data or decoder.unconsumed_tail:
        raise ValueError("Invalid compressed image size or stream")
    return raw


def receive_frame(sock, header=None):
    header = receive_json(sock) if header is None else header
    w, h = validate_header(header)
    receive = receive_compressed if header["version"] == 2 else recv_exact
    rgba = np.frombuffer(receive(sock, w * h * 4), np.uint8).reshape(h, w, 4)
    right = np.frombuffer(receive(sock, w * h), np.uint8).reshape(h, w)
    return header, cv2.cvtColor(rgba, cv2.COLOR_RGBA2RGB), right


class StereoDepth:
    """Rectify virtual pinhole camera images; reject unreliable disparities."""

    def __init__(self):
        self.key = None
        self.left_matcher = cv2.StereoSGBM_create(
            minDisparity=0, numDisparities=128, blockSize=5, P1=8 * 25,
            P2=32 * 25, disp12MaxDiff=1, uniquenessRatio=12,
            speckleWindowSize=60, speckleRange=2, mode=cv2.STEREO_SGBM_MODE_SGBM_3WAY)
        self.right_matcher = cv2.StereoSGBM_create(
            minDisparity=-128, numDisparities=128, blockSize=5, P1=8 * 25,
            P2=32 * 25, disp12MaxDiff=1, uniquenessRatio=12,
            speckleWindowSize=60, speckleRange=2, mode=cv2.STEREO_SGBM_MODE_SGBM_3WAY)

    def rectify(self, header, rgb, right):
        w, h = validate_header(header)
        key = json.dumps({k: header[k] for k in ("width", "height", "left_k", "right_k", "right_from_left_r", "right_from_left_t")}, sort_keys=True)
        if key != self.key:
            kl, kr = matrix(header["left_k"], (3, 3)), matrix(header["right_k"], (3, 3))
            r = check_rotation(header["right_from_left_r"])
            t = matrix(header["right_from_left_t"], (3, 1))
            r1, r2, p1, p2, q, roi1, roi2 = cv2.stereoRectify(
                kl, np.zeros(5), kr, np.zeros(5), (w, h), r, t,
                flags=cv2.CALIB_ZERO_DISPARITY, alpha=0)
            if abs(p2[1, 3]) > abs(p2[0, 3]) or p2[0, 3] >= 0:
                raise ValueError("Expected horizontal left-to-right stereo; verify SDK extrinsics")
            self.map_l = cv2.initUndistortRectifyMap(kl, np.zeros(5), r1, p1, (w, h), cv2.CV_32FC1)
            self.map_r = cv2.initUndistortRectifyMap(kr, np.zeros(5), r2, p2, (w, h), cv2.CV_32FC1)
            self.r1, self.k, self.q = r1, p1[:3, :3], q
            self.valid_roi = np.zeros((h, w), bool)
            x, y, rw, rh = roi1
            self.valid_roi[y:y+rh, x:x+rw] = True
            self.key = key
        return (cv2.remap(rgb, *self.map_l, cv2.INTER_LINEAR),
                cv2.remap(right, *self.map_r, cv2.INTER_LINEAR))

    def compute(self, rgb, right):
        left = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
        disparity = self.left_matcher.compute(left, right).astype(np.float32) / 16
        reverse = self.right_matcher.compute(right, left).astype(np.float32) / 16
        yy, xx = np.indices(disparity.shape)
        xr = np.rint(xx - disparity).astype(np.int32)
        in_bounds = (xr >= 0) & (xr < disparity.shape[1])
        back = reverse[yy, np.clip(xr, 0, disparity.shape[1] - 1)]
        valid = in_bounds & (disparity > 1) & (np.abs(disparity + back) < 1.5) & self.valid_roi
        points = cv2.reprojectImageTo3D(disparity, self.q)
        z = points[:, :, 2]
        valid &= np.isfinite(z) & (z >= 0.20) & (z <= 3.0)
        return np.where(valid, z, 0).astype(np.float32)


def foreground_depth(depth, mask):
    mask = np.asarray(mask, bool)
    if mask.shape != depth.shape or np.count_nonzero(mask) < 100:
        raise ValueError("Insufficient foreground mask")
    # Avoid silhouette bleed; do not invent depth where stereo failed.
    inner = cv2.erode(mask.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
    valid = inner & np.isfinite(depth) & (depth > 0)
    count = int(valid.sum())
    ratio = count / max(1, int(inner.sum()))
    if count < 100 or ratio < 0.30:
        raise ValueError(f"Insufficient foreground depth: {count} points, ratio={ratio:.3f}")
    return np.where(valid, depth, 0).astype(np.float32), count, ratio


class Models:
    def __init__(self, args, offline=False):
        if not offline:
            self.detector_options = dict(conf=args.conf, imgsz=args.imgsz, iou=args.iou)
            self.detector = load_detector(args.yolo, **self.detector_options)
        from f2g_pose import PoseEstimator
        self.pose = PoseEstimator(str(args.checkpoint), device=args.device)
        if not offline:
            from segment_anything import SamPredictor, sam_model_registry
            self.sam = SamPredictor(sam_model_registry["vit_b"](checkpoint=str(args.sam)).to(args.device))
        self.device = args.device
        self.native_bottom_up = getattr(args, "native_bottom_up", False)

    def warmup(self, sample):
        """Complete cold GPU initialization before advertising readiness."""
        import torch
        rgb = cv2.cvtColor(cv2.imread(str(sample / "rgb.png")), cv2.COLOR_BGR2RGB)
        depth = np.load(sample / "depth.npy")
        mask = cv2.imread(str(sample / "mask.png"), cv2.IMREAD_GRAYSCALE)
        k = np.loadtxt(sample / "K.txt")
        ys, xs = np.nonzero(mask)
        box = np.array([xs.min(), ys.min(), xs.max(), ys.max()])
        for _ in range(2):
            torch.cuda.synchronize()
            started = time.perf_counter()
            self.detector.predict(source=cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR),
                                  device=self.device, verbose=False, **self.detector_options)
            self.sam.set_image(rgb)
            self.sam.predict(box=box, multimask_output=False)
            # The bundled sample is already conventional top-down, unlike PICO raw rows.
            result = self.pose.predict(rgb, depth, k, mask, depth_scale=1.0, return_shape=False)
            r = result["rotation"]
            if not np.isfinite(r).all() or not np.allclose(r.T @ r, np.eye(3), atol=.015):
                raise ValueError("Warmup produced invalid rotation")
            torch.cuda.synchronize()
            print(f"Official-sample combined warmup: {(time.perf_counter()-started)*1000:.1f} ms", flush=True)
        torch.cuda.synchronize()
        print(f"Model warmup complete; peak allocated={torch.cuda.max_memory_allocated()/1024**2:.0f} MiB", flush=True)

    def segment(self, rgb):
        # Match the position path's native rows (Unity sender flipV=true plus
        # EncodeToJPG). SAM and F2G still consume conventional top-down images.
        result = self.detector.predict(source=cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR),
                                       device=self.device, verbose=False, **self.detector_options)[0]
        boxes = [b.xyxy[0].cpu().numpy() for b in result.boxes
                 if result.names[int(b.cls.item())].lower() == "lighter"]
        if len(boxes) != 1:
            raise ValueError(f"Require exactly one lighter, detected {len(boxes)}")
        box = boxes[0]
        h, w = rgb.shape[:2]
        if box[0] < 3 or box[1] < 3 or box[2] > w-3 or box[3] > h-3:
            raise ValueError("Target clipped by image edge")
        if self.native_bottom_up:
            rgb = np.ascontiguousarray(rgb[::-1])
            box = box.copy()
            box[1], box[3] = h - 1 - box[3], h - 1 - box[1]
        self.sam.set_image(rgb)
        masks, scores, _ = self.sam.predict(box=box, multimask_output=False)
        if not np.isfinite(scores[0]) or scores[0] < 0.80:
            raise ValueError("Low SAM mask score")
        mask = masks[0]
        box_mask = np.zeros((h, w), bool)
        x1, y1, x2, y2 = np.rint(box).astype(int)
        box_mask[max(0,y1):min(h,y2+1), max(0,x1):min(w,x2+1)] = True
        if (mask & ~box_mask).sum() > 0.15 * max(1, mask.sum()):
            raise ValueError("Mask extends beyond target box")
        result = mask & box_mask
        return np.ascontiguousarray(result[::-1]) if self.native_bottom_up else result

    def estimate(self, rgb, depth, k, mask):
        if not self.native_bottom_up:
            return self.pose.predict(rgb, depth, k, mask.astype(np.uint8),
                                     depth_scale=1.0, return_shape=False)
        # Keep stereo in its calibrated native pixel basis. Only normalize model
        # input; reflect both camera and object bases to return a proper native R.
        model_k = k.copy()
        model_k[1, 2] = rgb.shape[0] - 1 - k[1, 2]
        result = self.pose.predict(np.ascontiguousarray(rgb[::-1]),
            np.ascontiguousarray(depth[::-1]), model_k,
            np.ascontiguousarray(mask[::-1], dtype=np.uint8), depth_scale=1.0, return_shape=False)
        reflection = np.diag([1., -1., 1.])
        return {**result, "rotation": reflection @ result["rotation"] @ reflection}


def save_capture(folder, header, rgb, right):
    folder.mkdir(parents=True, exist_ok=False)
    (folder / "header.json").write_text(json.dumps(header, indent=2), encoding="utf-8")
    cv2.imwrite(str(folder / "left.png"), cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR))
    cv2.imwrite(str(folder / "right.png"), right)


def process(header, rgb, right, stereo, models, output=None, manual_mask=None):
    started = time.perf_counter()
    rect_rgb, rect_right = stereo.rectify(header, rgb, right)
    depth = stereo.compute(rect_rgb, rect_right)
    if output:
        output.mkdir(parents=True, exist_ok=True)
        cv2.imwrite(str(output / "rectified_rgb.png"), cv2.cvtColor(rect_rgb, cv2.COLOR_RGB2BGR))
        cv2.imwrite(str(output / "rectified_right.png"), rect_right)
        np.save(output / "depth_m.npy", depth)
        np.savetxt(output / "K.txt", stereo.k)
    if models is None:
        return {"valid": False, "reason": "RECORD_ONLY: no pose inference"}
    if manual_mask is not None:
        mask = cv2.imread(str(manual_mask), cv2.IMREAD_GRAYSCALE)
        if mask is None or mask.shape != depth.shape:
            raise ValueError("Manual mask must match rectified_rgb.png")
        mask = mask > 0
    else:
        mask = models.segment(rect_rgb)
    depth, count, ratio = foreground_depth(depth, mask)
    result = models.estimate(rect_rgb, depth, stereo.k, mask)
    rotation = check_rotation(stereo.r1.T @ check_rotation(result["rotation"]))
    reply = {"valid": True, "reason": "OK", "rotation": rotation.reshape(-1).tolist(),
             "valid_depth_points": count, "valid_depth_ratio": ratio,
             "processing_ms": (time.perf_counter() - started) * 1000}
    if output:
        cv2.imwrite(str(output / "mask.png"), mask.astype(np.uint8) * 255)
        (output / "pose.json").write_text(json.dumps(reply, indent=2), encoding="utf-8")
    return reply


def serve(args):
    models = None if args.record_only else Models(args)
    if models is not None and getattr(args, "warmup_sample", None):
        models.warmup(args.warmup_sample)
    stereo = StereoDepth()
    run_id = time.strftime("%Y%m%d-%H%M%S")
    recorded = 0
    processing = threading.Lock()
    clients = threading.BoundedSemaphore(2)

    def handle(client, address):
        nonlocal recorded
        print(f"Connected {address}", flush=True)
        try:
            with client:
                client.settimeout(5)
                try:
                    while True:
                        header = receive_json(client)
                        if header.get("op") == "health":
                            if header.get("version") != 2:
                                raise ValueError("Unsupported health protocol")
                            send_json(client, {"version": 2, "pose_ready": models is not None,
                                              "image_ready": True,
                                              "mode": "F2G_POSE" if models is not None else "RECORD_ONLY"})
                            continue
                        header, rgb, right = receive_frame(client, header)
                        reply = {"session": header["session"], "frame_id": header["frame_id"],
                                 "timestamp_ns": header["timestamp_ns"]}
                        # A cancelled GPU request may still be running. Never queue another inference behind it.
                        acquired = processing.acquire(blocking=False)
                        started = time.perf_counter()
                        try:
                            if not acquired:
                                reply.update(valid=False, reason="POSE_BUSY: previous inference still running")
                            else:
                                output = None
                                if args.record_dir and recorded < getattr(args, "record_limit", 60):
                                    output = args.record_dir / run_id / str(time.time_ns())
                                    save_capture(output, header, rgb, right)
                                    recorded += 1
                                reply.update(process(header, rgb, right, stereo, models, output))
                        except Exception as exc:
                            reply.update(valid=False, reason=f"{type(exc).__name__}: {exc}"[:500])
                        finally:
                            if acquired: processing.release()
                        send_json(client, reply)
                        print(f"frame={reply['frame_id']} processing_ms={(time.perf_counter()-started)*1000:.1f} {reply['reason']}", flush=True)
                except (EOFError, OSError, ValueError, KeyError, zlib.error) as exc:
                    print(f"Disconnected: {exc}", flush=True)
        finally:
            clients.release()

    with socket.socket() as listener, socket.socket() as health_listener:
        listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        listener.bind((args.host, args.port)); listener.listen(4)
        health_listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        health_listener.bind((args.host, getattr(args, 'health_port', None) or args.port+1))
        health_listener.listen(4)
        detector_identity = models.detector.magicmr_detector_identity if models is not None else None
        threading.Thread(target=serve_health,args=(health_listener,models is not None,detector_identity),daemon=True).start()
        print(f"Pose sidecar {args.host}:{args.port}; mode={'RECORD_ONLY' if models is None else 'F2G_POSE'}", flush=True)
        while True:
            client, address = listener.accept()
            if not clients.acquire(blocking=False):
                client.close()
                continue
            threading.Thread(target=handle,args=(client,address),daemon=True).start()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5006)
    parser.add_argument("--health-port", type=int, help="PC-only readiness listener; defaults to image port + 1")
    parser.add_argument("--checkpoint", type=Path)
    parser.add_argument("--sam", type=Path)
    parser.add_argument("--yolo", type=Path, default=WEIGHTS_PATH)
    parser.add_argument("--conf", type=float, default=DEFAULT_CONF)
    parser.add_argument("--imgsz", type=int, default=DEFAULT_IMGSZ)
    parser.add_argument("--iou", type=float, default=DEFAULT_IOU)
    parser.add_argument("--device", default="cuda:0")
    parser.add_argument("--warmup-sample", type=Path, help="Official ROPE sample folder; warm all models before readiness")
    parser.add_argument("--native-bottom-up", action="store_true", help="PICO raw row order: normalize model inputs and return rotation to native basis")
    parser.add_argument("--record-only", action="store_true",
                        help="Prepare stereo without pose inference; only save files when --record-dir is given")
    parser.add_argument("--record-dir", type=Path)
    parser.add_argument("--record-limit", type=int, default=60, help="Maximum saved frame sets per server launch")
    parser.add_argument("--offline", type=Path, help="Recorded frame directory")
    parser.add_argument("--mask", type=Path, help="Mask drawn on rectified_rgb.png (offline only)")
    args = parser.parse_args()
    if not args.record_only:
        required = [args.checkpoint] if args.offline else [args.checkpoint, args.sam, args.yolo]
        if any(p is None or not p.is_file() for p in required):
            parser.error("Provide existing --checkpoint and, for live inference, --sam and --yolo weights")
    if args.offline:
        if not args.record_only and not args.mask:
            parser.error("Offline inference requires --mask drawn on rectified_rgb.png")
        header = json.loads((args.offline / "header.json").read_text(encoding="utf-8"))
        left = cv2.imread(str(args.offline / "left.png"))
        right = cv2.imread(str(args.offline / "right.png"), cv2.IMREAD_GRAYSCALE)
        if left is None or right is None:
            parser.error("Recorded images missing")
        models = None if args.record_only else Models(args, offline=True)
        print(json.dumps(process(header, cv2.cvtColor(left, cv2.COLOR_BGR2RGB), right,
                                 StereoDepth(), models, args.offline, args.mask), indent=2))
    else:
        serve(args)


if __name__ == "__main__":
    main()
