"""Preserve the serialized OIV7 medium head and audited complete TRAIN sampling."""
from copy import deepcopy
import json
from pathlib import Path
import torch
from ultralytics.models.yolo.detect import DetectionTrainer
from hard_positive_world_trainer import planned_loader
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'Reports/statue-oiv7m-figure-labels-v24-training'
class PreservedMediumTrainer(DetectionTrainer):
    target_name = 'Statue'
    def get_model(self, cfg=None, weights=None, verbose=True):
        if weights is None or type(weights).__name__ != 'DetectionModel':
            raise ValueError('Requires a serialized DetectionModel initializer')
        if self.data['nc'] != 1 or weights.names != {0: self.target_name} or self.data['names'] != {0: self.target_name}:
            raise ValueError('Requires matching reviewed single-class dataset and initializer')
        model = deepcopy(weights).float()
        head = model.model[-1]
        if type(head).__name__ != 'Detect' or head.nc != 1 or head.end2end:
            raise ValueError('Unexpected detection head')
        if any(b[-1].in_channels != 192 or b[-1].out_channels != 1 for b in head.cv3):
            raise ValueError('Classification topology was not preserved')
        for key, value in model.state_dict().items():
            if not torch.equal(value, weights.state_dict()[key].float()):
                raise ValueError(f'Initialization mismatch: {key}')
        report = dict(status='PASS', tensors=len(model.state_dict()),
                      class_hidden_width=192, scope='Actual trainer model construction before optimizer setup')
        (OUT / 'trainer-initializer-verification.json').write_text(json.dumps(report, indent=2))
        return model

    def get_dataloader(self, dataset_path, batch_size=16, rank=0, mode='train'):
        if mode != 'train':
            return super().get_dataloader(dataset_path, batch_size, rank, mode)
        assert rank in (-1, 0) and self.args.workers == 0 and self.world_size <= 1
        dataset = self.build_dataset(dataset_path, mode, batch_size)
        loader = planned_loader(dataset, ROOT/'Reports/statue-figure-labels-v24-sampling-plan.json', Path(self.args.data).with_name('audit.json'), self.args.seed, batch_size)
        self.add_callback('on_train_epoch_start', lambda trainer: trainer.train_loader.sampler.set_epoch(trainer.epoch))
        return loader
