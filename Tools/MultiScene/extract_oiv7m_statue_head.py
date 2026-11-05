"""Extract a reviewed native OIV7 class logit without rebuilding or randomizing the detection head."""
from copy import deepcopy
import argparse
import hashlib
import json
from pathlib import Path

import cv2
import torch
from ultralytics import YOLO

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--target', choices=['statue', 'tree'], default='statue')
    args = parser.parse_args()
    native_id, native_name, output_name, dataset_name = {
        'statue':(442,'Sculpture','Statue','statue-rear-context-v17'),
        'tree':(553,'Tree','Tree','tree-windswept-positives-v11')}[args.target]
    source = ROOT / 'yolov8m-oiv7.pt'
    out = ROOT / f'Reports/{args.target}-oiv7m-single-class-extraction'
    out.mkdir(exist_ok=False)
    digest = sha(source)
    if digest != '21ffa3718c577ac23e708e4c0544c49a20682efa03914d5f816166b54e8fd3fe':
        raise ValueError('Official source SHA differs from verified baseline')
    wrapper = YOLO(source)
    original = wrapper.model.float().eval()
    if original.names[native_id] != native_name or type(original.model[-1]).__name__ != 'Detect':
        raise ValueError('Unexpected source architecture or native class')
    extracted = deepcopy(original)
    head = extracted.model[-1]
    if head.end2end or len(head.cv3) != 3:
        raise ValueError('Only reviewed legacy three-scale Detect supported')
    for branch in head.cv3:
        old = branch[-1]
        if not isinstance(old, torch.nn.Conv2d) or old.out_channels != 601:
            raise ValueError('Unexpected classifier')
        replacement = torch.nn.Conv2d(old.in_channels, 1, old.kernel_size,
                                     old.stride, old.padding, bias=True)
        with torch.no_grad():
            replacement.weight.copy_(old.weight[native_id:native_id+1])
            replacement.bias.copy_(old.bias[native_id:native_id+1])
        branch[-1] = replacement
    head.nc = 1
    head.no = head.reg_max * 4 + 1
    extracted.names = {0: output_name}
    extracted.yaml = deepcopy(extracted.yaml)
    extracted.yaml['nc'] = 1
    extracted.yaml['preserved_oiv7_classification_width'] = 192
    # The serialized model is authoritative: generic YAML rebuilding would
    # reduce cv3 width from 192 to 64 and discard source classification tensors.
    prefix = f'model.{len(extracted.model)-1}.cv3.'
    assert all(branch[-1].in_channels == 192 for branch in head.cv3)
    changes = []
    for name, tensor in extracted.state_dict().items():
        reference = original.state_dict()[name]
        if name.startswith(prefix) and any(name.endswith(f'.{k}.2.{suffix}')
                for k in range(3) for suffix in ('weight', 'bias')):
            reference = reference[native_id:native_id+1]
            changes.append(name)
        if not torch.equal(tensor, reference):
            raise AssertionError(f'Unexpected tensor change: {name}')
    if len(changes) != 6:
        raise AssertionError(changes)
    wrapper.model = extracted
    checkpoint = out / 'initializer.pt'
    wrapper.save(checkpoint)
    reloaded = YOLO(checkpoint).model.float().eval()
    probes = []
    audit = json.loads((ROOT / 'Datasets' / dataset_name / 'audit.json').read_text(encoding='utf-8'))
    samples = [r for r in audit['samples'] if r['split'] == 'train'][:2]
    with torch.inference_mode():
        for row, shape in zip(samples, [(320, 320), (384, 640)]):
            image = cv2.imread(str(ROOT / 'Datasets' / dataset_name / row['image']))
            if image is None:
                raise ValueError(row['image'])
            image = cv2.resize(image, shape[::-1])[:, :, ::-1].copy()
            tensor = torch.from_numpy(image).permute(2, 0, 1).float().unsqueeze(0) / 255
            a = original(tensor)[0][:, [0, 1, 2, 3, 4+native_id], :]
            b = reloaded(tensor)[0]
            if a.shape != b.shape:
                raise AssertionError((a.shape, b.shape))
            torch.testing.assert_close(a, b, rtol=1e-5, atol=1e-5)
            probes.append(dict(train_image=row['image'], shape=list(tensor.shape),
                               image_sha256=sha(ROOT / 'Datasets' / dataset_name / row['image']),
                               box_max_abs=float((a[:, :4]-b[:, :4]).abs().max()),
                               score_max_abs=float((a[:, 4]-b[:, 4]).abs().max())))
    if sha(source) != digest:
        raise ValueError('Source changed')
    report = dict(status='PASS', source_sha256=digest, initializer_sha256=sha(checkpoint),
                  source_class=native_id, native_name=native_name, output_class=0, output_name=output_name,
                  classifier_hidden_width=192, sliced_state_tensors=changes, probes=probes,
                  script_sha256=sha(Path(__file__)),
                  scope='All state tensors verified; two real TRAIN-image pre-NMS forward probes after save/reload',
                  limitations=['Extraction does not improve recognition',
                               'Training must preserve serialized head topology; generic YAML rebuild is incompatible',
                               'No TEST, PICO or deployment validation'], deployed=False)
    (out / 'report.json').write_text(json.dumps(report, indent=2))
    print(json.dumps(report))


if __name__ == '__main__':
    main()
