"""Frozen one-model prospective diagnostic; no training, selection or deployment."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path
from dataset_paths import manifest_relative_path

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    data = ROOT / 'Datasets/plant-validation-extension-v46'
    model = ROOT / 'object-detection-demo/weights/candidates/plant-cut-v36/best.pt'
    out = ROOT / 'Reports/plant-v47-prospective-baseline'
    protocol = ROOT / 'Reports/plant-v47-prospective-baseline-protocol.json'
    assert not protocol.exists() and not out.exists(), 'Preserve existing experiment'
    assert sha(model) == '9778ae05560fcdc3882a56a303783b3e3c3b8471a7c39cba22e77af20ea32a82'
    assert sha(data / 'audit.json') == '5c18bc75c706af67d9bdcb20d4197f31fc41791fcb5e58220bc8f1ac615db66a'
    audit = json.loads((data / 'audit.json').read_text())
    evaluator = ROOT / 'Tools/MultiScene/evaluate_mapped.py'
    paths = {model, evaluator, Path(__file__).resolve(), data / 'audit.json', data / 'review_hold.json', data / 'data.yaml'}
    for row in audit['samples']:
        for key, digest in [('image', 'sha256'), ('label', 'label_sha256')]:
            path = data / manifest_relative_path(row[key])
            assert sha(path) == row[digest]
            paths.add(path)
    files = [dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in sorted(paths)]
    plan = dict(mode='DIAGNOSTIC_ONLY_OPEN_ANNOTATION_HOLD', model_count=1, training_runs=0,
        rationale='Measure unchanged previously rejected v36 selected checkpoint on prospective coverage, not choose a checkpoint or repair its rejection.',
        evaluation=dict(split='val', images=30, classes=['PottedPlant', 'CutArrangement'], confidence=.35,
                        imgsz=640, rect=True, nms_iou=.7, max_det=8),
        reporting='Full30 VAL and original13 cohort plus each new cohort. No confidence/checkpoint search.',
        selection='NONE: one fixed historical model, diagnostic only. Historical rejection remains.',
        future_selection_policy='Before any training: separate frozen bounded plan; PottedPlant P>=.9 R>=.8 on fullVAL and old13cohort; report per-cohort errors and auxiliary metrics. Independent TEST required after selection.',
        budget=dict(max_evaluation_passes=1, gpu_training_epochs=0), files=files,
        deployment_accepted=False, pico_validation='NOT_PERFORMED')
    protocol.write_text(json.dumps(plan, indent=2), encoding='utf-8')
    subprocess.run([sys.executable, '-X', 'utf8', str(evaluator), '--model', str(model), '--data', str(data),
        '--out', str(out), '--split', 'val', '--imgsz', '640', '--operating-conf', '.35',
        '--max-det', '8', '--nms-iou', '.7'], cwd=ROOT, check=True)
    for row in files:
        assert sha(ROOT / row['path']) == row['sha256'], row['path']
    report = json.loads((out / 'report.json').read_text())
    assert report['images'] == 30 and report['sha256'] == sha(model)
    result = dict(status='DIAGNOSTIC_COMPLETE_NOT_ACCEPTED', frozen_files_verified=len(files),
                  protocol_sha256=sha(protocol), report_sha256=sha(out / 'report.json'),
                  deployment_accepted=False, historical_rejection_preserved=True)
    (out / 'completed-audit.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
