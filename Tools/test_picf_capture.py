"""Actual socket, byte-preservation and provenance checks for capture-only tooling."""
from dataclasses import replace
import io
import json
from pathlib import Path
import socket
import struct
import tempfile
import threading
import time
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from PIL import Image
from capture_picf_dataset import (CaptureSession, CaptureStop, Limits, PacketReader, fixture_jpeg,
                                  fixture_packet, inspect_jpeg, read_packet, selftest, serve, sha, validate_network)


class PicfCaptureChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='magicmr-picf-test-')
        self.root = Path(self.temp.name).resolve()
        self.assertTrue(self.root.name.startswith('magicmr-picf-test-'))
        self.session = CaptureSession(self.root/'session', 'fixture-session', 'tree', 'editor_synthetic', Limits(min_interval=0))
        self.jpeg = fixture_jpeg('red')

    def tearDown(self):
        self.temp.cleanup()

    def packet(self, jpeg=None, frame_id=1, wire_scene=None):
        return (frame_id, 1700000000000, 64, 48, jpeg or self.jpeg, wire_scene)

    def read_wire(self, data):
        left, right = socket.socketpair()
        with left, right:
            right.sendall(data); right.shutdown(socket.SHUT_WR)
            return read_packet(PacketReader(left, self.session))

    def test_production_parser_v1_v2_fragmented_headers(self):
        for version in (1, 2):
            left, right = socket.socketpair()
            wire = fixture_packet(self.jpeg, version=version)
            with left, right:
                def sender():
                    for index in range(0, len(wire), 7):
                        right.sendall(wire[index:index+7])
                worker = threading.Thread(target=sender);worker.start()
                parsed = read_packet(PacketReader(left, self.session));worker.join(1)
                self.assertFalse(worker.is_alive())
                self.assertEqual(parsed[:4], (1, 1700000000000, 64, 48))
                self.assertEqual(parsed[4], self.jpeg)
                self.assertEqual(parsed[5], 'tree' if version == 2 else None)

    def test_oversized_payload_rejected_after_header_before_payload_read(self):
        wire = b'PICF\x01'+struct.pack('<IqIII', 1, 1, 64, 48, self.session.limits.jpeg_bytes+1)
        with self.assertRaisesRegex(ValueError, 'before payload'):
            self.read_wire(wire)
        self.assertEqual(self.session.wire_bytes, 29)

    def test_pixel_and_long_edge_caps_before_payload_read(self):
        for width, height in [(2049, 1), (0, 48)]:
            wire = b'PICF\x01'+struct.pack('<IqIII', 1, 1, width, height, 1)
            with self.assertRaises(ValueError): self.read_wire(wire)

    def test_scene_mismatch_rejected_before_jpeg_allocation(self):
        with self.assertRaisesRegex(ValueError, 'scene differs'):
            self.read_wire(fixture_packet(self.jpeg, version=2, scene=5))
        self.assertEqual(self.session.wire_bytes, 6)

    def test_bad_magic_or_version_cannot_be_captured(self):
        for wire in (b'BAD!', b'PICF\x03'):
            with self.assertRaises(ValueError): self.read_wire(wire)
        self.assertEqual(self.session.records, [])

    def test_truncated_payload_does_not_create_partial_jpeg(self):
        with self.assertRaises(EOFError): self.read_wire(fixture_packet(self.jpeg)[:-10])
        self.assertEqual(list((self.session.output/'images').iterdir()), [])

    def test_whole_packet_deadline_not_reset_by_trickle(self):
        self.session.limits = replace(self.session.limits, packet_seconds=.15)
        left, right = socket.socketpair(); stop = threading.Event()
        wire = fixture_packet(self.jpeg)
        with left, right:
            right.sendall(wire[:29])
            def sender():
                for byte in wire[29:]:
                    if stop.wait(.04): break
                    try: right.sendall(bytes([byte]))
                    except OSError: break
            worker = threading.Thread(target=sender, daemon=True);worker.start()
            started = time.monotonic()
            try:
                with self.assertRaises((TimeoutError, socket.timeout)):
                    read_packet(PacketReader(left, self.session))
                self.assertLess(time.monotonic()-started, 1)
            finally:
                stop.set();worker.join(1)
            self.assertFalse(worker.is_alive())

    def test_wire_budget_checked_before_next_allocation(self):
        self.session.limits = replace(self.session.limits, wire_bytes=28)
        with self.assertRaises(CaptureStop): self.read_wire(fixture_packet(self.jpeg))
        self.assertEqual(self.session.wire_bytes, 5)

    def test_header_dimensions_must_match_actual_jpeg(self):
        with self.assertRaises(ValueError): inspect_jpeg(self.jpeg, 63, 48, Limits())

    def test_non_jpeg_content_rejected(self):
        stream = io.BytesIO();Image.new('RGB', (64, 48)).save(stream, 'PNG')
        with self.assertRaises(ValueError): inspect_jpeg(stream.getvalue(), 64, 48, Limits())

    def test_duplicate_jpeg_not_saved_and_no_fake_detections(self):
        first = self.session.accept_frame(self.packet(), 1, '127.0.0.1')
        second = self.session.accept_frame(self.packet(frame_id=2), 1, '127.0.0.1')
        self.assertTrue(first['saved']);self.assertFalse(second['saved'])
        self.assertEqual(second['reason'], 'duplicate_jpeg_sha256')
        self.assertEqual(first['detections'], [])
        self.assertEqual(len(self.session.records), 1)

    def test_sampling_interval_skips_different_frame_without_label(self):
        self.session.limits = replace(self.session.limits, min_interval=100)
        self.session.accept_frame(self.packet(), 1, '127.0.0.1')
        result = self.session.accept_frame(self.packet(fixture_jpeg('green'), 2), 1, '127.0.0.1')
        self.assertFalse(result['saved']);self.assertEqual(result['reason'], 'sampling_interval')
        self.assertFalse((self.session.output/'labels').exists())

    def test_image_and_stored_byte_caps_do_not_overrun(self):
        self.session.limits = replace(self.session.limits, stored_bytes=len(self.jpeg)-1)
        with self.assertRaises(CaptureStop): self.session.accept_frame(self.packet(), 1, '127.0.0.1')
        self.assertEqual(self.session.records, [])
        self.session.limits = replace(self.session.limits, stored_bytes=1_000_000, images=1)
        self.session.accept_frame(self.packet(), 1, '127.0.0.1')
        with self.assertRaises(CaptureStop):
            self.session.accept_frame(self.packet(fixture_jpeg('green'), 2), 1, '127.0.0.1')
        self.assertEqual(len(list((self.session.output/'images').iterdir())), 1)

    def test_disk_floor_stops_before_jpeg_write(self):
        with patch('capture_picf_dataset.shutil.disk_usage', return_value=SimpleNamespace(free=10*1024**3)):
            with self.assertRaisesRegex(CaptureStop, 'disk_floor'):
                self.session.accept_frame(self.packet(), 1, '127.0.0.1')
        self.assertEqual(list((self.session.output/'images').iterdir()), [])

    def test_existing_session_not_overwritten(self):
        before = (self.session.output/'session.json').read_bytes()
        with self.assertRaises(ValueError):
            CaptureSession(self.session.output, 'fixture-session', 'tree', 'unknown')
        self.assertEqual((self.session.output/'session.json').read_bytes(), before)

    def test_claimed_pico_origin_never_auto_approved(self):
        session = CaptureSession(self.root/'claimed', 'claimed-camera', 'tree', 'pico_camera_claimed')
        session.accept_frame(self.packet(), 1, '127.0.0.1')
        report = json.loads((session.output/'session.json').read_text('utf-8'))
        for key in ('device_domain_verified', 'independent_project_test_eligible', 'training_approved', 'real_yolo_validated'):
            self.assertIs(report[key], False)
        self.assertIs(report['records'][0]['target_presence_verified'], False)

    def test_v1_scene_is_operator_label_and_not_ground_truth(self):
        self.session.accept_frame(self.packet(), 1, '127.0.0.1')
        record = self.session.records[0]
        self.assertEqual(record['scene_evidence'], 'operator_session_only')
        self.assertIsNone(record['wire_scene'])
        self.assertFalse(record['target_presence_verified'])

    def test_all_frames_in_same_capture_family_across_connections(self):
        self.session.accept_frame(self.packet(), 1, '127.0.0.1')
        self.session.accept_frame(self.packet(fixture_jpeg('green'), 1), 2, '127.0.0.1')
        self.assertEqual({r['capture_family'] for r in self.session.records}, {'fixture-session'})
        self.assertEqual({r['connection'] for r in self.session.records}, {1, 2})

    def test_loopback_and_explicit_private_peer_only(self):
        self.assertEqual(validate_network('127.0.0.1', None), ('127.0.0.1', '127.0.0.1'))
        self.assertEqual(validate_network('192.168.1.10', '192.168.1.20'), ('192.168.1.10', '192.168.1.20'))
        for host, peer in [('0.0.0.0', None), ('8.8.8.8', None), ('192.168.1.10', None), ('192.168.1.10', '8.8.8.8')]:
            with self.assertRaises(ValueError): validate_network(host, peer)

    def test_actual_loopback_v1_v2_ack_bytes_and_no_provenance_promotion(self):
        result = selftest(self.root/'loopback', 'loopback-fixture')
        self.assertTrue(result['syntheticLoopbackPass'])
        report = json.loads((self.root/'loopback/session.json').read_text('utf-8'))
        self.assertEqual(report['received_packets'], 3)
        self.assertEqual(len(report['records']), 2)
        self.assertEqual([r['wire_scene'] for r in report['records']], [None, 'tree'])
        self.assertFalse(report['device_domain_verified'])
        for row in report['records']:
            self.assertEqual(sha((self.root/'loopback'/row['image']).read_bytes()), row['sha256'])

    def test_actual_loopback_decreasing_frame_id_closes_connection(self):
        responses, errors = [], []
        session = CaptureSession(self.root/'order', 'order-fixture', 'tree', 'editor_synthetic',
                                 Limits(seconds=3, packet_seconds=.5, idle_seconds=1, min_interval=0))
        worker = None
        def client(address):
            try:
                with socket.create_connection(address, timeout=1) as conn, conn.makefile('rb') as stream:
                    conn.sendall(fixture_packet(self.jpeg, frame_id=2));responses.append(stream.readline(4096))
                    conn.sendall(fixture_packet(fixture_jpeg('green'), frame_id=1));responses.append(stream.readline(4096))
            except BaseException as error: errors.append(error)
        def ready(address):
            nonlocal worker
            worker = threading.Thread(target=client, args=(address,), daemon=True);worker.start()
        serve(session, port=0, ready=ready, stop_after_disconnect=True);worker.join(1)
        self.assertFalse(worker.is_alive());self.assertEqual(errors, [])
        self.assertEqual(len(session.records), 1);self.assertEqual(responses[1], b'')
        self.assertTrue(any('Non-increasing' in e.get('error', '') for e in session.events))


if __name__ == '__main__':
    unittest.main()
