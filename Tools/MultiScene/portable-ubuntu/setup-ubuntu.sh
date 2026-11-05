#!/usr/bin/env bash
set -euo pipefail
workspace_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$workspace_dir"
python3 -m venv .venv-linux
.venv-linux/bin/python -m pip install --upgrade pip
.venv-linux/bin/python -m pip install -r requirements.txt
.venv-linux/bin/python -c 'import torch; print("PyTorch:", torch.__version__, "CUDA available:", torch.cuda.is_available())'
printf '%s\n' 'Environment ready. See README.txt for dataset/weight names and run --check first.'
