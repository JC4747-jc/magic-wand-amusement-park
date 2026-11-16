"""Bounded CUDA fine-tune of the frozen Cockroach/Spider bootstrap; never deployment acceptance."""
import argparse
import ctypes
import gc
import json
import math
import os
from pathlib import Path
import re
import shutil
import signal
import sys
import time

from smoke_yolo_training import Guard, OWNED, digest, directory_bytes, host_memory
from setup_yolo_training_env import fingerprint

ROOT = Path(__file__).resolve().parents[1]

class TrainingGuard(Guard):
    def check(self,*args,force=False):
        # Return unused native/Python allocations before sampling; never lower the safety floor.
        if force or time.monotonic()-self.last>=5:
            gc.collect();ctypes.CDLL(None).malloc_trim(0)
        return super().check(*args,force=force)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run-id', required=True)
    parser.add_argument('--epochs', type=int, default=20)
    args = parser.parse_args()
    assert re.fullmatch(r'[a-z0-9][a-z0-9-]{3,60}', args.run_id) and 2 <= args.epochs <= 40
    assert sys.platform == 'linux' and sys.prefix == str(OWNED/'venv')
    run = OWNED/'runs'/args.run_id
    workspace = ROOT/'.codex-tmp/yolo-training/artifacts'/args.run_id
    assert not run.exists() and not workspace.exists(), 'Refusing to overwrite an existing candidate run'
    run.mkdir(parents=True); workspace.mkdir(parents=True); os.chdir(run)
    report = {'purpose': 'BUGS_BOOTSTRAP_NOT_FULL_PROJECT_OR_DEPLOYMENT_ACCEPTANCE', 'status': 'RUNNING',
              'accepted_for_deployment': False, 'run': str(run), 'runner_sha256': digest(Path(__file__).resolve()),
              'limits': {'run_bytes': 128*1024**2, 'seconds': 900, 'allocator_fraction': .55, 'workers': 0, 'batch': 1}}
    shutil.copy2(Path(__file__).resolve(), workspace/'runner.py')
    helper = Path(__file__).with_name('yolo_training_logs.py')
    shutil.copy2(helper, workspace/helper.name)
    report['log_helper_sha256'] = digest(helper)
    report['log_receipt'] = str(ROOT/'.codex-tmp/yolo-training/run-logs'/(args.run_id+'.run.json'))
    guard = None; torch = None; before = fingerprint()
    try:
        assert host_memory() >= 6*1024**3, 'Need 6 GiB host RAM before CUDA imports/warmup'
        cgroup = Path('/sys/fs/cgroup')/Path('/proc/self/cgroup').read_text().strip().split('::',1)[1].lstrip('/')
        cgroup_limits = {k:(cgroup/k).read_text().strip() for k in ('memory.max','memory.high','memory.swap.max')}
        assert cgroup_limits['memory.max']=='3221225472' and cgroup_limits['memory.swap.max']=='0', 'Use bounded systemd scope, 3 GiB with no swap'
        report['cgroup_limits'] = cgroup_limits
        import psutil
        import numpy as np
        import torch
        import ultralytics
        from ultralytics import YOLO, settings
        from ultralytics.models.yolo.detect import DetectionTrainer, DetectionValidator
        from ultralytics.utils import YAML, events
        assert torch.__version__ == '2.4.0+cu124' and ultralytics.__version__ == '8.4.212'
        settings.update({k: False for k in ('sync','clearml','comet','dvc','mlflow','raytune','tensorboard','wandb') if k in settings})
        events.events.enabled = False
        class LocalCallbacks:
            def run_callbacks(self, event):
                for callback in self.callbacks.get(event, []):
                    module = callback.__module__
                    if module.startswith('ultralytics.utils.events') or module.startswith('ultralytics.utils.callbacks.') and module != 'ultralytics.utils.callbacks.base':
                        continue
                    callback(self)
        class LocalTrainer(LocalCallbacks, DetectionTrainer):
            def get_dataloader(self,*args,**kwargs):
                loader=super().get_dataloader(*args,**kwargs);loader.pin_memory=False;return loader
        class LocalValidator(LocalCallbacks, DetectionValidator): pass
        torch.set_num_threads(1); torch.set_num_interop_threads(1)
        assert torch.cuda.is_available()
        torch.cuda.set_per_process_memory_fraction(.55, 0); torch.cuda.reset_peak_memory_stats()
        guard = TrainingGuard(run, torch, psutil); guard.check(force=True)
        assert torch.cuda.mem_get_info()[0] >= 4*1024**3
        signal.signal(signal.SIGALRM, lambda *_: (_ for _ in ()).throw(TimeoutError('900-second candidate experiment budget')))
        signal.alarm(900)
        frozen_path = ROOT/'TrainingData/bugs-bootstrap-split-v1.json'
        frozen_hash = digest(frozen_path); frozen = json.loads(frozen_path.read_text())
        assert frozen['test_frozen_before_training'] and not frozen['accepted_for_deployment']
        assert digest(ROOT/'TrainingData/bugs-bootstrap-review-v1.json') == frozen['review_sha256']
        overlay_path = ROOT/'TrainingData/bugs-bootstrap-overlay-review-v1.json'
        overlay = json.loads(overlay_path.read_text())
        assert overlay['status'] == 'ORIGINALS_AND_THREE_QA_SHEETS_VISUALLY_REVIEWED'
        assert overlay['reviewSHA256'] == frozen['review_sha256'] and overlay['splitSHA256'] == frozen_hash
        for sheet in overlay['sheets']: assert digest(ROOT/sheet['path']) == sheet['sha256']
        assert len(frozen['records']) == 38 and frozen['names'] == ['Cockroach','Spider']
        assert args.epochs == frozen['preset_experiment']['epochs'] == 20, 'Do not tune frozen experiment on its test results'
        groups = {}
        for row in frozen['records']:
            groups.setdefault(row['component_id'], set()).add(row['split'])
        assert all(len(splits) == 1 for splits in groups.values())
        report['overlay_review_sha256'] = digest(overlay_path)
        report.update(dataset_sha256=frozen_hash, split_counts=frozen['split_counts'], versions={
            'torch': torch.__version__, 'ultralytics': ultralytics.__version__, 'python': sys.version.split()[0]})
        shutil.copy2(frozen_path, workspace/'frozen-dataset.json')
        for row in frozen['records']:
            source = (ROOT/row['source_path']).resolve()
            assert source.is_relative_to(ROOT.resolve()) and digest(source) == row['sha256']
            folder = run/'data'/row['split']
            (folder/'images').mkdir(parents=True, exist_ok=True); (folder/'labels').mkdir(exist_ok=True)
            shutil.copy2(source, folder/'images'/(row['key']+'.jpg'))
            labels = []
            for box in row['boxes']:
                x0,y0,x1,y1 = box['xyxy_normalized']
                assert all(math.isfinite(v) for v in (x0,y0,x1,y1)) and 0 <= x0 < x1 <= 1 and 0 <= y0 < y1 <= 1
                class_id = frozen['names'].index(box['class'])
                labels.append(f"{class_id} {(x0+x1)/2:.9f} {(y0+y1)/2:.9f} {x1-x0:.9f} {y1-y0:.9f}\n")
            (folder/'labels'/(row['key']+'.txt')).write_text(''.join(labels))
        data = run/'data/data.yaml'
        YAML.save(data, {'path': str(run/'data'), 'train': 'train/images', 'val': 'valid/images', 'test': 'test/images', 'names': frozen['names']})
        source_weight = ROOT/'yolo26n.pt'; service = ROOT/'object-detection-demo/scene_profiles.json'
        source_hash = digest(source_weight); service_hash = digest(service)
        model = YOLO(str(source_weight)); stats = {'batches': 0, 'optimizer_steps': 0, 'parameter_changed': False, 'epochs': []}; initial = {}
        report['stats'] = stats
        def started(trainer):
            name, parameter = next((n,p) for n,p in trainer.model.named_parameters() if p.requires_grad and p.ndim == 4)
            initial.update(name=name, parameter=parameter, before=parameter.detach().cpu().clone()); guard.check(force=True)
        def batch(trainer):
            stats['batches'] += 1
            assert trainer.loss is None or bool(torch.isfinite(trainer.loss).all()), 'Nonfinite training loss'
            parameter = initial['parameter']
            stats['parameter_changed'] |= bool((parameter.detach().cpu()-initial['before']).abs().max() > 0)
            stats['optimizer_steps'] = max(stats['optimizer_steps'], int(trainer.optimizer.state.get(parameter, {}).get('step', 0)))
            guard.check()
        def epoch(trainer):
            values = trainer.tloss.items() if isinstance(trainer.tloss, dict) else enumerate(trainer.tloss)
            losses = {str(k): float(v.detach().cpu() if torch.is_tensor(v) else v) for k,v in values}
            assert all(math.isfinite(v) for v in losses.values())
            stats['epochs'].append({'epoch': trainer.epoch+1, 'losses': losses}); guard.check(force=True)
        model.add_callback('on_train_start', started); model.add_callback('on_train_batch_end', batch)
        model.add_callback('on_train_epoch_end', epoch); model.add_callback('on_model_save', lambda *_: guard.check(force=True))
        model.add_callback('on_val_batch_end', guard.check)
        trained = model.train(trainer=LocalTrainer, data=str(data), epochs=args.epochs, patience=args.epochs,
            imgsz=416, batch=1, workers=0, device=0, cache=False, project=str(run), name='train', exist_ok=False,
            save=True, save_period=-1, plots=False, amp=True, optimizer='AdamW', nbs=1, lr0=.001,
            seed=42, deterministic=True, mosaic=0, mixup=0, copy_paste=0, close_mosaic=0, compile=False, verbose=False)
        train_count = sum(r['split'] == 'train' for r in frozen['records'])
        assert len(stats['epochs']) == args.epochs and stats['batches'] == train_count*args.epochs
        assert stats['optimizer_steps'] > 0 and stats['parameter_changed']
        artifacts = {}
        for name in ('best.pt','last.pt'):
            path = run/'train/weights'/name
            shutil.copy2(path, workspace/name); artifacts[name] = {'bytes': path.stat().st_size, 'sha256': digest(path)}
        reloaded = YOLO(str(workspace/'best.pt'))
        assert list(reloaded.names.values()) == frozen['names']
        # Only now read the frozen test split through the native validator, once; never tune on this result.
        test = reloaded.val(validator=LocalValidator, data=str(data), split='test', imgsz=416, batch=1, workers=0,
            device=0, cache=False, plots=False, save_json=False, save_txt=False, conf=.001, iou=.7, max_det=20,
            project=str(run), name='test', exist_ok=False, verbose=False)
        matrix = np.zeros((3,3), dtype=int); error_rows = []
        for row in (r for r in frozen['records'] if r['split'] == 'test'):
            image = run/'data/test/images'/(row['key']+'.jpg')
            torch.cuda.synchronize(); began = time.perf_counter()
            prediction = reloaded.predict(str(image), device=0, imgsz=416, conf=.25, iou=.7, max_det=20, verbose=False, save=False)[0]
            torch.cuda.synchronize(); elapsed = (time.perf_counter()-began)*1000
            boxes = prediction.boxes.xyxyn.cpu().numpy(); classes = prediction.boxes.cls.cpu().numpy().astype(int)
            scores = prediction.boxes.conf.cpu().numpy()
            truth = [np.asarray(box['xyxy_normalized']) for box in row['boxes']]
            truth_classes = [frozen['names'].index(box['class']) for box in row['boxes']]
            pairs = []
            for pi,box in enumerate(boxes):
                for ti,target in enumerate(truth):
                    intersection = np.maximum(0,np.minimum(box[2:],target[2:])-np.maximum(box[:2],target[:2])).prod()
                    area = (box[2:]-box[:2]).prod()+(target[2:]-target[:2]).prod()-intersection
                    iou = float(intersection/max(float(area),1e-12))
                    if iou >= .5: pairs.append((iou,pi,ti))
            matched_predictions = {};matched_truth = set()
            for iou,pi,ti in sorted(pairs,reverse=True):
                if pi in matched_predictions or ti in matched_truth: continue
                matched_predictions[pi]=(ti,iou);matched_truth.add(ti)
                matrix[classes[pi],truth_classes[ti]] += 1
            for pi,category in enumerate(classes):
                if pi not in matched_predictions: matrix[category,2] += 1
            for ti,category in enumerate(truth_classes):
                if ti not in matched_truth: matrix[2,category] += 1
            error_rows.append({'image':row['key'],'ground_truth':row['boxes'],'background_only':not bool(truth),
                'prediction_count':len(boxes),'detections':[{'class':frozen['names'][int(category)],'confidence':float(score),'xyxy_normalized':box.tolist(),
                    'matched_truth':matched_predictions[pi][0] if pi in matched_predictions else None,
                    'matched_iou':matched_predictions[pi][1] if pi in matched_predictions else None} for pi,(box,category,score) in enumerate(zip(boxes,classes,scores))],
                'unmatched_truth':[ti for ti in range(len(truth)) if ti not in matched_truth], 'single_image_call_ms':elapsed})
            guard.check(force=True)
        assert digest(frozen_path) == frozen_hash and digest(source_weight) == source_hash and digest(service) == service_hash
        assert fingerprint() == before, 'Critical shared F2G dependency files changed'
        for name in ('results.csv','args.yaml'):
            shutil.copy2(run/'train'/name, workspace/name)
        report.update(status='CANDIDATE_TRAINED_AND_TESTED_NOT_DEPLOYMENT_ACCEPTED', artifacts=artifacts,
            initial_weight_sha256=source_hash, service_config_unchanged=True, five_shared_dependency_files_unchanged=True,
            training={'epochs': args.epochs, 'batch': 1, 'workers': 0, 'imgsz': 416, 'image_cache': False, 'amp': bool(model.trainer.amp), 'seed': 42},
            validation_metrics=trained.results_dict if trained else {}, frozen_test_metrics=test.results_dict,
            per_class_test=test.summary(), confusion_matrix={'rows_predicted_columns_actual': matrix.tolist(),
                'names': frozen['names']+['background'], 'confidence': .25, 'iou': .5, 'matching': 'Class-agnostic greedy descending IoU, one-to-one, threshold .5; wrong-class matches are off-diagonal, unmatched predictions/targets are background FP/FN.'},
            test_error_analysis=error_rows, timing_scope='Eight fine-tuning-holdout single-image calls, including two backgrounds; first call may be cold, not headset or steady-state FPS.',
            baseline='Current bugs profile has no accepted two-class custom weight. Existing COCO weights lack both classes; earlier Cockroach/BronzeBeast experiment is not a comparable baseline. No improvement claimed.',
            limitations=frozen['limitations'])
        print('YOLO_BUGS_BOOTSTRAP_TRAIN_TEST_PASS: real optimizer updates, grouped public-photo holdout diagnostic, not deployment acceptance')
    except BaseException as error:
        report.update(status='FAILED', error=type(error).__name__+': '+str(error)); raise
    finally:
        signal.alarm(0)
        if torch is not None and torch.cuda.is_available():
            report.update(torch_peak_allocated_bytes=torch.cuda.max_memory_allocated(), torch_peak_reserved_bytes=torch.cuda.max_memory_reserved())
        if guard is not None:
            report.update(resource_samples=list(guard.samples), run_bytes=directory_bytes(run),
                resource_scope='Callback/5-second samples plus allocator peak, not continuous full-device peak')
        (workspace/'report.json').write_text(json.dumps(report, indent=2, default=float))
        (run/'report.json').write_text(json.dumps(report, indent=2, default=float))


if __name__ == '__main__':
    from yolo_training_logs import run_with_bounded_logs
    run_with_bounded_logs(main)
