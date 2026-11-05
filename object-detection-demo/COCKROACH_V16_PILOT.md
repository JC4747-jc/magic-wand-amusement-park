# Cockroach v16 public-photo pilot

This is an optional research profile, not PICO deployment acceptance. Default scene_profiles.json remains the rollback configuration. The lighter model is unchanged. Only Cockroach is routed; auxiliary Beetle is excluded. PICO_PENDING remains visible and must prevent automatic physical-scene routing in Unity.

From the repository root (install object-detection-demo/requirements.txt in your Python environment):

```sh
python object-detection-demo/tcp_server.py --host 127.0.0.1 --port 5017 --scene bugs --profiles object-detection-demo/scene_profiles.cockroach_v16_public_pilot.json --device 0 --imgsz 640 --iou 0.7 --headless
```

Use --device cpu --cpu-threads 1 without CUDA. For recognition-entry rotation, send PICF v2 scene ID 7. The service rotates at most one configured model per frame. Room is a manual scene and excluded from discovery. Startup verifies weight SHA-256 and prints the actual model path/classes. Git LFS checkout is required for best.pt.

Recorded actual local TCP results are in Reports/cockroach-v16-{public-pilot-cuda-tcp-smoke,discovery-cuda-smoke,tcp-latency-summary}.json. CUDA replay:64 responses, mean37.23ms, p95125.65ms; discovery:18 responses and6 bug detections. These include local transport and startup effects and are not headset latency or a new independent accuracy test.

Optional repeat commands (require the original licensed JPEGs in local Datasets/cockroach-fresh-test-v17; raw images are not included in this upload):

```sh
python Tools/MultiScene/tcp_dataset_replay.py --data Datasets/cockroach-fresh-test-v17 --weights object-detection-demo/weights/candidates/cockroach-positive-v16/best.pt --scene bugs --port 5017 --out Reports/repeated-bugs-tcp.json --allowed-classes Cockroach --expected-validation "PUBLIC_PILOT_TEST_PASSED_PICO_PENDING; artifact:LOCAL_TEST_PASSED_PICO_PENDING"
python Tools/MultiScene/cockroach_pilot_discovery_smoke.py --out Reports/repeated-discovery-tcp.json
python -m unittest discover -s object-detection-demo -p 'test_*.py'
```

The discovery replay deliberately uses a known positive image to test routing; it does not measure discovery accuracy. Existing Unity actual-response replay was tested locally with explicitly simulated placement. Unity integration changes are not included in this Python-service commit; no claim of complete Unity/PICO deployment is made. PICO positive-camera evaluation, stereo localization, gesture acceptance and remaining scene models are incomplete.
