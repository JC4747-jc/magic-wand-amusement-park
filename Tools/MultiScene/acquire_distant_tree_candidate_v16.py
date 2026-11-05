"""Acquire one named distant-tree candidate, metadata and TRAIN reservation before inference."""
import hashlib
import html
import io
import json
import re
from pathlib import Path
from urllib.parse import urlsplit

import requests
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]


def main():
    title='File:Beautiful view across paddy fields to Takeshi Kaneshiro Tree in the distance.jpg'
    out=ROOT/'Datasets/commons-distant-tree-v16-source'
    if out.exists():
        raise FileExistsError(out)
    session=requests.Session()
    session.headers['User-Agent']='MagicMRParkDatasetReview/1.0 (local research; attribution retained)'
    response=session.get('https://commons.wikimedia.org/w/api.php',params=dict(action='query',format='json',titles=title,
        prop='imageinfo',iiprop='url|extmetadata|size|sha1',iiurlwidth=960),timeout=30)
    response.raise_for_status()
    metadata=response.json()
    item=next(iter(metadata['query']['pages'].values()))
    info=item['imageinfo'][0];meta=info['extmetadata']
    license_name=meta['LicenseShortName']['value']
    if license_name not in {'CC BY 2.0','CC BY 3.0','CC BY 4.0','CC BY-SA 4.0','CC0'}:
        raise ValueError('Unreviewed license '+license_name)
    artist=html.unescape(re.sub('<[^>]*>','',meta['Artist']['value'])).strip()
    known=json.loads((ROOT/'Reports/tree-independent-test-known-artists-v1.json').read_text(encoding='utf-8'))
    names=known['artists'] if isinstance(known,dict) else known
    assert artist and artist.casefold() not in {n.casefold() for n in names}
    for source in ['commons-solitary-tree-independent-test-source-v1','commons-shrub-independent-test-source-v1']:
        rows=json.loads((ROOT/'Datasets'/source/'manifest.json').read_text(encoding='utf-8'))['samples']
        assert artist.casefold() not in {r['artist'].casefold() for r in rows}
    url=info['thumburl']
    assert urlsplit(url).hostname in {'upload.wikimedia.org','thumb.wikimedia.org'}
    response=session.get(url,timeout=30);response.raise_for_status();raw=response.content
    assert len(raw)<8*1024**2
    with Image.open(io.BytesIO(raw)) as image:
        rgb=image.convert('RGB');size=list(rgb.size)
        pixel=hashlib.sha256(f'RGB:{rgb.width}:{rgb.height}:'.encode()+rgb.tobytes()).hexdigest()
    ident=item['pageid'];digest=hashlib.sha256(raw).hexdigest()
    row=dict(page_id=ident,image=f'images/{ident}.jpg',source='commons',artist=artist,
             group='commons:artist:'+hashlib.sha256(artist.casefold().encode()).hexdigest(),title=title,
             license=license_name,license_url=meta['LicenseUrl']['value'],publisher_metadata=meta,
             description_url=info['descriptionurl'],photo_url=url,sha256=digest,decoded_rgb_sha256=pixel,
             dimensions=size,reserved_split='train',training_approved=False,annotation_status='UNREVIEWED',
             physical_subject='Takeshi Kaneshiro Tree; cross-author same-instance audit required')
    (out/'images').mkdir(parents=True)
    (out/row['image']).write_bytes(raw)
    (out/'publisher-metadata.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
    (out/'manifest.json').write_text(json.dumps(dict(names=['Tree'],samples=[row],training_approved=False),ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(page_id=ident,license=license_name,dimensions=size,training_approved=False)))


if __name__=='__main__':
    main()
