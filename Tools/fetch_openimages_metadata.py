"""Fetch only the small official boxable-class table, not image/annotation archives."""
import csv
import hashlib
import io
import json
from pathlib import Path
import urllib.request

ROOT=Path(__file__).resolve().parents[1]
URL='https://storage.googleapis.com/openimages/v5/class-descriptions-boxable.csv'
LIMIT=256*1024
CANDIDATES={'Lighter':['Lighter'],'Tree':['Tree'],'Cockroach':['Cockroach'],'Spider':['Spider'],
            'Statue':['Sculpture','Statue'],'BronzeBeast':['Bronze beast','BronzeBeast'],
            'chair':['Chair'],'couch':['Couch'],'bed':['Bed'],'dining table':['Kitchen & dining room table'],
            'PottedPlant':['Houseplant'],'Cat':['Cat'],'Dog':['Dog']}

def main():
    folder=ROOT/'.codex-tmp/yolo-training/source-metadata';folder.mkdir(parents=True,exist_ok=True)
    target=folder/'openimages-boxable.csv';manifest=folder/'openimages-boxable-source.json'
    if target.exists() and manifest.exists():
        raw=target.read_bytes();old=json.loads(manifest.read_text());assert old['url']==URL and old['sha256']==hashlib.sha256(raw).hexdigest()
    else:
        request=urllib.request.Request(URL,headers={'User-Agent':'MagicMR-training-data-audit/1.0'})
        with urllib.request.urlopen(request,timeout=25) as response:
            if int(response.headers.get('Content-Length','0'))>LIMIT:raise RuntimeError('Metadata exceeds cap')
            raw=response.read(LIMIT+1)
        if len(raw)>LIMIT:raise RuntimeError('Metadata exceeds cap')
        target.write_bytes(raw)
    rows=list(csv.reader(io.StringIO(raw.decode('utf-8-sig'))));assert 500<=len(rows)<=1000 and all(len(row)==2 and row[0].startswith('/m/') for row in rows)
    by_name={label.casefold():mid for mid,label in rows}
    mapping={name:[{'source_class':label,'class_id':by_name[label.casefold()]} for label in options if label.casefold() in by_name] for name,options in CANDIDATES.items()}
    result={'url':URL,'sha256':hashlib.sha256(raw).hexdigest(),'bytes':len(raw),'class_rows':len(rows),
            'scope':'Official class metadata only; candidate mapping is not semantic/data-license/accuracy acceptance',
            'image_downloaded':False,'annotations_downloaded':False,'candidates':mapping,'missing_candidates':[k for k,v in mapping.items() if not v],
            'provenance_review':'Check selected images individually using official provenance; Sculpture and Houseplant require target-semantic review',
            'official_description':'https://storage.googleapis.com/openimages/web/factsfigures_v7.html',
            'official_download':'https://storage.googleapis.com/openimages/web/download_v7.html'}
    manifest.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False))

if __name__=='__main__':main()
