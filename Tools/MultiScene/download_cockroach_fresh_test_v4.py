"""Bounded fresh observer TEST acquisition, no inference or training approval."""
import hashlib
import argparse
import io
import json
from pathlib import Path
from urllib.parse import urlsplit
import requests
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]


def walk(value):
    if isinstance(value,dict):
        yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value,list):
        for child in value:
            yield from walk(child)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out',default='Datasets/cockroach-fresh-test-v4-source')
    parser.add_argument('--reserved-split',choices=['train','val','test'],default='test')
    parser.add_argument('--max-images',type=int,default=12)
    parser.add_argument('--taxon-id',type=int,default=82231,help='Publisher taxon filter, never automatic target/negative annotation')
    parser.add_argument('--model-sha256',default='cb7262e883d7787a2ba30434807ea94768ec545836f6055b9a7f4aa36af96da0')
    args=parser.parse_args()
    if not 1<=args.max_images<=60:
        parser.error('--max-images must be between 1 and 60')
    if args.taxon_id<1 or len(args.model_sha256)!=64 or any(c not in '0123456789abcdef' for c in args.model_sha256):
        parser.error('Positive taxon ID and lowercase SHA256 required')
    out=(ROOT/args.out).resolve()
    out.relative_to(ROOT/'Datasets')
    if out.exists():
        raise FileExistsError(out)
    seen=set();ids=set();bindings={}
    for path in sorted((ROOT/'Datasets').rglob('*.json')):
        if path.name not in {'manifest.json','audit.json','reservation.json'}:
            continue
        payload=json.loads(path.read_text(encoding='utf-8-sig'))
        bindings[path.relative_to(ROOT).as_posix()]=hashlib.sha256(path.read_bytes()).hexdigest()
        for row in walk(payload):
            group=row.get('group')
            if isinstance(group,str) and group.startswith('inaturalist:user:'):
                seen.add(group)
            if row.get('source')=='inaturalist' and row.get('original_id'):
                ids.add(str(row['original_id']))
    out.mkdir();(out/'images').mkdir()
    (out/'exclusion-snapshot.json').write_text(json.dumps(dict(groups=sorted(seen),observation_ids=sorted(ids),bindings=bindings),indent=2))
    session=requests.Session();session.headers['User-Agent']='MagicMRParkDatasetReview/1.0 (research, attribution retained)'
    rows=[];failures=[]
    for page in range(1,5):
        response=session.get('https://api.inaturalist.org/v1/observations',params=dict(taxon_id=args.taxon_id,
            quality_grade='research',photo_license='cc0,cc-by',per_page=100,order_by='id',order='desc',page=page),timeout=30)
        response.raise_for_status();payload=response.json()
        (out/f'publisher-page-{page}.json').write_text(json.dumps(payload,indent=2))
        for observation in payload['results']:
            group=f'inaturalist:user:{observation["user"]["id"]}'
            if group in seen or str(observation['id']) in ids:
                continue
            photos=[p for p in observation.get('photos',[]) if p.get('license_code') in {'cc0','cc-by'}]
            if not photos:
                continue
            photo=photos[0];url=photo['url'].replace('/square.','/large.')
            if urlsplit(url).scheme!='https' or urlsplit(url).hostname not in {'inaturalist-open-data.s3.amazonaws.com','static.inaturalist.org','inaturalist-open-data.s3.us-west-2.amazonaws.com'}:
                failures.append(dict(observation_id=observation['id'],reason='UNREVIEWED_IMAGE_HOST'));continue
            try:
                response=session.get(url,timeout=30);response.raise_for_status();raw=response.content
                if len(raw)>8*1024**2:
                    raise ValueError('Image byte budget exceeded')
                with Image.open(io.BytesIO(raw)) as frame:
                    rgb=frame.convert('RGB');dimensions=list(rgb.size)
                    pixel=hashlib.sha256(f'RGB:{rgb.width}:{rgb.height}:'.encode()+rgb.tobytes()).hexdigest()
                name=f'{observation["id"]}-{photo["id"]}.jpg';(out/'images'/name).write_bytes(raw)
                rows.append(dict(image='images/'+name,source='inaturalist',group=group,original_id=str(observation['id']),
                    photo_id=photo['id'],query_taxon_id=args.taxon_id,taxon_id=observation['taxon']['id'],taxon_name=observation['taxon']['name'],license=photo['license_code'],
                    attribution=photo.get('attribution'),observation_url=f'https://www.inaturalist.org/observations/{observation["id"]}',
                    photo_url=url,sha256=hashlib.sha256(raw).hexdigest(),decoded_rgb_sha256=pixel,dimensions=dimensions,
                    reserved_split=args.reserved_split,training_approved=False,evaluation_approved=False,annotation_status='UNREVIEWED'))
                seen.add(group);print('Downloaded',len(rows),flush=True)
            except (requests.RequestException,OSError,ValueError) as error:
                failures.append(dict(observation_id=observation['id'],reason=type(error).__name__))
            if len(rows)>=args.max_images:
                break
        if len(rows)>=args.max_images or not payload['results']:
            break
    (out/'manifest.json').write_text(json.dumps(dict(names=['Cockroach'],samples=rows,failures=failures,
        model_sha256=args.model_sha256,
        training_approved=False,evaluation_approved=False,scope=f'Publisher taxon query {args.taxon_id}, {args.reserved_split}-only; no automatic presence/absence labels; visual review required',
        policy=f'Pre-inference {args.reserved_split} reservation; no fold reassignment. Observer aliases/near duplicates still need audit.'),indent=2))
    print(json.dumps(dict(images=len(rows),failures=len(failures))))


if __name__=='__main__':
    main()
