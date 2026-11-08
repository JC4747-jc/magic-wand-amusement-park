# PICO real lighter debug window

Double-click `Start-Lighter-Debug.cmd`. Default: saved v6 best.pt, confidence 0.399, max_det 1, TCP 5005. No deployed weights or Unity scenes are changed.

1. Stop the other PC YOLO server: only one server can use port 5005.
2. For USB with APK host 127.0.0.1, run `adb reverse tcp:5005 tcp:5005` with the intended headset connected. For LAN, configure the APK with the PC IP and allow the port on your trusted network.
3. Launch the existing PICO APK with camera permission and frame sending enabled. This viewer does not activate the headset camera remotely.
4. The window shows a waiting screen, then PICO frames with bounding boxes, confidence, center/box pixel coordinates, output FPS and predict-call time. Disconnections are accepted again. Stale images are explicitly marked.
5. Q/Esc or window close exits. S saves the displayed annotated screenshot under `Find lighter/debug_captures`.

The response protocol is unchanged: detections are returned to Unity, so the running APK may continue its normal anchor/interaction behavior. This is an alternative server, not a passive observer beside another server.

Pixel locations are NOT 3D world positions. Predict-call timing is NOT headset-to-display latency, and confidence is NOT measured accuracy. A bounding box does not prove the Unity stereo anchor is correct.

Optional command-line arguments: `--weights "absolute/path/to/best.pt" --conf 0.399 --device auto --port 5005`. A different port also requires matching APK/ADB configuration.

This entry point does not start the separate pose-availability service. Use the standard application launcher for unrelated full-body pose features.
