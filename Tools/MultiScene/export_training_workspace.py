"""Copy reviewed datasets and selected weights into a relocatable training workspace."""
import argparse
import copy
import hashlib
import importlib.metadata
import json
import re
import shutil
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def relative(value):
    path = PurePosixPath(value.replace('\\', '/'))
    if path.is_absolute() or '..' in path.parts or ':' in str(path):
        raise ValueError('Dataset paths must stay relative: ' + value)
    return path.as_posix()


def pairs(values):
    result = {}
    for value in values:
        name, location = value.split('=', 1)
        if not re.fullmatch(r'[A-Za-z0-9_-]+', name) or name in result:
            raise ValueError('Unique portable name required: ' + name)
        result[name] = (ROOT / location).resolve()
    return result


def reviewed_split_lists(source, schema, rows):
    """Keep explicit split membership, including auxiliary-label subsets."""
    lists = {}
    for split in ('train', 'val', 'test'):
        value = schema.get(split)
        if not value or Path(value).suffix.lower() != '.txt':
            continue
        path = source / relative(value)
        allowed = {row['image'] for row in rows if row['split'] == split}
        entries = [relative(line.strip()) for line in path.read_text(encoding='utf-8').splitlines() if line.strip()]
        if not entries or len(set(entries)) != len(entries) or not set(entries).issubset(allowed):
            raise ValueError('Split list is empty, duplicated, or crosses reviewed folds: ' + str(path))
        lists[relative(value)] = entries
    return lists


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', required=True)
    parser.add_argument('--dataset', action='append', required=True, help='name=dataset_directory; repeatable')
    parser.add_argument('--weight', action='append', required=True, help='name=weight.pt; repeatable')
    parser.add_argument('--clip-cache', help='Parent directory containing clip/ViT-B-32.pt for offline World training')
    args = parser.parse_args()
    out = Path(args.out).resolve()
    datasets, weights = pairs(args.dataset), pairs(args.weight)
    if out.exists():
        raise FileExistsError('Export is immutable; choose a new directory')
    if any(out.is_relative_to(path) or path.is_relative_to(out) for path in datasets.values()):
        raise ValueError('Export must not overlap source datasets')
    prepared = []
    for name, source in datasets.items():
        audit_file = source / 'audit.json'
        audit = json.loads(audit_file.read_text(encoding='utf-8'))
        if audit.get('training_approved') is False:
            raise ValueError('Dataset is explicitly withheld from training: ' + str(source))
        if audit.get('training_approved') is None and (not audit.get('sources') or
                not all(s.get('reviewed') is True for s in audit['sources'])):
            raise ValueError('Legacy dataset needs reviewed sources: ' + str(source))
        for key in ['reviewed_inputs', 'new_reviewed_inputs']:
            for evidence in audit.get(key, []):
                if sha(ROOT / relative(evidence['path'])) != evidence['sha256']:
                    raise ValueError('Source review evidence changed')
        hold = source / 'review_hold.json'
        if hold.exists() and json.loads(hold.read_text(encoding='utf-8-sig')).get('status') == 'OPEN':
            raise ValueError('Dataset has an open review hold')
        rows = copy.deepcopy(audit['samples'])
        for row in rows:
            for key, digest_key in [('image', 'sha256'), ('label', 'label_sha256')]:
                row[key] = relative(row[key])
                path = (source / row[key]).resolve()
                if not path.is_relative_to(source) or sha(path) != row[digest_key]:
                    raise ValueError('Dataset input SHA/path mismatch: ' + str(path))
        schema_text = (source / 'data.yaml').read_text(encoding='utf-8')
        try:
            schema = json.loads(schema_text)
        except ValueError:
            import yaml
            schema = yaml.safe_load(schema_text)
        for key in ['train', 'val', 'test']:
            if schema.get(key):
                if not isinstance(schema[key], str):
                    raise ValueError('Only directory-based split schemas supported')
                schema[key] = relative(schema[key])
        split_lists = reviewed_split_lists(source, schema, rows)
        prepared.append((name, source, audit, rows, schema, split_lists))
    for path in weights.values():
        if not path.is_file() or path.suffix != '.pt':
            raise ValueError('Expected existing .pt weight: ' + str(path))
    clip = Path(args.clip_cache).resolve() / 'clip/ViT-B-32.pt' if args.clip_cache else None
    if clip and not clip.is_file():
        raise FileNotFoundError(clip)
    out.mkdir(parents=True)
    config = dict(format_version=1, datasets={}, weights={}, payload_sha256={},
                  notes=['Paths resolved relative to this workspace, historical source metadata may retain Windows paths',
                         'Original dataset/source files and Windows environments are not moved or modified'])
    for name, source, audit, rows, schema, split_lists in prepared:
        target = out / 'Datasets' / name
        target.mkdir(parents=True)
        shutil.copy2(source / 'audit.json', target / 'original-audit.json')
        for row in rows:
            for key in ['image', 'label']:
                dest = target / row[key]
                dest.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source / row[key], dest)
        for relative_list, entries in split_lists.items():
            dest = target / relative_list
            dest.parent.mkdir(parents=True, exist_ok=True)
            # Ultralytics interprets ./ relative to the list's directory.
            prefix = Path(relative_list).parent
            if prefix != Path('.'):
                raise ValueError('Split lists must be in the dataset root')
            dest.write_text('\n'.join('./' + entry for entry in entries) + '\n', encoding='utf-8')
        portable = {**audit, 'samples': rows, 'original_audit_sha256': sha(source / 'audit.json'),
                    'portability_revision': 'Only executable image/label path separators normalized; source bytes/folds/classes preserved'}
        (target / 'audit.json').write_text(json.dumps(portable, indent=2), encoding='utf-8')
        schema['path'] = '.'
        for key in ['train', 'val', 'test']:
            if schema.get(key):
                if not isinstance(schema[key], str):
                    raise ValueError('Only directory-based split schemas supported')
                schema[key] = relative(schema[key])
        (target / 'data.yaml').write_text(json.dumps(schema, indent=2), encoding='utf-8')
        config['datasets'][name] = dict(directory=f'Datasets/{name}', samples=len(rows),
                                        original_audit_sha256=sha(source / 'audit.json'))
    for name, source in weights.items():
        path = out / 'Weights' / (name + '.pt')
        path.parent.mkdir(exist_ok=True)
        shutil.copy2(source, path)
        config['weights'][name] = dict(path=f'Weights/{name}.pt', sha256=sha(path),
                                      original_source=str(source), deployment_accepted=False)
        companion = source.with_name('model.json')
        if companion.exists():
            shutil.copy2(companion, path.with_suffix('.model.json'))
        if sha(source) != sha(path):
            raise ValueError('Weight changed during export')
    if clip:
        target = out / 'Cache/clip/ViT-B-32.pt'
        target.parent.mkdir(parents=True)
        shutil.copy2(clip, target)
        config['clip_directory'] = 'Cache'
    shutil.copy2(Path(__file__).with_name('portable_training_runner.py'), out / 'train.py')
    version = importlib.metadata.version('ultralytics')
    (out / 'requirements.txt').write_text(f'ultralytics=={version}\nPyYAML>=6.0\nPillow>=10.0\n', encoding='utf-8')
    first_dataset, first_weight = next(iter(datasets)), next(iter(weights))
    guide = f'''Ubuntu training workspace

Copy this entire directory to any Ubuntu path, for example ~/magic-training.
Do not copy a Windows .venv. Create a new Linux environment:

cd ~/magic-training
python3 -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
python train.py --dataset {first_dataset} --weight {first_weight} --check
python train.py --dataset {first_dataset} --weight {first_weight} --epochs 20 --device auto --name ubuntu-first

CUDA: install the PyTorch build matching your GPU/driver using https://pytorch.org/get-started/locally/
Verify with: python -c "import torch; print(torch.cuda.is_available())"
CPU: use --device cpu. Training results go to Runs/<name>/train/weights/best.pt.
Never overwrite old runs. --check validates all exported source bytes without torch.
This starts a NEW training run, not an exact Windows optimizer-state resume.
Native baseline validation runs only when initializer and dataset class names/IDs match.
Otherwise baseline-skipped.json records why mapped evaluation is required.
Final service/independent TEST/PICO acceptance is separate.
World training uses bundled CLIP cache if exported. All data licenses/attribution remain in audit.json.
Historical specialized experiments may use different samplers or hyperparameters. This standalone
runner uses the standard loader and must not be described as an exact reproduction of those runs.
Only training assets are exported; Unity Library/SDK/Windows virtual environments are unnecessary.
'''
    (out / 'README.txt').write_text(guide, encoding='utf-8')
    ubuntu_templates = Path(__file__).with_name('portable-ubuntu')
    for name in ['setup-ubuntu.sh', 'train-ubuntu.sh']:
        # Shell scripts must remain executable by bash after Windows checkouts.
        script = (ubuntu_templates / name).read_bytes().replace(b'\r\n', b'\n')
        (out / name).write_bytes(script)
    dual_boot = (ubuntu_templates / 'DUAL_BOOT.txt').read_text(encoding='utf-8')
    dual_boot = dual_boot.replace('C:\\magic-training', str(out))
    dual_boot = dual_boot.replace('C:\\Users\\26417\\Documents\\ChatGPT\\magic\\project', str(ROOT))
    dual_boot = dual_boot.replace('@FOLDER@', out.name).replace('@DATASET@', first_dataset).replace('@WEIGHT@', first_weight)
    (out / 'DUAL_BOOT.txt').write_text(dual_boot, encoding='utf-8')
    for path in sorted(out.rglob('*')):
        if path.is_file():
            config['payload_sha256'][path.relative_to(out).as_posix()] = sha(path)
    (out / 'workspace.json').write_text(json.dumps(config, indent=2), encoding='utf-8')
    print(json.dumps(dict(root=str(out), datasets=list(datasets), weights=list(weights),
                          files=len(config['payload_sha256'])), ensure_ascii=False))


if __name__ == '__main__':
    main()
