"""Verify crop lineage and record the assistant's actual three-overlay review.

This is annotation evidence, not a second independent review or deployment gate.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

from dataset_paths import manifest_relative_path
from source_author_identity import reservation_author_keys

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def main():
    folder = ROOT / "Reports/plant-hanging-crops-v87-boxes"
    destination = folder / "reviewed-three.json"
    assert not destination.exists(), "Preserve existing review"
    source = ROOT / "Datasets/plant-hanging-crop-source-v86"
    manifest = read(source / "manifest.json")
    draft = read(folder / "draft.json")
    assert draft["source_manifest_sha256"] == sha(source / "manifest.json")
    reservation = read(source / "train-reservation.json")
    assert reservation["reserved_split"] == "train"
    assert reservation["manifest_sha256"] == sha(source / "manifest.json")
    reports = {
        name: ROOT / f"Reports/plant-hanging-crops-v86-{name}.json"
        for name in ("identity-history-v2", "creator-links", "pixel-history")
    }
    identity, creator, pixels = (read(reports[k]) for k in reports)
    for report in (identity, pixels):
        assert report["source_manifest_sha256"] == sha(source / "manifest.json")
    assert not identity["errors"]
    assert not creator["collisions"] and not creator["missing_creator_link_indices"]
    assert pixels["scanned_paths"] == pixels["enumerated_paths"] == 21180
    assert not any(pixels[k] for k in ("errors", "matches", "internal_matches"))
    ids = {r["page_id"] for r in manifest["samples"]}
    assert ids == {200088542, 60784237, 134655765}
    groups = {r["group"] for r in manifest["samples"]}
    authors = set().union(*(reservation_author_keys(r) for r in manifest["samples"]))
    assert authors
    history, hits = [], []

    def walk(value, split=None):
        if isinstance(value, dict):
            split = value.get("split") or value.get("reserved_split") or value.get("project_reserved_split") or split
            if split in ("val", "test") and (
                value.get("page_id") in ids or value.get("group") in groups
                or reservation_author_keys(value) & authors
            ):
                hits.append(dict(page_id=value.get("page_id"), group=value.get("group"), split=split))
            for child in value.values():
                walk(child, split)
        elif isinstance(value, list):
            for child in value:
                walk(child, split)

    for filename in ("manifest.json", "audit.json", "selection.json"):
        for path in sorted((ROOT / "Datasets").glob("*/" + filename)):
            history.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
            walk(read(path))
    assert not hits, hits
    proofs = {p.relative_to(ROOT).as_posix(): sha(p) for p in [
        source / "manifest.json", source / "train-reservation.json",
        ROOT / "Reports/plant-hanging-crops-v86-plan.json", folder / "draft.json",
        Path(__file__).resolve(), *reports.values(),
    ]}
    notes = {
        200088542: "One low-angle hanging flower pot with long trailing foliage; bottom clipped. Narrow left crop removes ambiguous separate edge vegetation. The support/background tree is excluded from the target.",
        60784237: "One terracotta hanging fuchsia plant, clipped at top/right; right-side separate distant container removed by crop. Box includes the container and attached flowering foliage.",
        134655765: "One hanging orchid-cactus container with living and dry attached stems; clipped bottom/right. Crop excludes the separate lower-right plant from the full frame.",
    }
    rows = []
    for box in draft["samples"]:
        row = next(r for r in manifest["samples"] if r["page_id"] == box["page_id"])
        derivation = row["derivation"]
        assert box["derivation"] == derivation
        parent_path = ROOT / manifest_relative_path(derivation["source_manifest"])
        parent = read(parent_path)
        assert sha(parent_path) == derivation["source_manifest_sha256"]
        original_reservation_path = parent_path.parent / "train-reservation.json"
        assert sha(original_reservation_path) == derivation["reservation_sha256"]
        parent_reservation = read(original_reservation_path)
        assert parent_reservation["reserved_split"] == "train"
        assert parent_reservation["manifest_sha256"] == sha(parent_path)
        original_row = next(r for r in parent["samples"] if r["page_id"] == row["page_id"])
        assert original_row["group"] == row["group"]
        original = ROOT / manifest_relative_path(derivation["original_image"])
        assert sha(original) == original_row["sha256"] == derivation["original_sha256"]
        image = ROOT / manifest_relative_path(box["image"])
        assert sha(image) == row["sha256"] == box["image_sha256"]
        expected = np.asarray(Image.open(original).convert("RGB").crop(derivation["crop_xyxy"]))
        actual = np.asarray(Image.open(image).convert("RGB"))
        assert expected.shape == actual.shape and np.array_equal(expected, actual)
        match = next(r for r in identity["rows"] if r["page_id"] == row["page_id"])
        assert len(match["historical_hits"]) == 1
        assert match["historical_hits"][0]["manifest"] == parent_path.relative_to(ROOT / "Datasets").as_posix()
        assert match["historical_hits"][0]["group"] == row["group"]
        assert derivation["original_full_frame_hold_retained"]
        for key in ("label", "overlay"):
            path = ROOT / manifest_relative_path(box[key])
            assert sha(path) == box[key + "_sha256"]
        label = (ROOT / manifest_relative_path(box["label"])).read_text().splitlines()
        assert len(label) == 1 and label[0].split()[0] == "0"
        for path in (parent_path, original_reservation_path):
            proofs[path.relative_to(ROOT).as_posix()] = sha(path)
        reviewed = dict(box)
        reviewed.update(overlay_reviewed=True, annotation_approved=True,
                        lossless_crop_verified=True, observation=notes[row["page_id"]])
        rows.append(reviewed)
    result = dict(samples=rows, source_manifest_sha256=sha(source / "manifest.json"),
                  proofs=proofs, historical_inputs=history, cross_val_test_hits=hits,
                  expected_original_parent_identity_hits=3,
                  review_scope="Single assistant actually viewed all three original/cropped images and draft overlays; not an independent second reviewer.",
                  limitations="Crops are TRAIN derivatives of existing sources, not independent sources. Full original frame holds remain. Recorded identities cannot exclude unknown aliases, subjects or pretraining exposure. Dense multi-basket coverage remains incomplete.",
                  training_approved=False, deployment_accepted=False)
    destination.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(dict(reviewed=3, lossless_crops_verified=3,
                          expected_parent_hits=3, cross_val_test_hits=0,
                          historical_documents_checked=len(history),
                          review_sha256=sha(destination), training_approved=False)))


if __name__ == "__main__":
    main()
