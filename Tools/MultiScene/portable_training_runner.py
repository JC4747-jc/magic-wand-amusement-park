"""Standalone training entry point copied into an exported training workspace."""
import argparse
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dataset', required=True)
    parser.add_argument('--weight', required=True)
    parser.add_argument('--check', action='store_true', help='Verify all payload bytes without importing training dependencies')
    parser.add_argument('--epochs', type=int, default=20)
    parser.add_argument('--batch', type=int, default=4)
    parser.add_argument('--imgsz', type=int, default=640)
    parser.add_argument('--device', default='auto')
    parser.add_argument('--name', default='candidate')
    parser.add_argument('--lr', type=float, default=.0001)
    parser.add_argument('--freeze', type=int, default=10)
    args = parser.parse_args()
    if args.epochs < 1 or args.batch < 1 or args.imgsz < 32 or args.imgsz % 32:
        parser.error('epochs/batch must be positive, imgsz must be a multiple of32')
    if not 0 < args.lr < 1 or args.freeze < 0:
        parser.error('Invalid learning rate or freeze count')
    if not args.name or Path(args.name).name != args.name or '/' in args.name or '\\' in args.name or ':' in args.name or args.name in ('.', '..'):
        parser.error('name must be one directory name')
    config = json.loads((ROOT / 'workspace.json').read_text(encoding='utf-8'))
    dataset = config['datasets'][args.dataset]
    weight = config['weights'][args.weight]
    # Only exporter-created relative payload paths are executable paths.
    def resolve(relative):
        path = (ROOT / relative).resolve()
        if not path.is_relative_to(ROOT):
            raise ValueError('Payload path escapes workspace')
        return path
    for relative, digest in config['payload_sha256'].items():
        if sha(resolve(relative)) != digest:
            raise ValueError('Payload changed: ' + relative)
    data_root = resolve(dataset['directory'])
    model_path = resolve(weight['path'])
    print(json.dumps(dict(status='PAYLOAD_VERIFIED', root=str(ROOT), dataset=str(data_root),
                          weight=str(model_path), sha256=weight['sha256']), ensure_ascii=False))
    if args.check:
        return
    import torch
    import ultralytics
    from ultralytics import YOLO
    import ultralytics.nn.text_model as text_model
    device = ('0' if torch.cuda.is_available() else 'cpu') if args.device == 'auto' else args.device
    runtime = ROOT / 'Runtime'
    runtime.mkdir(exist_ok=True)
    # Resolve the dataset root on this machine, never reuse an old drive path.
    schema = json.loads((data_root / 'data.yaml').read_text(encoding='utf-8'))
    schema['path'] = data_root.as_posix()
    data_file = runtime / (args.dataset + '.yaml')
    data_file.write_text(json.dumps(schema, indent=2), encoding='utf-8')
    if config.get('clip_directory'):
        text_model.WEIGHTS_DIR = resolve(config['clip_directory'])
    output = ROOT / 'Runs' / args.name
    if output.exists():
        raise FileExistsError('Preserve the existing run; choose another --name')
    output.mkdir(parents=True)
    model = YOLO(model_path)
    if args.freeze > len(model.model.model):
        raise ValueError('freeze exceeds model module count')
    if model.model.yaml.get('preserved_oiv7_classification_width'):
        raise ValueError('This initializer needs its specialized preserved-head trainer; generic rebuilding is unsafe')
    metadata = dict(args=vars(args), python=sys.version, torch=torch.__version__, ultralytics=ultralytics.__version__,
                    device=device, original_weight_sha256=sha(model_path), dataset_audit_sha256=sha(data_root / 'audit.json'),
                    platform=sys.platform, deployment_accepted=False,
                    limitations=['New training on this platform; not exact Windows optimizer/RNG resume',
                                 'Standard complete-coverage loader; no historical custom72-exposure sampler',
                                 'Native baseline metrics are not service acceptance or PICO validation'])
    (output / 'run.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    data_names = schema['names']
    if isinstance(data_names, dict):
        data_names = [data_names[key] for key in sorted(data_names, key=lambda key: int(key))]
    native_names = list(model.names.values())
    if native_names == data_names:
        model.val(data=str(data_file), split='val', device=device, imgsz=args.imgsz,
                  batch=args.batch, workers=0, project=str(output), name='baseline', plots=False)
    else:
        # Native val cannot compare IDs when the initializer's head changes.
        # Use the project's mapped evaluator for a semantic baseline instead.
        (output / 'baseline-skipped.json').write_text(json.dumps(dict(
            reason='Native class IDs/names differ; mapped evaluation required',
            initializer_names=native_names, dataset_names=data_names,
            baseline_performed=False), indent=2), encoding='utf-8')
    # Validation may fuse layers in memory; train from a fresh unfused checkpoint.
    model = YOLO(model_path)
    model.train(data=str(data_file), device=device, epochs=args.epochs, batch=args.batch,
                imgsz=args.imgsz, workers=0, seed=4747, deterministic=True, freeze=args.freeze,
                optimizer='AdamW', lr0=args.lr, lrf=.1, warmup_bias_lr=args.lr,
                mosaic=0., mixup=0., cutmix=0., scale=.1, translate=.05,
                hsv_h=.01, hsv_s=.3, hsv_v=.2, close_mosaic=0,
                project=str(output), name='train', plots=True)


if __name__ == '__main__':
    main()
