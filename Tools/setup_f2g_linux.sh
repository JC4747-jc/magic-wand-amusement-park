#!/usr/bin/env bash
# Dedicated WSL environment. Requires the authorized D-drive Ubuntu installation.
set -euo pipefail
export DEBIAN_FRONTEND=noninteractive
mkdir -p /opt/f2g
cd /opt/f2g
curl -fL --retry 3 https://developer.download.nvidia.com/compute/cuda/repos/ubuntu2404/x86_64/cuda-keyring_1.1-1_all.deb -o cuda-keyring.deb
dpkg -i cuda-keyring.deb
apt-get update
# Toolkit packages only; never install the Linux GPU driver under WSL.
apt-get install -y cuda-nvcc-13-0 cuda-cudart-dev-13-0
export CUDA_HOME=/usr/local/cuda-13.0
export PATH="$CUDA_HOME/bin:$PATH"
python3 -m venv /opt/f2g/venv
/opt/f2g/venv/bin/python -m pip install --upgrade pip wheel setuptools
/opt/f2g/venv/bin/python -m pip install torch torchvision --index-url https://download.pytorch.org/whl/cu130
/opt/f2g/venv/bin/python -c 'import torch; print(torch.__version__, torch.version.cuda, torch.cuda.get_device_name(0)); assert (torch.ones(4,device="cuda")*2).sum().item()==8; print("CUDA_KERNEL_PASS")'
/opt/f2g/venv/bin/python /mnt/d/Unity/Project/magic-wand-amusement-park/Tools/setup_f2g_runtime.py --root /opt/f2g/runtime
echo F2G_LINUX_SETUP_COMPLETE
