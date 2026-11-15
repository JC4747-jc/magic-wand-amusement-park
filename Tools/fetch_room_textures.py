"""Fetch six verified 1K CC0 maps from Poly Haven, within a fixed download budget."""
import hashlib
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'Assets/ThirdParty/NaturalDecor/RoomTextures'
ASSETS = ('wood_table_001', 'rough_linen', 'beige_wall_002')
LIMIT = 8 * 1024 * 1024


def request(url, limit):
    with urllib.request.urlopen(urllib.request.Request(url, headers={
            'User-Agent': 'MagicMRAssetPreparation/1.0'}), timeout=60) as response:
        data = response.read(limit + 1)
    if len(data) > limit:
        raise ValueError('Download exceeds size budget: ' + url)
    return data


def main():
    jobs = []
    sources = []
    for asset in ASSETS:
        manifest = json.loads(request('https://api.polyhaven.com/files/' + asset, 4 * 1024 * 1024))
        info = json.loads(request('https://api.polyhaven.com/info/' + asset, 4 * 1024 * 1024))
        sources.append(dict(asset=asset, source='https://polyhaven.com/a/' + asset,
                            license='CC0-1.0', info=info))
        for key, suffix in [('Diffuse', 'diff'), ('nor_gl', 'normal')]:
            item = manifest[key]['1k']['jpg']
            jobs.append((asset + '_' + suffix + '.jpg', item))
    if sum(item['size'] for _, item in jobs) > LIMIT:
        raise ValueError('Room textures exceed the 8 MiB total download budget')
    OUTPUT.mkdir(parents=True, exist_ok=True)
    files = []
    for name, item in jobs:
        path = OUTPUT / name
        data = path.read_bytes() if path.exists() else b''
        if len(data) != item['size'] or hashlib.md5(data).hexdigest() != item['md5']:
            data = request(item['url'], item['size'])
            if len(data) != item['size'] or hashlib.md5(data).hexdigest() != item['md5']:
                raise ValueError('Manifest checksum mismatch: ' + name)
            path.write_bytes(data)
        files.append(dict(path=name, bytes=len(data), url=item['url'], sha256=hashlib.sha256(data).hexdigest()))
    (OUTPUT / 'sources.json').write_text(json.dumps(dict(sources=sources, files=files), indent=2), encoding='utf-8')
    (OUTPUT / 'LICENSE.txt').write_text('All maps: CC0 1.0, https://creativecommons.org/publicdomain/zero/1.0/\n'
        + '\n'.join(s['source'] + ' — see sources.json for original authors and file hashes' for s in sources)
        + '\nOriginal 1K colour and OpenGL normal maps; no generated textures or model training.\n', encoding='utf-8')
    print(json.dumps(dict(files=len(files), bytes=sum(f['bytes'] for f in files), output=str(OUTPUT))))


if __name__ == '__main__':
    main()
