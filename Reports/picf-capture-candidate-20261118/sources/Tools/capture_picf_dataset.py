"""Bounded capture-only PICF receiver. No detection model, labels or device attestation."""
import argparse
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
import hashlib
import io
import ipaddress
import json
from pathlib import Path
import re
import shutil
import socket
import struct
import sys
import threading
import time

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/'object-detection-demo'))
from scene_protocol import read_packet, SCENES
from PIL import Image


@dataclass(frozen=True)
class Limits:
    images: int = 24
    jpeg_bytes: int = 2*1024**2
    pixels: int = 4*1024**2
    long_edge: int = 2048
    stored_bytes: int = 32*1024**2
    wire_bytes: int = 64*1024**2
    packets: int = 300
    connections: int = 4
    seconds: float = 120
    packet_seconds: float = 3
    idle_seconds: float = 30
    min_interval: float = 1


class CaptureStop(RuntimeError):
    pass


def sha(data):
    return hashlib.sha256(data).hexdigest()


def inspect_jpeg(data, width, height, limits):
    if not 0 < len(data) <= limits.jpeg_bytes or width*height > limits.pixels or max(width, height) > limits.long_edge:
        raise ValueError('JPEG byte/pixel/dimension limit')
    with Image.open(io.BytesIO(data)) as image:
        if image.format != 'JPEG' or image.size != (width, height) or image.getexif().get(274, 1) != 1:
            raise ValueError('JPEG format/dimensions/orientation mismatch')
        image.load()
        return sha(image.convert('RGB').tobytes())


class PacketReader:
    """Use production parsing, with a single deadline and stricter pre-payload caps."""
    def __init__(self, conn, session):
        self.conn = conn
        self.session = session
        self.deadline = min(time.monotonic()+session.limits.packet_seconds, session.deadline)
        self.position = 0
        self.version = None
        self.wire_scene = None

    def __call__(self, count):
        limits = self.session.limits
        if not 0 < count <= limits.jpeg_bytes:
            raise ValueError('PICF read allocation limit')
        if self.session.wire_bytes+count > limits.wire_bytes:
            raise CaptureStop('wire_byte_cap')
        before = self.position
        data = bytearray()
        while len(data) < count:
            remaining = self.deadline-time.monotonic()
            if remaining <= 0:
                raise TimeoutError('Whole PICF packet deadline')
            self.conn.settimeout(remaining)
            block = self.conn.recv(min(count-len(data), 64*1024))
            if not block:
                raise EOFError('Peer closed PICF stream')
            self.session.wire_bytes += len(block)
            data.extend(block)
        self.position += count
        result = bytes(data)
        if before == 4 and count == 1:
            self.version = result[0]
        elif before == 5 and self.version == 2 and count == 1:
            scene_id = result[0]
            if scene_id >= len(SCENES):
                raise ValueError('Unknown PICF scene')
            self.wire_scene = SCENES[scene_id]
            if self.wire_scene != self.session.scene:
                raise ValueError('Wire scene differs from capture session')
        elif before in (5, 6) and count == 24:
            _, _, width, height, length = struct.unpack('<IqIII', result)
            if not 0 < length <= limits.jpeg_bytes or not 0 < width <= limits.long_edge or not 0 < height <= limits.long_edge or width*height > limits.pixels:
                raise ValueError('PICF header exceeds capture limits before payload read')
        return result


