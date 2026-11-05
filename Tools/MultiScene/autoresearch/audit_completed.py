"""Verify terminal experiment evidence without running inference or changing weights."""
import argparse
import csv
import json
import math
from pathlib import Path

from prepare import ROOT, sha, verify
from runtime_checkpoint_selection import rank_report


def read(path):
    return json.loads(path.read_text())


def require(condition, message):
    if not condition:
        raise ValueError(message)


def audit_trial(folder, record, epochs, dataset_sha):
    require(record['status'] in {'COMPLETED_VAL_CANDIDATE', 'REJECTED_VALIDATION'},
            'Trial did not complete successfully: ' + folder.name)
    selection = read(folder / 'runtime-selection/selection.json')
    require(selection['dataset_sha256'] == dataset_sha, 'Selection dataset mismatch')
    require(len(selection['records']) == epochs, 'Missing epoch evaluations')
    best_rank = None
    selected_epoch = None
    for epoch, item in enumerate(selection['records'], 1):
        path = folder / f'runtime-selection/epoch-{epoch:03d}/report.json'
        report = read(path)
        require(item['epoch'] == epoch, 'Nonsequential epoch records')
        require(item['report_sha256'] == sha(path), 'Epoch report hash mismatch')
        require(report['sha256'] == item['checkpoint_sha256'], 'Checkpoint identity mismatch')
        require(report['dataset_version'] == dataset_sha, 'Epoch dataset mismatch')
        rank = rank_report(report)
        require(list(rank) == item['rank'], 'Stored rank mismatch')
        selected = best_rank is None or rank > best_rank
        require(item['selected'] == selected, 'Checkpoint selection policy mismatch')
        if selected:
            best_rank, selected_epoch = rank, epoch
    with (folder / 'train/results.csv').open(newline='') as stream:
        rows = list(csv.DictReader(stream))
    require([int(row['epoch'].strip()) for row in rows] == list(range(1, epochs + 1)),
            'Training epoch budget mismatch')
    weight = folder / 'train/weights/best.pt'
    weight_sha = sha(weight)
    require(weight_sha == record['weight_sha256'], 'Final weight hash mismatch')
    require(weight_sha == sha(folder / 'runtime-selection/runtime-best.pt'),
            'Final weight is not the stripped runtime selection')
    require(record['selected_epoch'] == selected_epoch, 'Selected epoch mismatch')
    final = read(folder / 'final-val/report.json')
    require(final['sha256'] == weight_sha and final['dataset_version'] == dataset_sha,
            'Final evaluation identity mismatch')
    final_rank = rank_report(final)
    require(all(math.isclose(a, b, rel_tol=1e-9, abs_tol=1e-9)
                for a, b in zip(final_rank, best_rank)), 'Final re-evaluation differs from selected epoch')
    require(list(final_rank) == record['rank'], 'Ledger rank mismatch')
    require(not record.get('deployment_accepted') and not record['service_routed'],
            'Research trial must not claim deployment')
    expected_status = 'COMPLETED_VAL_CANDIDATE' if final_rank[0] else 'REJECTED_VALIDATION'
    require(record['status'] == expected_status, 'Gate status mismatch')
    target = next(row for row in final['operating_per_class'] if row['name'] == 'PottedPlant')
    return dict(name=folder.name, selected_epoch=selected_epoch, weight_sha256=weight_sha,
                rank=final_rank, operating_metrics=target,
                mean_inference_ms=final['mean_inference_ms'],
                status=expected_status, deployment_accepted=False)


def audit(output):
    contract = read(output / 'contract.json')
    verify(contract)
    plan = read(output / 'plan.json')
    require((output / 'ledger.json').is_file(), 'No terminal trial ledger yet')
    ledger = read(output / 'ledger.json')
    dataset_sha = sha(ROOT / contract['dataset'] / 'audit.json')
    require(len(ledger['trials']) == len(plan['trials']), 'Experiments still running or missing trials')
    results = []
    baseline_run = None
    for trial, record in zip(plan['trials'], ledger['trials']):
        require(trial['name'] == record['name'], 'Trial order mismatch')
        require(trial['overrides'] == record['overrides'], 'Trial overrides mismatch')
        folder = output / trial['name']
        require(read(folder / 'state.json') == record, 'Trial state and ledger disagree')
        run = read(folder / 'run.json')
        require(run['dataset_version'] == dataset_sha, 'Training dataset mismatch')
        require(run['contract_sha256'] == sha(output / 'contract.json'), 'Training contract mismatch')
        require(run['parameters']['epochs'] == plan['epochs'], 'Configured epoch budget mismatch')
        if baseline_run is None:
            require(not trial['overrides'], 'First trial must be the unmodified baseline')
            baseline_run = run
        else:
            require(run['initializer_sha256'] == baseline_run['initializer_sha256'],
                    'Initializer changed between trials')
            expected = dict(baseline_run['parameters'])
            expected.update(trial['overrides'])
            for key, value in expected.items():
                if key not in {'project', 'name'}:
                    require(run['parameters'][key] == value, 'Unplanned parameter change: ' + key)
            require(set(run['parameters']) == set(expected), 'Training parameter set changed')
        for key, value in trial['overrides'].items():
            require(run['parameters'][key] == value, 'Configured override mismatch: ' + key)
        results.append(audit_trial(folder, record, plan['epochs'], dataset_sha))
    winner = max(results, key=lambda result: tuple(result['rank']))
    require(ledger['best_development_candidate']['name'] == winner['name'], 'Winner mismatch')
    require(ledger['best_development_candidate']['sha256'] == winner['weight_sha256'], 'Winner hash mismatch')
    require(not ledger['fresh_test_performed'] and not ledger['deployment_accepted'],
            'Unexpected test or deployment claim')
    return dict(status='COMPLETED_EVIDENCE_VERIFIED', trials=results,
                best_development_candidate=winner['name'], fresh_test_performed=False,
                deployment_accepted=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', required=True, help='Completed experiment directory under Reports')
    args = parser.parse_args()
    output = (ROOT / args.out).resolve()
    require(output.is_relative_to(ROOT / 'Reports'), 'Reports output required')
    print(json.dumps(audit(output), indent=2))
