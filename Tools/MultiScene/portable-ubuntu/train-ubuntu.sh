#!/usr/bin/env bash
set -euo pipefail
workspace_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$workspace_dir"
if [[ ! -x .venv-linux/bin/python ]]; then
    printf '%s\n' 'Linux environment missing. Run: bash setup-ubuntu.sh' >&2
    exit 2
fi
exec .venv-linux/bin/python train.py "$@"
