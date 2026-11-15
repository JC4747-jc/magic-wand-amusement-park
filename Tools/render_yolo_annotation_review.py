"""Render bounded annotation QA plots without modifying any source photograph."""
import hashlib
import json
import os
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
os.environ.setdefault('OMP_NUM_THREADS', '1')
os.environ.setdefault('OPENBLAS_NUM_THREADS', '1')
os.environ.setdefault('MPLCONFIGDIR', str(ROOT / '.codex-tmp/yolo-training/mpl-review'))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle
from PIL import Image

OUT = ROOT / '.codex-tmp/yolo-training/annotation-plots'
CAP = 32 * 1024**2


def pages(records, prefix):
    outputs = []
    assert len(records) <= 200
    for start in range(0, len(records), 8):
        fig, axes = plt.subplots(2, 4, figsize=(16, 10), dpi=100, layout='constrained')
        for ax, record in zip(axes.flat, records[start:start+8]):
            path = ROOT / record['local_path']
            if record.get('sha256'):
                assert hashlib.sha256(path.read_bytes()).hexdigest() == record['sha256']
            with Image.open(path) as image:
                image.load()
                w, h = image.size
                # PIL grayscale JPEGs otherwise receive matplotlib's false-color palette.
                ax.imshow(image.convert('RGB'))
                for index, box in enumerate(record.get('boxes', [])):
                    if 'xyxy_pixels' in box:
                        x0, y0, x1, y1 = box['xyxy_pixels']
                        name = box['class']
                    else:
                        x0, y0, x1, y1 = [v * scale for v, scale in zip(box['xyxy'], (w, h, w, h))]
                        name = box['canonical_candidate']
                    ax.add_patch(Rectangle((x0, y0), x1-x0, y1-y0, fill=False, edgecolor='#00ff7f', linewidth=1))
                    ax.text(x0, y0, f'{index}:{name}', fontsize=6, color='black', bbox={'facecolor': '#00ff7f', 'alpha': .8, 'pad': 1})
            ax.set_title(record['key'], fontsize=9)
            ax.set_axis_off()
        for ax in list(axes.flat)[len(records[start:start+8]):]:
            ax.set_axis_off()
        target = OUT / f'{prefix}-{start//8+1:02}.jpg'
        assert sum(p.stat().st_size for p in OUT.glob('*.jpg')) < CAP
        fig.savefig(target, format='jpg', pil_kwargs={'quality': 88})
        plt.close(fig)
        outputs.append(target.relative_to(ROOT).as_posix())
    assert sum(p.stat().st_size for p in OUT.glob('*.jpg')) <= CAP
    return outputs


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    gap = json.loads((ROOT / 'TrainingData/public-gap-annotations-v1.json').read_text(encoding='utf8'))
    gap_records = [dict(r, key=r['source_key']) for r in gap['records']]
    oi = json.loads((ROOT / '.codex-tmp/yolo-training/openimages-review/candidate-p20.json').read_text(encoding='utf8'))
    ambiguous = [dict(r, key=r['image_id'], boxes=r['annotations']) for r in oi['records']
                 if any(a['canonical_candidate'] in ('Statue', 'PottedPlant') for a in r['annotations'])]
    result = {'purpose': 'ANNOTATION_QA_PLOTS_NOT_EDITED_SOURCE_IMAGES', 'gap_photos': len(gap_records),
              'ambiguous_openimages_photos': len(ambiguous), 'gap_pages': pages(gap_records, 'gap'),
              'ambiguous_pages': pages(ambiguous, 'oi-ambiguous')}
    expanded = json.loads((ROOT / '.codex-tmp/yolo-training/openimages-review/potted-p40.json').read_text(encoding='utf8'))
    old_ids = {r['image_id'] for r in oi['records']}
    extra = [dict(r, key=r['image_id'], boxes=r['annotations']) for r in expanded['records'] if r['image_id'] not in old_ids]
    result['additional_potted_photos'] = len(extra)
    result['additional_potted_pages'] = pages(extra, 'oi-potted-extra')
    exact = ('Tree', 'Spider', 'chair', 'couch', 'bed', 'dining table', 'Cat', 'Dog')
    sample = {}
    for category in exact:
        eligible = sorted((r for r in oi['records'] if any(a['canonical_candidate'] == category for a in r['annotations'])), key=lambda r:r['image_id'])
        for r in eligible[:2]:
            sample[r['image_id']] = dict(r, key=r['image_id'], boxes=r['annotations'])
    result['exact_class_sample_photos'] = len(sample)
    result['exact_class_sample_pages'] = pages(list(sample.values()), 'oi-exact-sample')
    (OUT / 'manifest.json').write_text(json.dumps(result, indent=2), encoding='utf8')
    print(json.dumps(result))


def remaining_exact():
    OUT.mkdir(parents=True, exist_ok=True)
    oi = json.loads((ROOT / '.codex-tmp/yolo-training/openimages-review/candidate-p20.json').read_text(encoding='utf8'))
    sample = set()
    for category in ('Tree','Spider','chair','couch','bed','dining table','Cat','Dog'):
        eligible = sorted((r for r in oi['records'] if any(a['canonical_candidate']==category for a in r['annotations'])), key=lambda r:r['image_id'])
        sample.update(r['image_id'] for r in eligible[:2])
    records = [dict(r, key=r['image_id'], boxes=r['annotations']) for r in sorted(oi['records'],key=lambda r:r['image_id'])
               if r['image_id'] not in sample and not any(a['canonical_candidate'] in ('Statue','PottedPlant') for a in r['annotations'])]
    report = {'purpose':'REMAINING_EXACT_CATEGORY_ANNOTATION_QA_NOT_TRAINING_ACCEPTANCE',
              'previous_sample_ids':sorted(sample), 'image_ids':[r['image_id'] for r in records], 'pages':pages(records,'oi-exact-rest')}
    (OUT/'exact-rest-manifest.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print(json.dumps({'photos':len(records),'pages':report['pages']}))


if __name__ == '__main__':
    remaining_exact() if sys.argv[1:]==['--remaining-exact'] else main()
