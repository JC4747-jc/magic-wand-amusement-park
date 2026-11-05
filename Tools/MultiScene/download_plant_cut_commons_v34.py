"""Bounded Commons source acquisition; metadata is evidence, not annotation approval."""
import argparse,hashlib,html,io,json,re,time,shutil
from pathlib import Path
from urllib.parse import urlsplit
import requests
from PIL import Image
parser=argparse.ArgumentParser();parser.add_argument('--out',default='Datasets/plant-cut-train-source-v34');parser.add_argument('--metadata-source');parser.add_argument('--exclude',nargs='*',default=[]);parser.add_argument('--exclude-images',nargs='*',default=[],help='Exclude existing decoded images without excluding their authors; preserve source-author folds when adding more photos');parser.add_argument('--max-per-artist',type=int,default=1);parser.add_argument('--max-images',type=int,default=60);parser.add_argument('--max-pages',type=int,default=4);parser.add_argument('--category',default='Category:Flowers in vases');parser.add_argument('--include-sharealike4',action='store_true',help='Also acquire CC BY-SA4 sources with attribution/share-alike obligations retained; not training approval');parser.add_argument('--exclude-artist-file',help='JSON list or artists field of previously recorded author names; case-insensitive exclusion');parser.add_argument('--target-class',default='CutArrangement',choices=['CutArrangement']);args=parser.parse_args()
if not 1<=args.max_pages<=12:parser.error('--max-pages must be between1 and12')
if not args.category.startswith('Category:'):parser.error('--category must be a Commons category title')
if not 1<=args.max_per_artist<=5:parser.error('--max-per-artist must be between1 and5')
if not 1<=args.max_images<=60:parser.error('--max-images must be between1 and60')
root=Path(__file__).resolve().parents[2]
if args.metadata_source:
 cached_config=root/args.metadata_source/'acquisition.json'
 cached_category=json.loads(cached_config.read_text(encoding='utf-8-sig'))['category'] if cached_config.exists() else 'Category:Flowers in vases'
 if cached_category!=args.category:parser.error('Cached metadata category does not match requested category')
out=root/args.out
if (out/'manifest.json').exists():raise ValueError('Immutable manifest already exists')
out.mkdir(exist_ok=True);(out/'images').mkdir(exist_ok=True)
if args.metadata_source:
 for cached in (root/args.metadata_source).glob('publisher-category-page-*.json'):shutil.copy2(cached,out/cached.name)
s=requests.Session();s.headers['User-Agent']='MagicMRParkDatasetReview/1.0 (local research; attribution retained)'
allowed={'CC BY 2.0','CC BY 3.0','CC BY 4.0','CC0'}
if args.include_sharealike4:allowed.add('CC BY-SA 4.0')
(out/'acquisition.json').write_text(json.dumps(dict(category=args.category,max_pages=args.max_pages,max_per_artist=args.max_per_artist,max_images=args.max_images,metadata_source=args.metadata_source,allowed_licenses=sorted(allowed),exclude=args.exclude,exclude_images=args.exclude_images,exclude_artist_file=args.exclude_artist_file,exclude_artist_file_sha256=hashlib.sha256((root/args.exclude_artist_file).read_bytes()).hexdigest() if args.exclude_artist_file else None,sharealike_source_obligations='Retain attribution,license link,change notices; adapted material shared under same license when distributed. Model/dataset release needs its own review.',source='https://commons.wikimedia.org/w/api.php',training_approved=False),indent=2),encoding='utf-8')
rows=[];artists=set();artist_counts={};seen=set();failures=[];continuation={}
for manifest in args.exclude:
 for row in json.loads((root/manifest).read_text(encoding='utf-8'))['samples']:artists.add(row['artist'].casefold());seen.add(row['decoded_rgb_sha256'])
