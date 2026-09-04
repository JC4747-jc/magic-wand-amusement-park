"""Build a clean YOLO detection dataset from the existing and QUAD exports."""

from __future__ import annotations

import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OLD = ROOT / "Find lighter" / "detect_merged_v2"
QUAD = ROOT / "Find lighter" / "quad_lighter_v1_yolo26"
OUT = ROOT / "Find lighter" / "detect_merged_v3_quad"
SPLITS = ("train", "valid", "test")
IMAGE_SUFFIXES = {".jpg", ".jpeg", ".png", ".webp"}


def bbox_line(line: str) -> str:
    values = line.split()
    if len(values) == 5:
        cls, cx, cy, width, height = values
        return f"{int(float(cls))} {float(cx):.8f} {float(cy):.8f} {float(width):.8f} {float(height):.8f}"
    if len(values) < 7 or len(values) % 2 == 0:
        raise ValueError(f"Unsupported YOLO annotation with {len(values)} values: {line}")

    cls = int(float(values[0]))
    coords = [float(value) for value in values[1:]]
    xs = coords[0::2]
    ys = coords[1::2]
    x1, x2 = max(0.0, min(xs)), min(1.0, max(xs))
    y1, y2 = max(0.0, min(ys)), min(1.0, max(ys))
    return f"{cls} {(x1 + x2) / 2:.8f} {(y1 + y2) / 2:.8f} {x2 - x1:.8f} {y2 - y1:.8f}"


def add_source(source: Path, prefix: str, split: str) -> tuple[int, int]:
    image_dir = source / split / "images"
    label_dir = source / split / "labels"
    out_images = OUT / split / "images"
    out_labels = OUT / split / "labels"
    images = 0
    boxes = 0

    for image in sorted(image_dir.iterdir()):
        if image.suffix.lower() not in IMAGE_SUFFIXES:
            continue
        label = label_dir / f"{image.stem}.txt"
        if not label.is_file():
            raise FileNotFoundError(f"Missing label for {image}")
        destination_stem = f"{prefix}_{image.stem}"
        shutil.copy2(image, out_images / f"{destination_stem}{image.suffix.lower()}")
        converted = [bbox_line(line) for line in label.read_text(encoding="utf-8").splitlines() if line.strip()]
        (out_labels / f"{destination_stem}.txt").write_text("\n".join(converted) + ("\n" if converted else ""), encoding="utf-8")
        images += 1
        boxes += len(converted)
    return images, boxes


def main() -> None:
    if OUT.exists():
        raise SystemExit(f"Output already exists; refusing to overwrite: {OUT}")
    for split in SPLITS:
        (OUT / split / "images").mkdir(parents=True)
        (OUT / split / "labels").mkdir(parents=True)

    for split in SPLITS:
        old_count = add_source(OLD, "old", split)
        quad_count = add_source(QUAD, "quad", split)
        print(f"{split}: old={old_count}, quad={quad_count}, total_images={old_count[0] + quad_count[0]}")

    yaml = f"""path: {OUT.as_posix()}
train: train/images
val: valid/images
test: test/images

nc: 1
names: ['lighter']
"""
    (OUT / "data.yaml").write_text(yaml, encoding="utf-8")
    print(f"Created {OUT}")


if __name__ == "__main__":
    main()
