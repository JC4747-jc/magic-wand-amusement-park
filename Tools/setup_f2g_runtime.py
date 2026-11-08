"""Install pinned F2G sources into an EXISTING Linux CUDA Python environment.

Does not install WSL, system packages, or replace PyTorch. Run from a dedicated
virtual environment with a working CUDA torch/torchvision and nvcc first.
"""
import argparse
import hashlib
import os
import platform
import shutil
import subprocess
import sys
from pathlib import Path

F2G_REV = "6141733a61d9a6e764bb0a72d2c5ecde06adc1db"
RADIO_REV = "fbd19ec1e68483482d7d96a59cb639880a8b33ed"


def run(*command, cwd=None, env=None):
    subprocess.run([str(x) for x in command], cwd=cwd, env=env, check=True)


def checkout(url, path, revision):
    if path.exists():
        actual = subprocess.check_output(["git", "-C", str(path), "rev-parse", "HEAD"], text=True).strip()
        if actual != revision:
            raise RuntimeError(f"Existing checkout differs: {path}; use a new --root")
        dirty = subprocess.check_output(["git", "-C", str(path), "status", "--porcelain"], text=True).strip()
        if dirty:
            raise RuntimeError(f"Checkout has local modifications: {path}; use a new --root")
        return
    run("git", "clone", "--no-checkout", url, path)
    run("git", "-C", path, "checkout", "--detach", revision)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1]/".codex-tmp/f2g-runtime")
    args = parser.parse_args()
    if platform.system() != "Linux":
        parser.error("Run inside Linux/WSL2; this script does not install or change the Windows OS")
    if sys.prefix == sys.base_prefix:
        parser.error("Activate a dedicated Python virtual environment first")
    if not shutil.which("nvcc") or not shutil.which("git"):
        parser.error("Install the compatible CUDA development toolkit and git first")
    import torch
    import torchvision
    if not torch.cuda.is_available():
        parser.error("CUDA PyTorch is not ready")
    # Run a real kernel, including on SM 12.0 GPUs, before installing anything.
    assert (torch.ones(4, device="cuda")*2).sum().item() == 8
    print("Using", torch.__version__, torchvision.__version__, torch.cuda.get_device_name(0))
    root = args.root.resolve(); root.mkdir(parents=True, exist_ok=True)
    source, radio, weights = root/"F2G-Pose", root/"RADIO", root/"weights"
    checkout("https://github.com/jrryzh/F2G-Pose.git", source, F2G_REV)
    checkout("https://github.com/NVlabs/RADIO.git", radio, RADIO_REV)
    constraints = root/"torch-constraints.txt"
    constraints.write_text(f"torch=={torch.__version__}\ntorchvision=={torchvision.__version__}\n")
    # Deliberately retain the already verified PyTorch/torchvision pair. Official
    # torch 2.4/CUDA 12.4 pins target older GPUs; SM12 compatibility needs validation.
    run(sys.executable,"-m","pip","install","-c",constraints,"numpy==2.1.2","scipy==1.15.3","Pillow>=10",
        "opencv-python==4.12.0.88","PyYAML==6.0.2","easydict==1.13","timm==1.0.22",
        "einops==0.8.1","cutoop==0.1.0","tqdm>=4.66","ultralytics","segment-anything","ninja","wheel")
    environment = os.environ.copy()
    # Recent CUDA wheels ship math-library headers outside CUDA_HOME. Reuse the
    # exact libraries installed with torch rather than installing a second toolkit.
    nvidia_packages = Path(torch.__file__).resolve().parent.parent / "nvidia"
    wheel_includes = sorted(str(p) for p in nvidia_packages.glob("*/include") if p.is_dir())
    if wheel_includes:
        environment["CPATH"] = os.pathsep.join(wheel_includes +
            ([environment["CPATH"]] if environment.get("CPATH") else []))
    major, minor = torch.cuda.get_device_capability(0)
    environment["TORCH_CUDA_ARCH_LIST"] = f"{major}.{minor}"
    environment.setdefault("MAX_JOBS", "4")
    run(sys.executable,"-m","pip","install","--no-build-isolation","--no-deps",
        source/"third_party/pointnet2_ops",source/"third_party/chamfer",env=environment)
    run(sys.executable,"-m","pip","install","--no-deps","-e",source)
    weights.mkdir(exist_ok=True)
    downloads = [
        ("https://huggingface.co/J3rr1/F2G-Pose/resolve/main/f2g-pose.pt?download=true",
         "bff06bd58a42fd805d8232f9ab7b4612b8eb833edf6b6cccd23e5040a8c9ca18","f2g-pose.pt"),
        ("https://huggingface.co/nvidia/RADIO/resolve/main/radio-v2.5-l_half.pth.tar?download=true",
         "50324e8cb086885126a896ad9ecbac46355cf0e4c1d8363763b01693a4618ff1","radio-v2.5-l_half.pth.tar"),
        ("https://dl.fbaipublicfiles.com/segment_anything/sam_vit_b_01ec64.pth",
         "ec2df62732614e57411cdcf32a23ffdf28910380d03139ee0f4fcbe91eb8c912","sam_vit_b_01ec64.pth")]
    for url, sha, filename in downloads:
        existing = weights/filename
        if existing.is_file():
            with existing.open("rb") as stream:
                if hashlib.file_digest(stream, "sha256").hexdigest() != sha:
                    raise RuntimeError("Existing weight hash mismatch: " + str(existing))
            continue
        run(sys.executable,source/"scripts/download_weights.py","--url",url,"--sha256",sha,"--output",weights/filename)
    import shlex
    (root/"environment.sh").write_text(
        "export F2G_RADIO_REPOSITORY="+shlex.quote(str(radio))+"\n"+
        "export F2G_RADIO_CHECKPOINT="+shlex.quote(str(weights/"radio-v2.5-l_half.pth.tar"))+"\n")
    print("Setup complete; source",root/"environment.sh","then run pose_preflight.py and the official sample.")
    print("Custom newer CUDA/PyTorch compatibility and VRAM must still be validated with actual inference.")


if __name__ == "__main__":
    main()
