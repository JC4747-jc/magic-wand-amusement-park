"""Bounded official validation metadata index; no image downloads or training acceptance."""
import csv
from collections import Counter,defaultdict
import hashlib
import json
from pathlib import Path
import random
import urllib.request

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'.codex-tmp/yolo-training/source-metadata'
SOURCES={
    'openimages-validation-boxes.csv':'https://storage.googleapis.com/openimages/v5/validation-annotations-bbox.csv',
    'openimages-validation-images.csv':'https://storage.googleapis.com/openimages/2018_04/validation/validation-images-with-rotation.csv'}
CAP=32*1024**2

def sha(path):
    value=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024**2),b''):value.update(block)
    return value.hexdigest()

def fetch(name,url):
    path=OUT/name;info=OUT/(name+'.source.json')
    if path.exists() and info.exists():
        metadata=json.loads(info.read_text());assert metadata['url']==url and metadata['sha256']==sha(path) and path.stat().st_size<=CAP;return path
    request=urllib.request.Request(url,headers={'User-Agent':'MagicMR-training-metadata-audit/1.0'})
    with urllib.request.urlopen(request,timeout=25) as response:
        if int(response.headers.get('Content-Length','0'))>CAP:raise RuntimeError('Metadata file exceeds 32 MiB cap: '+name)
        size=0
        with (OUT/(name+'.part')).open('wb') as stream:
            while True:
                block=response.read(1024**2)
                if not block:break
                size+=len(block)
                if size>CAP:raise RuntimeError('Metadata stream exceeds cap')
                stream.write(block)
    (OUT/(name+'.part')).replace(path)
    info.write_text(json.dumps({'url':url,'bytes':size,'sha256':sha(path)},indent=2))
    return path

def rows(path):
    with path.open(encoding='utf-8-sig',newline='') as stream:yield from csv.DictReader(stream)

def main():
    OUT.mkdir(parents=True,exist_ok=True);sources={name:fetch(name,url) for name,url in SOURCES.items()}
    table=json.loads((OUT/'openimages-boxable-source.json').read_text())
    ids={entry['class_id']:canonical for canonical,entries in table['candidates'].items() for entry in entries}
    boxes=sources['openimages-validation-boxes.csv'];excluded=set();counts=Counter()
    for row in rows(boxes):
        if row['LabelName'] in ids:
            counts[ids[row['LabelName']]]+=1
            if row['IsGroupOf']=='1' or row['IsDepiction']=='1':excluded.add(row['ImageID'])
    selected=defaultdict(list);seen=defaultdict(set);rng=random.Random(42)
    for row in rows(boxes):
        if row['LabelName'] not in ids or row['ImageID'] in excluded:continue
        name=ids[row['LabelName']];image=row['ImageID'];x0,x1,y0,y1=[float(row[k]) for k in ('XMin','XMax','YMin','YMax')]
        if image in seen[name] or x1-x0<.02 or y1-y0<.02 or (x1-x0)*(y1-y0)<.005:continue
        seen[name].add(image);count=len(seen[name])
        if len(selected[name])<40:selected[name].append(image)
        else:
            index=rng.randrange(count)
            if index<40:selected[name][index]=image
    wanted={image for images in selected.values() for image in images};annotations=defaultdict(list)
    for row in rows(boxes):
        if row['ImageID'] in wanted and row['LabelName'] in ids:
            annotations[row['ImageID']].append({'canonical_candidate':ids[row['LabelName']],'source_class_id':row['LabelName'],
                'xyxy':[float(row[k]) for k in ('XMin','YMin','XMax','YMax')],'occluded':row['IsOccluded']=='1','truncated':row['IsTruncated']=='1'})
    records=[]
    for row in rows(sources['openimages-validation-images.csv']):
        if row['ImageID'] in wanted:records.append({'image_id':row['ImageID'],'official_metadata':row,'annotations':annotations[row['ImageID']]})
    assert len({r['image_id'] for r in records})==len(wanted),'Selected image provenance is missing'
    report={'purpose':'DATA_SOURCE_CANDIDATES_ONLY_NOT_TRAINING_ACCEPTED','seed':42,'maximum_per_class':40,
            'source_split':'official validation; no train/val/test assignment has been made for this project',
            'raw_target_box_counts':dict(counts),'excluded_image_ids_with_target_group_or_depiction':len(excluded),
            'eligible_unique_images_per_class':{k:len(v) for k,v in seen.items()},'selected_per_class':{k:len(v) for k,v in selected.items()},
            'unique_selected_images':len(records),'image_downloaded':False,'semantic_review_required':['Statue from Sculpture','PottedPlant from Houseplant'],
            'provenance_review':'OriginalURL/OriginalLandingURL/License/Author metadata retained for per-image review and attribution',
            'sources':{name:json.loads((OUT/(name+'.source.json')).read_text()) for name in SOURCES},'records':records}
    (OUT/'openimages-candidates.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='records'},ensure_ascii=False))

if __name__=='__main__':main()
