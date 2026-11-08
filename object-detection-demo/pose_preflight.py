"""Read-only environment probe. Exit 2 means live F2G inference is not ready."""
import argparse
import importlib.util
import json
import os
import platform
import sys
from pathlib import Path


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--checkpoint",type=Path)
    parser.add_argument("--sam",type=Path)
    parser.add_argument("--output",type=Path)
    args=parser.parse_args()
    report={"platform":platform.platform(),"python":sys.version,"executable":sys.executable,"blockers":[]}
    if platform.system()!="Linux":
        report["blockers"].append("Official F2G runtime requires Linux; Windows recording works independently")
    for name in ["numpy","cv2","torch","f2g_pose","pointnet2_ops","segment_anything","ultralytics"]:
        present=importlib.util.find_spec(name) is not None
        report[name]=present
        if not present: report["blockers"].append("Missing module: "+name)
    if report["torch"]:
        try:
            import torch
            report["torch_version"]=torch.__version__
            report["cuda_version"]=torch.version.cuda
            report["cuda_available"]=torch.cuda.is_available()
            if torch.cuda.is_available():
                p=torch.cuda.get_device_properties(0)
                report["gpu"]={"name":p.name,"vram_mb":round(p.total_memory/1024**2),"capability":torch.cuda.get_device_capability(0)}
                report["cuda_kernel_probe"]=(torch.ones(4,device="cuda")*2).sum().item()==8
            else: report["blockers"].append("CUDA unavailable")
        except Exception as exc: report["blockers"].append("CUDA probe: "+str(exc))
    for name,path in [("f2g_checkpoint",args.checkpoint),("sam_checkpoint",args.sam),
                      ("radio_checkpoint",Path(os.environ.get("F2G_RADIO_CHECKPOINT","__missing__"))),
                      ("radio_repository",Path(os.environ.get("F2G_RADIO_REPOSITORY","__missing__")))]:
        present=path is not None and (path.is_dir() if name=="radio_repository" else path.is_file())
        report[name]=str(path) if present else None
        if not present: report["blockers"].append("Missing "+name)
    report["live_ready"]=not report["blockers"]
    text=json.dumps(report,ensure_ascii=False,indent=2)
    print(text)
    if args.output:
        args.output.parent.mkdir(parents=True,exist_ok=True)
        args.output.write_text(text,encoding="utf-8")
    return 0 if report["live_ready"] else 2


if __name__=="__main__":
    raise SystemExit(main())
