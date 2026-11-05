"""Verify completed v11 frozen inputs and every runtime checkpoint decision."""
import csv
import json
from pathlib import Path

from statue_runtime_checkpoint_selection import rank_report, sha

ROOT = Path(__file__).resolve().parents[2]


def main():
    run = ROOT / 'Reports/statue-unfrozen-runtime-v15-training'
    spec = json.loads((run / 'run.json').read_text())
    done = json.loads((run / 'completion.json').read_text())
    selection = json.loads((run / 'runtime-selection/selection.json').read_text())
    for item in spec['frozen_inputs']:
        assert sha(ROOT / item['path']) == item['sha256'], item['path']
    records = selection['records']
    assert len(records) == 20
    best_rank = None
    best_record = None
    for epoch, record in enumerate(records, 1):
        assert record['epoch'] == epoch
        path = run / f'runtime-selection/epoch-{epoch:03d}/report.json'
        assert sha(path) == record['report_sha256']
        report = json.loads(path.read_text())
        assert report['sha256'] == record['checkpoint_sha256']
        assert report['dataset_version'] == spec['dataset_version']
        score = rank_report(report)
        assert list(score) == record['rank']
        selected = best_rank is None or score > best_rank
        assert selected == record['selected']
        if selected:
            best_rank, best_record = score, record
    assert sha(run / 'runtime-selection/runtime-best.pt') == best_record['checkpoint_sha256'] == done['raw_selected_sha256'] == selection['best_sha256']
    assert sha(run / 'train/weights/best.pt') == sha(run / 'runtime-selection/selected-stripped.pt') == done['sha256']
    assert list(best_rank) == done['rank']
    assert done['frozen_inputs_verified'] == len(spec['frozen_inputs'])
    with (run / 'train/results.csv').open(newline='') as handle:
        rows = list(csv.DictReader(handle))
    assert len(rows) == 20 and int(rows[-1]['epoch']) == 20
    assert (run / 'native-fitness-best.pt').is_file()
    output = ROOT / 'Reports/statue-unfrozen-runtime-v15-completed-audit.json'
    assert not output.exists()
    output.write_text(json.dumps(dict(status='PASS', epochs=20,
        selected_epoch=best_record['epoch'], rank=best_rank,
        frozen_inputs_verified=len(spec['frozen_inputs']), best_sha256=done['sha256'],
        native_best_sha256=sha(run / 'native-fitness-best.pt'),
        elapsed_training_csv_seconds=float(rows[-1]['time']),
        deployment_accepted=False, final_validation_required=True), indent=2))
    print(output.read_text())


if __name__ == '__main__':
    main()
