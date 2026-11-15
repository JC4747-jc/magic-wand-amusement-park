"""Read-only, bounded audit of the existing YOLO datasets; never starts training."""
import hashlib
import json
import math
import os
from collections import Counter, defaultdict
from pathlib import Path

from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
os.environ.setdefault('OMP_NUM_THREADS','1')
os.environ.setdefault('OPENBLAS_NUM_THREADS','1')
os.environ.setdefault('YOLO_CONFIG_DIR',str(ROOT/'.codex-tmp/yolo-training/settings'))
from ultralytics.utils import YAML
DATA=ROOT/'Find lighter'
MAX_BYTES=2*1024**3
MAX_IMAGES=25000
Image.MAX_IMAGE_PIXELS=16_000_000

def main():
    expected=['Lighter','Tree','Cockroach','Spider','Statue','BronzeBeast','chair','couch','bed','dining table','PottedPlant','Cat','Dog']
    folders=[DATA,DATA/'detect',DATA/'quad_lighter_v1_yolo26',DATA/'detect_merged_v2',DATA/'detect_merged_v3_quad']
    report={'scope':'Existing local data only; label/image structure and exact pixel overlap, not semantic annotation acceptance or training accuracy',
            'limits':{'image_bytes':MAX_BYTES,'images':MAX_IMAGES,'decoded_pixels_per_image':Image.MAX_IMAGE_PIXELS},'datasets':[]}
    total_bytes=total_images=0;available=set()
    for folder in folders:
        config=folder/'data.yaml'
        if not config.is_file():continue
        data=YAML.load(config)
        names=data.get('names',[]);names=list(names.values()) if isinstance(names,dict) else names
        available.update('Lighter' if str(n).casefold()=='lighter' else str(n) for n in names)
        result={'folder':folder.relative_to(ROOT).as_posix(),'names':names,'declared_root':data.get('path'),'declared_splits':{s:data.get(s) for s in ('train','val','test')},
                'license_status':'Not independently verified; consult dataset provenance before acceptance','splits':{},'review_samples':[]}
        hashes=defaultdict(list);groups=defaultdict(set)
        for split in ('train','valid','test'):
            images=folder/split/'images';labels=folder/split/'labels'
            stats=Counter();formats=Counter();classes=Counter()
            if not images.is_dir():result['splits'][split]={'missing_image_directory':True};continue
            for path in sorted(images.iterdir()):
                if not path.is_file() or path.suffix.lower() not in ('.jpg','.jpeg','.png','.bmp','.webp'):continue
                total_images+=1;total_bytes+=path.stat().st_size
                if total_images>MAX_IMAGES or total_bytes>MAX_BYTES:raise RuntimeError('Initial audit exceeded its image/byte cap; no further files decoded')
                stats['images']+=1;stats['image_bytes']+=path.stat().st_size
                label=labels/(path.stem+'.txt')
                if not label.is_file():stats['missing_labels']+=1
                else:
                    if label.stat().st_size>1024**2:raise RuntimeError('Label exceeds 1 MiB cap: '+str(label))
                    rows=label.read_text(encoding='utf-8-sig').splitlines()
                    if not any(row.strip() for row in rows):stats['empty_labels']+=1
                    for row in rows:
                        if not row.strip():continue
                        fields=row.split();formats[str(len(fields))]+=1
                        try:
                            values=[float(v) for v in fields]
                            valid=all(math.isfinite(v) for v in values) and values[0].is_integer() and 0<=values[0]<len(names)
                            valid=valid and len(values)>=5 and all(-1e-5<=v<=1.00001 for v in values[1:])
                            if len(values)==5:valid=valid and values[3]>0 and values[4]>0
                            elif len(values)>=7 and len(values)%2==1:stats['polygon_or_quad_rows_require_explicit_task_conversion']+=1
                            else:valid=False
                            if not valid:raise ValueError('class, format or normalized coordinate')
                            classes[str(int(values[0]))]+=1;stats['annotation_rows']+=1
                        except (ValueError,IndexError):
                            stats['invalid_rows']+=1
                            if len(result['review_samples'])<12:result['review_samples'].append({'file':label.relative_to(folder).as_posix(),'reason':'invalid class/format/normalized coordinates'})
                try:
                    with Image.open(path) as image:
                        image.load()
                        if image.width*image.height>Image.MAX_IMAGE_PIXELS:raise ValueError('decoded pixel cap')
                        rgb=image.convert('RGB');digest=hashlib.sha256(str(rgb.size).encode()+rgb.tobytes()).hexdigest()
                    stats['decoded_images']+=1;hashes[digest].append((split,path.name))
                except (OSError,ValueError,Image.DecompressionBombError) as error:
                    stats['image_decode_failures']+=1
                    if len(result['review_samples'])<12:result['review_samples'].append({'file':path.relative_to(folder).as_posix(),'reason':type(error).__name__})
                groups[path.stem.split('.rf.')[0]].add(split)
            result['splits'][split]={**dict(stats),'label_column_counts':dict(formats),'class_rows':dict(classes)}
        overlaps=[v for v in hashes.values() if len({s for s,_ in v})>1]
        result['exact_pixel_cross_split_groups']=len(overlaps);result['exact_pixel_cross_split_examples']=overlaps[:12]
        result['filename_group_cross_split_candidates']=sum(len(s)>1 for s in groups.values())
        result['group_warning']='Filename grouping is conservative and incomplete; source-capture/near-duplicate review is still required'
        if data.get('path') and str(data['path']).replace('\\','/').rstrip('/')!=folder.as_posix():result['legacy_root_requires_correction']=True
        report['datasets'].append(result)
    report.update(total_images_scanned=total_images,total_image_bytes_scanned=total_bytes,available_declared_classes=sorted(available),missing_declared_classes=[c for c in expected if c not in available],training_started=False)
    output=ROOT/'.codex-tmp/yolo-training';output.mkdir(parents=True,exist_ok=True)
    (output/'data-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='datasets'},ensure_ascii=False))
    for item in report['datasets']:
        print(json.dumps({'dataset':item['folder'],'names':item['names'],'images':{s:v.get('images',0) for s,v in item['splits'].items()},'pixel_cross_split_groups':item['exact_pixel_cross_split_groups'],'label_formats':{s:v.get('label_column_counts',{}) for s,v in item['splits'].items()}},ensure_ascii=False))

if __name__=='__main__':main()
