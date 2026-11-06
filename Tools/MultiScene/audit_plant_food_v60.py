"""Read back all payloads and the real installed training loader independently."""
import hashlib
import json
from pathlib import Path
from dataset_paths import manifest_relative_path as rel

ROOT = Path(__file__).resolve().parents[2]
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    from ultralytics.data.dataset import YOLODataset
    import ultralytics
    data = ROOT/'Datasets/plant-food-v60'
    parent = ROOT/'Datasets/plant-label-paths-v52'
    audit = json.loads((data/'audit.json').read_text())
    old = json.loads((parent/'audit.json').read_text())
    assert audit['samples'][:161] == old['samples']
    counts = {k: 0 for k in ('train', 'val', 'test')}
    instances = {0: 0, 1: 0}
    for row in audit['samples']:
        counts[row['split']] += 1
        assert sha(data/rel(row['image'])) == row['sha256']
        label = data/rel(row['label'])
        assert sha(label) == row['label_sha256']
        if row['split'] == 'train':
            for line in label.read_text().splitlines(): instances[int(line.split()[0])] += 1
    assert counts == {'train': 136, 'val': 30, 'test': 4}
    assert instances == {0: 133, 1: 32}
    for row in audit['samples'][161:]:
        assert row['split'] == 'train' and row['negative']
        assert (data/rel(row['label'])).read_bytes() == b''
    loader = YOLODataset(img_path=str(data/'images/train'), data={'names': {0:'PottedPlant',1:'CutArrangement'},'nc':2},
                         imgsz=640, augment=False, batch_size=4)
    actual = {0: 0, 1: 0}
    backgrounds = 0
    for row in loader.labels:
        backgrounds += int(len(row['cls']) == 0)
        for cls in row['cls'].reshape(-1): actual[int(cls)] += 1
    assert len(loader) == 136 and actual == instances and backgrounds == 23
    result = dict(status='ACTUAL_LOADER_AND_ALL340_PAYLOAD_SHA_VERIFIED', images=170, counts=counts,
        inherited_rows_exact=161, actual_training_images=len(loader), actual_class_instances=actual,
        actual_background_images=backgrounds, ultralytics=ultralytics.__version__,
        dataset_sha256=sha(data/'audit.json'), training_approved=False, deployment_accepted=False,
        limitation='Data integrity check only; OPEN annotation hold and independent TEST/PICO requirements remain.')
    out = ROOT/'Reports/plant-food-v60-loader-check.json'
    assert not out.exists()
    out.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__': main()
