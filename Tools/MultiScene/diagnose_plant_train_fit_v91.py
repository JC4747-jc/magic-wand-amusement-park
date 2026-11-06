"""Evaluate fixed v89 on TRAIN only; neither checkpoint selection nor acceptance."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path
from dataset_paths import manifest_relative_path as rel

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    data = ROOT/'Datasets/plant-hanging-v88'
    model = ROOT/'object-detection-demo/weights/candidates/plant-hanging-v89/best.pt'
    out = ROOT/'Reports/plant-v91-training-fit'
    assert not out.exists(), 'Preserve immutable or partial results'
    assert sha(model) == '49e7c0332e4cd1988070893fa3b13b1aed92bf279073c54f0bbf55ca443a5d04'
    assert sha(data/'audit.json') == 'efee464040393a27bc61eb94eb3de4a1abf90506f0f2d5faf3b43cd31c2dc8c1'
    audit = json.loads((data/'audit.json').read_text())
    rows = [r for r in audit['samples'] if r['split'] == 'train']
    assert len(rows) == 166
    files = {model, data/'audit.json', Path(__file__).resolve(), ROOT/'Tools/MultiScene/evaluate_mapped.py'}
    for row in rows:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            path = data/rel(row[key])
            assert sha(path) == row[digest]
            files.add(path)
    selected = audit['samples'][194:]
    assert len(selected) == 6 and all(r['split'] == 'train' for r in selected)
    out.mkdir()
    protocol = dict(status='PRE_INFERENCE_TRAIN_FIT_DIAGNOSTIC',
                    files=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in sorted(files)],
                    split='train', images=166, added_hanging_images=[r['image'] for r in selected],
                    operating_confidence=.35, imgsz=640, rect=True, max_det=8, nms_iou=.7,
                    checkpoint_selection=False, test_used=False, deployment_accepted=False,
                    limitation='Training fit only; previously observed TRAIN is not generalization evidence.')
    (out/'protocol.json').write_text(json.dumps(protocol, indent=2), encoding='utf-8')
    subprocess.run([sys.executable, '-X', 'utf8', str(ROOT/'Tools/MultiScene/evaluate_mapped.py'),
                    '--model', str(model), '--data', str(data), '--out', str(out/'evaluation'),
                    '--split', 'train', '--imgsz', '640', '--operating-conf', '.35',
                    '--allowed-classes', 'PottedPlant', '--max-det', '8', '--nms-iou', '.7'], cwd=ROOT, check=True)
    for row in protocol['files']:
        assert sha(ROOT/row['path']) == row['sha256']
    reportpath = out/'evaluation/report.json'
    report = json.loads(reportpath.read_text())
    assert report['images'] == 166 and report['split'] == 'train' and report['sha256'] == sha(model)
    stems = {Path(p).stem for p in protocol['added_hanging_images']}
    subset = [r for r in report['per_image'] if Path(r['image'].replace(chr(92), '/')).stem in stems]
    assert len(subset) == 6 and sum(r['ground_truth'] for r in subset) == 6
    result = dict(status='ACTUAL_TRAIN_FIT_DIAGNOSTIC_COMPLETE', operating_per_class=report['operating_per_class'],
                  added_hanging_images=subset, report_sha256=sha(reportpath), protocol_sha256=sha(out/'protocol.json'),
                  frozen_files_verified=len(protocol['files']), test_used=False, deployment_accepted=False,
                  limitation='Training fit does not prove held-out-view coverage or independent generalization.')
    (out/'findings.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