if args.exclude_artist_file:
 author_data=json.loads((root/args.exclude_artist_file).read_text(encoding='utf-8-sig'))
 author_names=author_data.get('artists') if isinstance(author_data,dict) else author_data
 if not isinstance(author_names,list) or not all(isinstance(n,str) and n.strip() for n in author_names):raise ValueError('Expected nonempty author name strings')
 artists.update(n.casefold() for n in author_names)
for manifest in args.exclude_images:
 for row in json.loads((root/manifest).read_text(encoding='utf-8-sig'))['samples']:seen.add(row['decoded_rgb_sha256'])
for page in range(1,args.max_pages+1):
 if (out/f'publisher-category-page-{page}.json').exists():data=json.loads((out/f'publisher-category-page-{page}.json').read_text(encoding='utf-8'))
 else:
  params=dict(action='query',format='json',generator='categorymembers',gcmtitle=args.category,gcmtype='file',gcmlimit=50,prop='imageinfo',iiprop='url|extmetadata|size|sha1',iiurlwidth=640,**continuation)
  response=s.get('https://commons.wikimedia.org/w/api.php',params=params,timeout=25);response.raise_for_status();data=response.json();(out/f'publisher-category-page-{page}.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
 for item in data.get('query',{}).get('pages',{}).values():
  for info in item.get('imageinfo',[]):
   meta=info.get('extmetadata',{});license=meta.get('LicenseShortName',{}).get('value','');artist=html.unescape(re.sub('<[^>]*>','',meta.get('Artist',{}).get('value',''))).strip()
   if license not in allowed or not artist or artist.casefold() in artists or artist_counts.get(artist.casefold(),0)>=args.max_per_artist:continue
   url=info.get('thumburl','')
   if urlsplit(url).hostname not in {'upload.wikimedia.org','thumb.wikimedia.org'}:continue
   try:
    response=s.get(url,timeout=30);response.raise_for_status();raw=response.content
    if len(raw)>8*1024**2:raise ValueError('Image budget exceeded')
    with Image.open(io.BytesIO(raw)) as im:
     im.load();rgb=im.convert('RGB');pixel=hashlib.sha256(f'RGB:{rgb.width}:{rgb.height}:'.encode()+rgb.tobytes()).hexdigest();size=rgb.size
    if pixel in seen:continue
    filename=f'images/{item["pageid"]}.jpg';(out/filename).write_bytes(raw)
    rows.append(dict(image=filename,source='commons',group='commons:artist:'+hashlib.sha256(artist.casefold().encode()).hexdigest(),artist=artist,title=item['title'],page_id=item['pageid'],license=license,license_url=meta.get('LicenseUrl',{}).get('value'),attribution_required=meta.get('AttributionRequired',{}).get('value'),publisher_metadata=meta,description_url=info.get('descriptionurl'),photo_url=url,sha256=hashlib.sha256(raw).hexdigest(),decoded_rgb_sha256=pixel,dimensions=size,annotation_status='UNLABELED'))
    artist_counts[artist.casefold()]=artist_counts.get(artist.casefold(),0)+1;seen.add(pixel);print('Downloaded',len(rows),'licensed source',flush=True)
   except (requests.RequestException,OSError,ValueError) as e:failures.append({'page_id':item['pageid'],'error':type(e).__name__})
   if len(rows)>=args.max_images:break
  if len(rows)>=args.max_images:break
 continuation=data.get('continue',{})
 if not continuation or len(rows)>=args.max_images:break
(out/'manifest.json').write_text(json.dumps(dict(names=[args.target_class],samples=rows,failures=failures,training_approved=False,deployment_accepted=False,project_split='UNASSIGNED',scope=f'At most {args.max_per_artist} photos per normalized artist in current pool. All photos of a repeated source author must retain the same reserved fold. Same physical subject across artists and historical overlaps still require audit; category does not prove image suitability or target absence.'),ensure_ascii=False,indent=2),encoding='utf-8')
print('COMPLETE',len(rows),'images',len(failures),'failures',flush=True)
