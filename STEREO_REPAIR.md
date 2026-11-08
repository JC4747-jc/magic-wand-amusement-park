# Position and summon repair — 1.10.1

This diagnostic build restores the independent 1.10.0 YOLO + stereo position pipeline. It contains **no F2GPoseClient**, opens no pose connection, and preserves the existing 3-frame visual average, stereo rejection thresholds, and 1.5-second reply timeout. The YOLO server retains the reconnect and newest-frame fixes covered by `test_tcp_reconnect.py`.

## Build and run

- Unity menu: `Bridge / Build Position Repair APK (Pose Off)`.
- Already-open editor: create `.codex-tmp/build-repair.request`; completion is written to `.codex-tmp/build-repair.result`. The editor must have no unsaved scene changes and must not be in Play mode.
- APK: `Builds/Android/BridgeStereoRepair.apk`.
- App: **Magic MR Stereo Repair**, `com.yn.picmagicmr.stereo.repair`, version 1.10.1 / code 25. Installs alongside the original `.stereo`, experimental `.stereo.pose`, and Park packages.
- Start the existing GPU detector: `object-detection-demo/.venv-gpu/Scripts/python.exe -u object-detection-demo/tcp_server.py --headless`.
- Connect the headset and run `adb reverse tcp:5005 tcp:5005`. No port 5006 or pose server is needed.
- Close the other camera apps before opening Repair. Keep the real lighter upright and stationary until `READY`, then use the right-hand thumbs-up gesture. Reset clears registration; transient vision occlusion does not revoke an already verified registration.

## Evidence

The panel separates YOLO confidence / box size, stereo measurement, and reply capture age. Logs include `[StereoMeasure]` and rejected `[StereoReply]` metadata. `NoReliableLighterDepth` retains its existing meaning but now includes the underlying match reason. Per-sample reasons distinguish low texture, ambiguous correspondence, search-limit hits, and out-of-bounds patches. These diagnostics do not relax depth acceptance.

The repair build records at most 60 detected pairs (one per second) and 6 no-detection/timeout pairs (one per five seconds) per launch. One background disk job is allowed at a time. Each JSON file and its two PGM images correspond to the exact stereo pair retained for that YOLO request, with native image coordinates, frame ID, timestamps, calibrated intrinsics, baseline, capture pose, and sample results. No new image stream or pose inference is introduced.

Pull after a test (device storage may differ):

```powershell
adb pull /sdcard/Android/data/com.yn.picmagicmr.stereo.repair/files/stereo-diagnostics .codex-tmp/repair-evidence
object-detection-demo/.venv-gpu/Scripts/python.exe Tools/inspect_stereo_evidence.py .codex-tmp/repair-evidence --output .codex-tmp/repair-review
```

The same bounded filenames are reused on the next launch, keeping disk usage near 40 MB. The inspection tool filters to the latest session, draws the detector box on the native left image, and draws accepted correspondences on the right image. It does not rerun YOLO on a different frame or infer a cause from unrelated camera images. Pull evidence after the app is idle so a write is not in progress.

## Acceptance

Build checks cover stereo depth, texture rejection, diagnostic parity, reply identity and age, visual averaging, and summon eligibility after occlusion. Python transport tests cover successive sessions, worker failure, and the newest-frame regression.

Hardware acceptance still requires repeated reset → real-lighter registration → thumbs-up → virtual-lighter appearance, alignment during head movement, and a detector reconnect test. A successful build or a depth computed from background objects is not sufficient evidence of restored lighter positioning. F2G rotation remains a separate later integration step.

## Deployment record — 2026-10-30

- Unity build: `SUCCESS`; stereo, 3-frame average, gesture, and summon-after-occlusion checks passed.
- Python: all 14 tests in `test_tcp_reconnect` and `test_pose_bridge` passed.
- Installed successfully on PICO `PA921AMGKA310157G`; verified version 1.10.1/code 25 and camera permission. Port 5005 reverse configured; the existing YOLO server was started after the newest-frame fix was saved.
- APK SHA-256: `80118871155C62A08B5155AD3897103395C83C3B4E84A4B6ECD4D6B6DFF3BB9F`.
- The user deferred the headset test. The repair app was not launched for this test; no new real-lighter captures or successful summon are claimed.
