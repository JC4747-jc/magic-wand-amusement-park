#!/usr/bin/env bash
# Dedicated D-drive WSL runtime for the current RTX4050. Never installs GPU drivers.
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
export DEBIAN_FRONTEND=noninteractive
export MAX_JOBS=1
export PIP_NO_CACHE_DIR=1
export OMP_NUM_THREADS=2
export CMAKE_BUILD_PARALLEL_LEVEL=1
export MKL_NUM_THREADS=2
export MAMBA_ROOT_PREFIX=/opt/f2g/mamba
# Requires the dedicated D-drive WSL2 distro to be running. Fail before
# downloads if the Linux filesystem is nearly full.
available_kb=$(df -Pk /opt | awk 'NR==2 {print $4}')
if (( available_kb < 20 * 1024 * 1024 )); then
  echo 'At least 20 GiB free in the WSL filesystem is required.' >&2
  exit 2
fi
# Check the actual host drive as well as WSL's much larger virtual maximum.
if [[ -n "${WSL_DISTRO_NAME:-}" ]]; then
    host_free_bytes=$(powershell.exe -NoProfile -NonInteractive -Command '(Get-PSDrive -Name D).Free' | tr -d '\r\n')
    if [[ ! "$host_free_bytes" =~ ^[0-9]+$ ]] || (( host_free_bytes < 25 * 1024 * 1024 * 1024 )); then
        echo 'The physical Windows D drive requires at least 25 GiB free.' >&2
        exit 2
    fi
fi
apt-get update
apt-get install -y --no-install-recommends gcc-12 g++-12 git ca-certificates bzip2 curl
mkdir -p /opt/f2g/tools
archive=/opt/f2g/tools/micromamba-2.3.3-0.tar.bz2
if [[ ! -f "$archive" ]]; then
  curl -fL --retry 2 --max-time 180 'https://conda.anaconda.org/conda-forge/linux-64/micromamba-2.3.3-0.tar.bz2' -o "$archive"
fi
echo "e7274528ceb9c20d048a428d6c22d7e02e268f8ffb762c4c365422347c8b8ba2  $archive" | sha256sum -c -
tar -xjf "$archive" -C /opt/f2g/tools bin/micromamba
if [[ ! -x /opt/f2g/toolchain/bin/python ]]; then
  /opt/f2g/tools/bin/micromamba create -y -p /opt/f2g/toolchain --override-channels \
    -c nvidia/label/cuda-12.4.1 -c conda-forge --strict-channel-priority \
    python=3.10.19 cuda-nvcc=12.4.131 cuda-cudart-dev=12.4.127 cuda-cccl=12.4.127
fi
export CUDA_HOME=/opt/f2g/toolchain
export PATH="$CUDA_HOME/bin:$PATH"
export CC=/usr/bin/gcc-12 CXX=/usr/bin/g++-12
"$CUDA_HOME/bin/nvcc" --version
if [[ ! -x /opt/f2g/venv/bin/python ]]; then
  /opt/f2g/toolchain/bin/python -m venv /opt/f2g/venv
fi
/opt/f2g/venv/bin/python -m pip install 'pip>=24,<26' setuptools==75.8.0 wheel==0.48.0
/opt/f2g/venv/bin/python -m pip install torch==2.4.0 torchvision==0.19.0 --index-url https://download.pytorch.org/whl/cu124
/opt/f2g/venv/bin/python -c 'import torch; print(torch.__version__, torch.version.cuda, torch.cuda.get_device_name(0)); assert (torch.ones(4,device="cuda")*2).sum().item()==8; print("CUDA_KERNEL_PASS")'
/opt/f2g/venv/bin/python "$project_dir/Tools/setup_f2g_runtime.py" --root /opt/f2g/runtime
echo F2G_LINUX_SETUP_COMPLETE