class CaptureSession:
    def __init__(self, output, session_id, scene, declared_source, limits=Limits()):
        if not re.fullmatch(r'[a-z0-9][a-z0-9-]{3,60}', session_id) or scene not in SCENES:
            raise ValueError('Invalid session identity/scene')
        self.output = Path(output).resolve()
        if self.output.exists():
            raise ValueError('Refusing existing capture session')
        self.output.mkdir(parents=True)
        (self.output/'images').mkdir()
        self.session_id, self.scene, self.declared_source = session_id, scene, declared_source
        self.limits = limits
        self.started = time.monotonic()
        self.deadline = self.started+limits.seconds
        self.started_utc = datetime.now(timezone.utc).isoformat()
        self.records, self.events = [], []
        self.seen, self.stored_bytes, self.wire_bytes = set(), 0, 0
        self.packets, self.completed_packets, self.connections = 0, 0, 0
        protocol_path = Path(sys.modules[read_packet.__module__].__file__)
        self.source_hashes = dict(recorder=sha(Path(__file__).read_bytes()), protocol_parser=sha(protocol_path.read_bytes()))
        self.last_saved = float('-inf')
        self.stop_reason = None
        self.endpoint = None
        self.persist()

    def persist(self):
        report = dict(schema=1, purpose='CAPTURE_ONLY_NO_INFERENCE_OR_LABELS', session_id=self.session_id,
                      capture_family=self.session_id, scene=self.scene, declared_source=self.declared_source,
                      device_domain_verified=False, independent_project_test_eligible=False,
                      training_approved=False, split_assigned=False, accepted_for_deployment=False,
                      model_loaded=False, real_yolo_validated=False, labels_created=False,
                      sender_timestamp_scope='Raw sender timestamp; current sender uses UTC after encoding, not exposure time or synchronized latency proof',
                      started_at_utc=self.started_utc, elapsed_seconds=round(time.monotonic()-self.started, 4),
                      endpoint=self.endpoint, limits=asdict(self.limits), source_hashes=self.source_hashes,
                      packet_attempts=self.packets, received_packets=self.completed_packets,
                      received_wire_bytes=self.wire_bytes, stored_image_bytes=self.stored_bytes,
                      connections=self.connections, stop_reason=self.stop_reason, records=self.records, events=self.events)
        raw = (json.dumps(report, indent=2)+'\n').encode()
        if len(raw) > 512*1024:
            raise CaptureStop('manifest_byte_cap')
        temporary = self.output/'session.json.tmp'
        temporary.write_bytes(raw)
        temporary.replace(self.output/'session.json')

    def event(self, kind, **fields):
        if len(self.events) < self.limits.packets+self.limits.connections+4:
            self.events.append(dict(kind=kind, elapsed_seconds=round(time.monotonic()-self.started, 4), **fields))

    def accept_frame(self, packet, connection, peer):
        frame_id, timestamp, width, height, data, wire_scene = packet
        pixel_sha = inspect_jpeg(data, width, height, self.limits)
        digest = sha(data)
        saved, reason = False, None
        if digest in self.seen:
            reason = 'duplicate_jpeg_sha256'
        elif time.monotonic()-self.last_saved < self.limits.min_interval:
            reason = 'sampling_interval'
        else:
            if len(self.records) >= self.limits.images or self.stored_bytes+len(data) > self.limits.stored_bytes:
                raise CaptureStop('stored_image_or_byte_cap')
            if shutil.disk_usage(self.output).free < 20*1024**3:
                raise CaptureStop('20_GiB_disk_floor')
            name = f'images/{len(self.records)+1:04d}.jpg'
            with (self.output/name).open('xb') as stream:
                stream.write(data)
            self.records.append(dict(image=name, sha256=digest, pixel_sha256=pixel_sha, bytes=len(data),
                width=width, height=height, frame_id=frame_id, sender_timestamp_ms=timestamp,
                received_at_utc=datetime.now(timezone.utc).isoformat(), connection=connection,
                peer=peer, wire_scene=wire_scene, scene_evidence='wire_profile_only' if wire_scene else 'operator_session_only',
                capture_family=self.session_id, annotation_status='UNREVIEWED_NO_LABEL',
                target_presence_verified=False, device_domain_verified=False, independent_project_test_eligible=False))
            self.stored_bytes += len(data)
            self.seen.add(digest)
            self.last_saved = time.monotonic()
            saved = True
        self.event('frame', frame_id=frame_id, saved=saved, reason=reason)
        self.persist()
        return dict(frame_id=frame_id, timestamp=timestamp, width=width, height=height, detections=[],
                    mode='capture_only_no_inference', saved=saved, reason=reason,
                    device_domain_verified=False)


def validate_network(host, peer):
    address = ipaddress.ip_address(host)
    networks = [ipaddress.ip_network(x) for x in ('10.0.0.0/8', '172.16.0.0/12', '192.168.0.0/16')]
    if address.version != 4 or not (address.is_loopback or any(address in n for n in networks)):
        raise ValueError('Bind only explicit loopback or RFC1918 IPv4, never all interfaces')
    if not address.is_loopback and not peer:
        raise ValueError('LAN capture requires an explicit expected peer IPv4')
    expected = ipaddress.IPv4Address(peer) if peer else ipaddress.IPv4Address('127.0.0.1')
    if not (expected.is_loopback or any(expected in n for n in networks)):
        raise ValueError('Expected peer must be explicit loopback or RFC1918 IPv4')
    if address.is_loopback and not expected.is_loopback:
        raise ValueError('Loopback listener cannot attest a LAN peer')
    return str(address), str(expected)


