"""Freeze a small two-class candidate experiment by reviewed provenance components."""
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path
import random

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / 'TrainingData/gap-candidate-split-v1.json'


def main():
    annotation_path = ROOT / 'TrainingData/public-gap-annotations-v1.json'
    raw = annotation_path.read_bytes()
    annotations = json.loads(raw)
    audit_path = ROOT / '.codex-tmp/yolo-training/candidate-group-audit.json'
    audit_bytes = audit_path.read_bytes()
    audit = json.loads(audit_bytes)
    chain = next(r for r in audit['source_manifests'] if r['path'] == annotation_path.relative_to(ROOT).as_posix())
    assert chain['sha256'] == hashlib.sha256(raw).hexdigest(), 'Grouping audit is stale'
    by_key = {r['key']: r for r in audit['records']}
    names = ['Cockroach', 'BronzeBeast']
    groups = defaultdict(list)
    for r in annotations['records']:
        if r['eligible_for_candidate_training']:
            assert r['status'] == 'BOX_REVIEWED_CANDIDATE_ONLY'
            assert len(r['boxes']) == 1 and r['boxes'][0]['class'] in names
            assert by_key[r['source_key']]['sha256'] == r['sha256']
            groups[by_key[r['source_key']]['component_id']].append(r)
    assignments = {}
    rng = random.Random(42)
    for category in names:
        candidates = [key for key, rows in groups.items() if any(r['boxes'][0]['class'] == category for r in rows)]
        assert len(candidates) >= 3
        rng.shuffle(candidates)
        candidates.sort(key=lambda k: len(groups[k]))  # Keep largest component available for training.
        held = max(1, round(len(candidates) * .15))
        assert len(candidates) > 2 * held
        for i, key in enumerate(candidates):
            split = 'valid' if i < held else 'test' if i < held*2 else 'train'
            assert key not in assignments or assignments[key] == split
            assignments[key] = split
    records = []
    for component, rows in groups.items():
        for r in rows:
            records.append({'key': r['source_key'], 'source_path': r['local_path'], 'sha256': r['sha256'],
                'size': r['size'], 'class': r['boxes'][0]['class'], 'class_id': names.index(r['boxes'][0]['class']),
                'xyxy_normalized': r['boxes'][0]['xyxy_normalized'], 'component_id': component, 'split': assignments[component]})
    counts = {s: dict(Counter(r['class'] for r in records if r['split'] == s)) for s in ('train','valid','test')}
    assert len(records) == 23 and all(set(v) == set(names) for v in counts.values())
    report = {'version': 1, 'purpose': 'SMALL_TWO_CLASS_CANDIDATE_EXPERIMENT_NOT_FULL_PROJECT_TRAINING_ACCEPTANCE',
        'seed': 42, 'names': names, 'annotation_sha256': hashlib.sha256(raw).hexdigest(),
        'group_audit_sha256': hashlib.sha256(audit_bytes).hexdigest(), 'split_counts': counts,
        'physical_provenance_components': len(groups), 'group_overlap_between_splits': False,
        'test_frozen_before_training': True, 'accepted_for_deployment': False,
        'limitations': ['Very small public-photo holdout: metrics are exploratory and do not establish deployment quality.',
            'Other eleven project classes remain required; this experiment does not replace their training/data work.',
            'No real PICO image-domain accuracy or full capture-independence claim beyond the documented conservative grouping.'],
        'records': sorted(records, key=lambda r: (r['split'],r['key']))}
    if TARGET.exists():
        raise RuntimeError('Refusing to overwrite frozen candidate split')
    TARGET.write_text(json.dumps(report, indent=2), encoding='utf8')
    print(json.dumps({'split_counts': counts, 'components': len(groups), 'records': len(records)}))


if __name__ == '__main__':
    main()
