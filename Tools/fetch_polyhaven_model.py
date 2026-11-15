"""Fetch one bounded 1K CC0 glTF from the official manifest, preserving provenance."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import urllib.request


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('asset')
    parser.add_argument('--max-mib',type=int,default=20,choices=range(1,129),metavar='1..128')
    parser.add_argument('--extra-map',action='append',default=[],help='Additional original 1K JPEG map from the official manifest')
    args=parser.parse_args()
    if not args.asset.replace('_','').isalnum():
        parser.error('Use the Poly Haven asset identifier')
    root=Path(__file__).resolve().parents[1]
    output=root/'.codex-tmp/asset-candidates'/('polyhaven_'+args.asset)
    output.mkdir(parents=True,exist_ok=True)
    headers={'User-Agent':'MagicMRAssetPreparation/1.0'}
    def metadata(endpoint):
        with urllib.request.urlopen(urllib.request.Request('https://api.polyhaven.com/'+endpoint+'/'+args.asset,headers=headers),timeout=30) as response:
            return json.loads(response.read(4*1024*1024))
    manifest=metadata('files')
    info=metadata('info')
    model=manifest['gltf']['1k']['gltf']
    files={args.asset+'_1k.gltf':model,**model['include']}
    for key in args.extra_map:
        if not key.replace('_','').isalnum():parser.error('Invalid extra map identifier')
        files['textures/'+args.asset+'_'+key+'_1k.jpg']=manifest[key]['1k']['jpg']
    total=sum(item['size'] for item in files.values())
    if total>args.max_mib*1024*1024:
        raise ValueError('1K model exceeds the selected download budget: '+str(total))
    if shutil.disk_usage(output).free<5*1024**3+total*3:
        raise ValueError('Insufficient physical disk headroom for candidate and conversion')
    verified=[]
    for name,item in files.items():
        destination=(output/name).resolve()
        if not destination.is_relative_to(output.resolve()):
            raise ValueError('Manifest path escaped candidate directory')
        destination.parent.mkdir(parents=True,exist_ok=True)
        def digest(path,algorithm):
            checksum=hashlib.new(algorithm)
            with path.open('rb') as stream:
                for chunk in iter(lambda:stream.read(256*1024),b''):checksum.update(chunk)
            return checksum.hexdigest()
        if not (destination.exists() and destination.stat().st_size==item['size'] and digest(destination,'md5')==item['md5']):
            temporary=destination.with_name(destination.name+'.part')
            try:
                request=urllib.request.Request(item['url'],headers=headers)
                received=0;checksum=hashlib.md5()
                with urllib.request.urlopen(request,timeout=60) as response,temporary.open('wb') as stream:
                    for chunk in iter(lambda:response.read(256*1024),b''):
                        received+=len(chunk)
                        if received>item['size']:raise ValueError('Download exceeds manifest size: '+name)
                        stream.write(chunk);checksum.update(chunk)
                if received!=item['size'] or checksum.hexdigest()!=item['md5']:
                    raise ValueError('Manifest size/checksum mismatch: '+name)
                temporary.replace(destination)
            finally:
                if temporary.exists():temporary.unlink()
        verified.append(dict(path=name,bytes=destination.stat().st_size,sha256=digest(destination,'sha256'),url=item['url']))
    provenance=dict(source='https://polyhaven.com/a/'+args.asset,license='CC0-1.0',
                    info=info,files=verified,total_bytes=total,resolution='1k')
    (output/'source.json').write_text(json.dumps(provenance,indent=2),encoding='utf-8')
    print(json.dumps(dict(asset=args.asset,bytes=total,output=str(output)),indent=2))


if __name__=='__main__':
    main()
