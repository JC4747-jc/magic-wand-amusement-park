"""Audit the new export without modifying existing training data."""
import hashlib
import io
import json
import math
import random
import zipfile
from collections import Counter, defaultdict
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1] / 'Find lighter'
OUT = ROOT / 'audit_lighter_v2'
ZIP = ROOT / 'lighter.v2i.yolo26.zip'
ALLOW_POLYGONS = False
EXT = {'.jpg', '.jpeg', '.png', '.webp'}


def fingerprint(im):
    im = im.convert('RGB')
    digest = hashlib.sha256(str(im.size).encode() + im.tobytes()).hexdigest()
    small = im.convert('L').resize((9, 8))
    px = list(small.getdata())
    dhash = sum((px[y*9+x] > px[y*9+x+1]) << (y*8+x) for y in range(8) for x in range(8))
    return digest, dhash


def main():
    OUT.mkdir(exist_ok=True)
    old = []
    for p in (ROOT / 'detect_merged_v3_quad').glob('*/images/*'):
        if p.suffix.lower() in EXT:
            with Image.open(p) as im:
                digest, dhash = fingerprint(im)
            old.append((str(p.relative_to(ROOT)), digest, dhash))
    rows, errors = [], []
    with zipfile.ZipFile(ZIP) as z:
        crc_error = z.testzip()
        duplicates = {n:c for n,c in Counter(z.namelist()).items() if c > 1}
        for name in sorted(set(z.namelist())):
            if Path(name).suffix.lower() not in EXT:
                continue
            label = str(Path(name).with_suffix('.txt')).replace('\\', '/').replace('/images/', '/labels/')
            try:
                boxes = []
                for line in z.read(label).decode('utf-8-sig').splitlines():
                    if not line.strip():
                        continue
                    v = list(map(float, line.split()))
                    if ALLOW_POLYGONS and len(v) >= 7 and len(v) % 2 == 1:
                        if not all(math.isfinite(t) and 0 <= t <= 1 for t in v[1:]):
                            raise ValueError('Invalid polygon coordinates')
                        xs, ys = v[1::2], v[2::2]
                        v = [v[0], (min(xs)+max(xs))/2, (min(ys)+max(ys))/2, max(xs)-min(xs), max(ys)-min(ys)]
                    if len(v) != 5 or not all(math.isfinite(x) for x in v):
                        raise ValueError('Not a finite 5-column bbox')
                    c,x,y,w,h = v
                    if c != 0 or not (0 <= x <= 1 and 0 <= y <= 1 and 0 < w <= 1 and 0 < h <= 1):
                        raise ValueError('Invalid class or bbox range')
                    boxes.append(v)
                with Image.open(io.BytesIO(z.read(name))) as im:
                    im.load()
                    digest, dhash = fingerprint(im)
                    size = im.size
                rows.append(dict(name=name, split=name.split('/')[0], boxes=boxes, size=size, digest=digest, dhash=dhash))
            except Exception as e:
                errors.append({'name':name, 'error':str(e)})
        groups = defaultdict(list)
        for r in rows:
            groups[r['digest']].append(r['name'])
        exact = [v for v in groups.values() if len(v)>1]
        overlap = [{'new':r['name'], 'old':p} for r in rows for p,d,h in old if r['digest']==d]
        near = []
        for i,r in enumerate(rows):
            for s in rows[:i]:
                distance = (r['dhash'] ^ s['dhash']).bit_count()
                if distance <= 4 and r['digest'] != s['digest']:
                    near.append({'a':r['name'], 'b':s['name'], 'distance':distance, 'cross_split':r['split'] != s['split']})
        old_near = [{'new':r['name'], 'old':p, 'distance':(r['dhash']^h).bit_count()} for r in rows for p,d,h in old if r['digest']!=d and (r['dhash']^h).bit_count() <= 4]
        report = dict(crc_error=crc_error, duplicate_zip_entries=duplicates, images=len(rows), splits=dict(Counter(r['split'] for r in rows)), boxes=sum(len(r['boxes']) for r in rows), empty_labels=sum(not r['boxes'] for r in rows), errors=errors, exact_duplicate_groups=exact, exact_old_overlap=overlap, near_pairs=near, near_old_pairs=old_near, records=rows)
        (OUT/'audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
        samples = random.Random(42).sample(rows,min(60,len(rows)))
        for page in range(3):
            sheet = Image.new('RGB',(1200,1000),'white')
            draw = ImageDraw.Draw(sheet)
            for idx,r in enumerate(samples[page*20:(page+1)*20]):
                im = Image.open(io.BytesIO(z.read(r['name']))).convert('RGB').resize((240,220))
                d=ImageDraw.Draw(im)
                for c,x,y,w,h in r['boxes']:
                    d.rectangle(((x-w/2)*240,(y-h/2)*220,(x+w/2)*240,(y+h/2)*220),outline='red',width=2)
                x0,y0=(idx%5)*240,(idx//5)*250
                sheet.paste(im,(x0,y0))
                draw.text((x0+3,y0+222),str(page*20+idx)+': '+Path(r['name']).name[:29],fill='black')
            sheet.save(OUT/f'samples_{page+1}.jpg')
        print(json.dumps({k:v for k,v in report.items() if k not in ('records','near_pairs','near_old_pairs','exact_duplicate_groups','exact_old_overlap')},ensure_ascii=False))
        print('exact duplicate groups',len(exact),'old overlaps',len(overlap),'near pairs',len(near),'cross split near',sum(p['cross_split'] for p in near),'old near',len(old_near))


if __name__ == '__main__':
    main()
