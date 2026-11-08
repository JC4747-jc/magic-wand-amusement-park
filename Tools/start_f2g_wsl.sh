#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
runtime_dir=/opt/f2g/runtime
export F2G_RADIO_REPOSITORY="$runtime_dir/RADIO"
export F2G_RADIO_CHECKPOINT="$runtime_dir/weights/radio-v2.5-l_half.pth.tar"
exec /opt/f2g/venv/bin/python -u "$project_dir/object-detection-demo/pose_bridge.py" \
  --host 127.0.0.1 --port 5006 --health-port 5007 \
  --checkpoint "$runtime_dir/weights/f2g-pose.pt" \
  --sam "$runtime_dir/weights/sam_vit_b_01ec64.pth" \
  --native-bottom-up --warmup-sample "$runtime_dir/F2G-Pose/examples/rope" "$@"
