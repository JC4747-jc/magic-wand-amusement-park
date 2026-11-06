"""Package the completed diagnostic only after actual audit and visual review."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def main():
    run = ROOT/'Reports/plant-v89-hanging-diagnostic'
    data = ROOT/'Datasets/plant-hanging-v88'
    completed = read(run/'completed.json')
    audit = read(run/'independent-audit.json')
    comparison = read(ROOT/'Reports/plant-v89-hanging-comparison.json')
    review = read(run/'error-review.json')
    report = run/'final-val/report.json'
    best = run/'train/weights/best.pt'
    assert sha(best) == completed['weight_sha256'] == audit['stripped_best_sha256']
    assert review['report_sha256'] == completed['report_sha256'] == sha(report)
    assert review['weight_sha256'] == sha(best)
    assert all(r['actually_viewed'] for r in review['reviewed'])
    assert comparison['results']['v89']['weight_sha256'] == sha(best)
    # An unsuccessful diagnostic stays rejected; a passing candidate still needs TEST.
    status = 'LOCAL_TEST_PENDING' if comparison['results']['v89']['runtime_gate_pass'] else 'REJECTED_VALIDATION'
    protocol = read(run/'protocol.json')
    compatibility = run/'run.json'
    assert not compatibility.exists(), 'Preserve existing packaging record'
    compatibility.write_text(json.dumps(dict(
        status='POST_RUN_PACKAGING_COMPATIBILITY_RECORD',
        dataset_sha256=sha(data/'audit.json'), parameters=protocol['parameters'],
        initializer_sha256=protocol['initializer_sha256'], selection=protocol['selection'],
        source_protocol='protocol.json', source_protocol_sha256=sha(run/'protocol.json'),
        generated_after_training=True, deployment_accepted=False), indent=2), encoding='utf-8')
    subprocess.run([sys.executable, '-X', 'utf8', str(ROOT/'Tools/MultiScene/package_candidate.py'),
                    '--run', str(run), '--data', str(data), '--validation', str(report),
                    '--out', str(ROOT/'object-detection-demo/weights/candidates/plant-hanging-v89'),
                    '--status', status, '--allowed-classes', 'PottedPlant'], cwd=ROOT, check=True)


if __name__ == '__main__':
    main()
