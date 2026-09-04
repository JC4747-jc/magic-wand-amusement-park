"""Generate same-dataset, two-color curve comparisons for YOLO26n v3 and v4."""

from __future__ import annotations

from pathlib import Path
import csv

import matplotlib.pyplot as plt
import numpy as np
from ultralytics import YOLO


ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "Find lighter" / "detect_merged_v3_quad" / "data.yaml"
RUNS = ROOT / "Find lighter" / "runs" / "detect"
OUT = RUNS / "lighter-yolo26n-v3-v4-comparison"
MODELS = {
    "v3 (previous)": RUNS / "lighter-yolo26n-quad-stage2" / "weights" / "best.pt",
    "v4 (50 epochs)": RUNS / "lighter-yolo26n-quad-v4-50e" / "weights" / "best.pt",
}
COLORS = {"v3 (previous)": "#2563eb", "v4 (50 epochs)": "#f97316"}


def test_curves() -> None:
    curve_data = {}
    metric_rows = []
    for label, weights in MODELS.items():
        metrics = YOLO(str(weights)).val(
            data=str(DATA), split="test", imgsz=640, batch=16, device=0,
            plots=False, verbose=False,
        )
        curve_data[label] = metrics.curves_results
        metric_rows.append({
            "model": label,
            "precision": metrics.box.mp,
            "recall": metrics.box.mr,
            "mAP50": metrics.box.map50,
            "mAP50-95": metrics.box.map,
        })

    fig, axes = plt.subplots(2, 2, figsize=(14, 10), dpi=180)
    titles = ["Precision–Recall", "F1–Confidence", "Precision–Confidence", "Recall–Confidence"]
    for index, (axis, title) in enumerate(zip(axes.flat, titles)):
        for label in MODELS:
            x, y, x_name, y_name = curve_data[label][index]
            axis.plot(x, y[0], color=COLORS[label], linewidth=2.5, label=label)
        axis.set_title(title)
        axis.set_xlabel(x_name)
        axis.set_ylabel(y_name)
        axis.set_xlim(0, 1)
        axis.set_ylim(0, 1)
        axis.grid(alpha=0.25)
        axis.legend()
    fig.suptitle("YOLO26n Lighter Detection — Same Test Set Curve Comparison", fontsize=16)
    fig.tight_layout()
    fig.savefig(OUT / "test_curves_v3_vs_v4.png", bbox_inches="tight")
    plt.close(fig)

    metric_names = ["precision", "recall", "mAP50", "mAP50-95"]
    with (OUT / "test_metrics_v3_vs_v4.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=["model", *metric_names])
        writer.writeheader()
        writer.writerows(metric_rows)
    fig, ax = plt.subplots(figsize=(11, 6))
    positions = np.arange(len(metric_rows))
    width = 0.18
    bar_colors = ["#0ea5e9", "#22c55e", "#a855f7", "#ef4444"]
    for index, metric_name in enumerate(metric_names):
        bars = ax.bar(positions + (index - 1.5) * width, [row[metric_name] for row in metric_rows], width,
                      label=metric_name, color=bar_colors[index])
        ax.bar_label(bars, fmt="%.3f", padding=2, fontsize=8)
    ax.set_xticks(positions, [row["model"] for row in metric_rows])
    ax.set_ylim(0.75, 1.0)
    ax.set_ylabel("Score")
    ax.set_title("Independent Test Metrics")
    ax.grid(axis="y", alpha=0.25)
    ax.legend()
    fig.tight_layout()
    fig.savefig(OUT / "test_metrics_v3_vs_v4.png", dpi=180, bbox_inches="tight")
    plt.close(fig)


def training_dashboard() -> None:
    csvs = {
        "v3 (previous)": RUNS / "lighter-yolo26n-quad-stage2" / "results.csv",
        "v4 (50 epochs)": RUNS / "lighter-yolo26n-quad-v4-50e" / "results.csv",
    }
    columns = [
        "train/box_loss", "train/cls_loss", "train/l1_loss",
        "metrics/precision(B)", "metrics/recall(B)",
        "val/box_loss", "val/cls_loss", "val/l1_loss",
        "metrics/mAP50(B)", "metrics/mAP50-95(B)",
    ]
    titles = [
        "Train Box Loss", "Train Class Loss", "Train L1 Loss",
        "Validation Precision", "Validation Recall",
        "Validation Box Loss", "Validation Class Loss", "Validation L1 Loss",
        "Validation mAP50", "Validation mAP50-95",
    ]
    frames = {}
    for label, path in csvs.items():
        with path.open(newline="", encoding="utf-8-sig") as stream:
            frames[label] = [{key.strip(): float(value) for key, value in row.items()} for row in csv.DictReader(stream)]
    fig, axes = plt.subplots(2, 5, figsize=(22, 8), dpi=160)
    for axis, column, title in zip(axes.flat, columns, titles):
        for label, rows in frames.items():
            axis.plot([row["epoch"] for row in rows], [row[column] for row in rows],
                      color=COLORS[label], linewidth=2, label=label)
        axis.set_title(title)
        axis.set_xlabel("Epoch")
        axis.grid(alpha=0.25)
        axis.legend(fontsize=8)
    fig.suptitle("Training and Validation Curves — v3 vs v4", fontsize=17)
    fig.tight_layout()
    fig.savefig(OUT / "training_dashboard_v3_vs_v4.png", bbox_inches="tight")
    plt.close(fig)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    test_curves()
    training_dashboard()
    print(OUT)


if __name__ == "__main__":
    main()