def serve(session, host='127.0.0.1', port=15055, peer=None, ready=None, stop_after_disconnect=False):
    host, expected_peer = validate_network(host, peer)
    last_activity = time.monotonic()
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as listener:
            # No SO_REUSEADDR on Windows: do not replace an existing listener.
            if hasattr(socket, 'SO_EXCLUSIVEADDRUSE'):
                listener.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
            listener.bind((host, port))
            listener.listen(1)
            listener.settimeout(.2)
            session.endpoint = list(listener.getsockname())
            session.persist()
            if ready:
                ready(listener.getsockname())
            while time.monotonic() < session.deadline:
                if time.monotonic()-last_activity > session.limits.idle_seconds:
                    session.stop_reason = 'idle_timeout'; break
                if session.connections >= session.limits.connections:
                    session.stop_reason = 'connection_cap'; break
                try:
                    conn, remote = listener.accept()
                except socket.timeout:
                    continue
                session.connections += 1
                if remote[0] != expected_peer:
                    conn.close(); session.event('unexpected_peer', peer=remote[0]); continue
                previous_id = None
                with conn:
                    try:
                        while time.monotonic() < session.deadline:
                            if session.packets >= session.limits.packets:
                                raise CaptureStop('packet_cap')
                            session.packets += 1
                            packet = read_packet(PacketReader(conn, session))
                            session.completed_packets += 1
                            if previous_id is not None and packet[0] <= previous_id:
                                raise ValueError('Non-increasing frame ID within connection')
                            previous_id = packet[0]
                            last_activity = time.monotonic()
                            response = session.accept_frame(packet, session.connections, remote[0])
                            remaining = session.deadline-time.monotonic()
                            if remaining <= 0:
                                raise CaptureStop('session_timeout')
                            conn.settimeout(min(1, remaining))
                            conn.sendall((json.dumps(response)+'\n').encode())
                            if len(session.records) >= session.limits.images:
                                raise CaptureStop('image_cap')
                    except (EOFError, ValueError, OSError, TimeoutError) as error:
                        session.event('connection_closed', error=type(error).__name__+': '+str(error)[:180])
                session.persist()
                if stop_after_disconnect:
                    session.stop_reason = 'selftest_peer_disconnected'; break
            if session.stop_reason is None:
                session.stop_reason = 'session_timeout'
    except CaptureStop as stop:
        session.stop_reason = str(stop)
    finally:
        session.persist()


def fixture_jpeg(color):
    stream = io.BytesIO()
    Image.new('RGB', (64, 48), color).save(stream, 'JPEG')
    return stream.getvalue()


def fixture_packet(jpeg, frame_id=1, version=1, scene=1, width=64, height=48):
    return b'PICF'+bytes([version])+(bytes([scene]) if version == 2 else b'')+struct.pack('<IqIII', frame_id, 1700000000000, width, height, len(jpeg))+jpeg


def selftest(output, session_id):
    session = CaptureSession(output, session_id, 'tree', 'editor_synthetic',
                             Limits(images=4, seconds=5, packet_seconds=1, idle_seconds=1, min_interval=0))
    errors, responses = [], []
    red, green = fixture_jpeg('red'), fixture_jpeg('green')
    def client(address):
        try:
            with socket.create_connection(address, timeout=1) as conn:
                with conn.makefile('rb') as stream:
                    for frame_id, version, jpeg in [(1, 1, red), (2, 2, green), (3, 2, green)]:
                        conn.sendall(fixture_packet(jpeg, frame_id, version))
                        responses.append(json.loads(stream.readline(4096)))
        except BaseException as error:
            errors.append(error)
    worker = None
    def ready(address):
        nonlocal worker
        worker = threading.Thread(target=client, args=(address,), daemon=True); worker.start()
    serve(session, port=0, ready=ready, stop_after_disconnect=True)
    worker.join(2)
    if worker.is_alive() or errors or len(session.records) != 2 or len(responses) != 3:
        raise RuntimeError('Capture-only loopback self-test failed: '+str(errors))
    if [r['saved'] for r in responses] != [True, True, False] or any(r['detections'] for r in responses):
        raise RuntimeError('Capture response is not empty/sampling-safe')
    for row in session.records:
        data = (session.output/row['image']).read_bytes()
        if sha(data) != row['sha256'] or row['device_domain_verified'] or row['independent_project_test_eligible']:
            raise RuntimeError('Fixture bytes or provenance state mismatch')
    return dict(syntheticLoopbackPass=True, protocolVersions=[1, 2], packets=3, images=2,
                duplicateNotStored=True, realDeviceValidated=False, yoloInferenceValidated=False,
                heavyImports=[n for n in ('torch', 'ultralytics', 'numpy') if n in sys.modules])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--session-id', required=True)
    parser.add_argument('--scene', choices=SCENES, default='tree')
    parser.add_argument('--declared-source', choices=['unknown', 'editor_synthetic', 'pico_camera_claimed'], default='unknown')
    parser.add_argument('--host', default='127.0.0.1')
    parser.add_argument('--peer')
    parser.add_argument('--port', type=int, default=15055)
    parser.add_argument('--self-test', action='store_true')
    args = parser.parse_args()
    if not re.fullmatch(r'[a-z0-9][a-z0-9-]{3,60}', args.session_id) or not 1024 <= args.port <= 65535 or args.port == 5005:
        parser.error('Safe session ID and a separate non-production port required')
    output = ROOT/'.codex-tmp/device-capture'/args.session_id
    if args.self_test:
        result = selftest(output, args.session_id)
    else:
        validate_network(args.host, args.peer)
        session = CaptureSession(output, args.session_id, args.scene, args.declared_source)
        serve(session, args.host, args.port, args.peer)
        result = dict(captureOnlyFinished=True, images=len(session.records), stopReason=session.stop_reason,
                      realDeviceValidated=False, yoloInferenceValidated=False)
    print(json.dumps(result))


if __name__ == '__main__':
    main()
