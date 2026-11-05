"""Freeze pre-inference source decisions and manually reviewed draft boxes."""
import hashlib
import html
import json
import re
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Reports/statue-independent-v22-boxes'
BOXES = {
    46472562: [[85, 35, 908, 905]],
    75908598: [[175, 22, 867, 680], [189, 251, 384, 607], [500, 239, 687, 456], [696, 431, 820, 656]],
    113009697: [[184, 74, 857, 1453]],
    179100901: [[285, 155, 868, 1237]],
    45374298: [[198, 203, 809, 1098]],
    45756467: [[649, 79, 861, 481]],
    45105771: [[106, 177, 877, 987]],
    123655719: [[311, 173, 546, 706]],
    82534150: [[352, 0, 628, 306]],
    68506694: [[338, 212, 613, 836]],
    80080423: [[712, 105, 805, 281]],
    39354795: [[154, 79, 433, 413]],
    15001683: [[259, 78, 377, 371]],
    2273236: [[186, 37, 846, 682]],
    34609639: [[0, 369, 191, 720], [94, 337, 324, 717], [215, 261, 553, 709],
               [444, 393, 640, 696], [608, 207, 923, 680], [883, 96, 960, 455]],
}
HOLDS = {
    58228525: 'Unknown photographer; credit A.S. (Alkema Sweerts) lacks verified creator identity.',
    132489272: 'Small articulated humanoid display in robotics lab: cannot establish statue versus functioning robot from available photo/description. Whole-image semantic HOLD before inference.',
}
def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))
def clean(s):
    return html.unescape(re.sub('<[^>]+>', ' ', s)).casefold()
def main():
    assert not (OUT/'draft.json').exists(), 'Never overwrite completed draft/review evidence.'
    OUT.mkdir(exist_ok=True)
    evidence, rows = [], []
    for pool in ['women', 'men', 'vr']:
        source = ROOT / f'Datasets/statue-{pool}-independent-source-v22'
        manifest = source / 'manifest.json'
        m = read(manifest)
        page_files = sorted(source.glob('publisher-category-page-*.json'))
        pages = {}
        for page in page_files:
            pages.update(read(page)['query']['pages'])
        for p in [manifest, *page_files, source/'test-reservation.json',
                  ROOT/f'Reports/statue-independent-v22-{pool}-original-review.json',
                  ROOT/f'Reports/statue-independent-v22-{pool}-pixel-history.json',
                  ROOT/f'Reports/statue-independent-v22-{pool}-creator-links.json',
                  ROOT/f'Reports/statue-independent-v22-{pool}-identity-history.json']:
            evidence.append(dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)))
        for s in m['samples']:
            pid = s['page_id']
            pub = pages[str(pid)]
            info = pub['imageinfo'][0]
            assert pub['pageid'] == pid and pub['title'] == s['title']
            assert info['extmetadata'] == s['publisher_metadata']
            assert info.get('thumburl', info['url']) == s['photo_url']
            assert sha(source/s['image']) == s['sha256']
            assert s['license'] == info['extmetadata']['LicenseShortName']['value']
            assert s['license_url'] == info['extmetadata']['LicenseUrl']['value']
            assert s['license'] in ['CC BY 2.0', 'CC BY 3.0', 'CC BY 4.0', 'CC BY-SA 3.0', 'CC BY-SA 4.0', 'CC0']
            image = source / s['image']
            w, h = Image.open(image).size
            boxes = BOXES.get(pid, [])
            assert all(0 <= x1 < x2 <= w and 0 <= y1 < y2 <= h for x1,y1,x2,y2 in boxes)
            group = s['group']
            if pid in [45374298,45105771]:
                group = 'commons:creator:jurgen-friede'
            row = dict(page_id=pid, source_pool=pool, image=image.relative_to(ROOT).as_posix(),
                       sha256=s['sha256'], dimensions=[w,h], boxes=boxes,
                       source_group=s['group'], test_group=group, split='test',
                       original_reviewed=True, overlay_reviewed=False,
                       status='HOLD' if pid in HOLDS else 'DRAFT_PREINFERENCE',
                       hold_reason=HOLDS.get(pid), license=s['license'],
                       license_url=s['license_url'], artist=s['artist'])
            if pid not in HOLDS:
                im = Image.open(image).convert('RGB'); d = ImageDraw.Draw(im)
                for i, box in enumerate(boxes):
                    d.rectangle(box, outline='red', width=3)
                    d.text((box[0]+3,box[1]+3), f'Statue {i+1}', fill='red')
                overlay = OUT / f'{pid}.jpg'
                if not overlay.exists():
                    im.save(overlay, quality=94)
                row.update(overlay=overlay.relative_to(ROOT).as_posix(), overlay_sha256=sha(overlay))
            rows.append(row)
    # Search historical creator fields for aliases missed by whole-string grouping.
    audits = read(ROOT/'Reports/statue-independent-v22-men-creator-links.json')['historical_manifests']
    alias_hits=[]
    for a in audits:
        p = ROOT / a['path']
        assert sha(p) == a['sha256']
        for s in read(p).get('samples', []):
            text = clean(str(s.get('artist',''))+' '+str(s.get('publisher_metadata',{})))
            if any(token in text for token in ['friede','campuspartybrasil','campus  party brasil']):
                alias_hits.append(dict(manifest=a['path'],image=s.get('image'),artist=s.get('artist')))
    assert not alias_hits, alias_hits
    men = read(ROOT/'Datasets/statue-men-independent-source-v22/manifest.json')['samples']
    fresh = next(s for s in men if s['page_id']==2273236)
    oldfile = ROOT/'Datasets/statue-rear-train-source-v17/manifest.json'
    old = next(s for s in read(oldfile)['samples'] if s['page_id']==6820057)
    assert '70103020@N00' in fresh['publisher_metadata']['Artist']['value']
    assert '10422465@N00' in old['publisher_metadata']['Artist']['value']
    decision = dict(
        status='PREINFERENCE_SOURCE_DECISIONS_AND_DRAFT_BOXES',
        candidate_sha256='2508435a84ea8ec907ac4bdb1aa1141c7f58adde20c126582b4557bb1ae84dfb',
        model_inference_performed=False, labels_approved=False, inputs=evidence,
        uploader_false_match=dict(page_id=2273236, historical_page_id=6820057,
            historical_manifest_sha256=sha(oldfile),
            artist_flickr_ids=['70103020@N00','10422465@N00'],
            resolution='Different Artist creator IDs; broad-history hits came from uploader/reviewer fields. Raw report preserved; does not prove every possible identity alias.'),
        author_alias_resolution=dict(page_ids=[45374298,45105771], shared_group='commons:creator:jurgen-friede',
            basis='Artist links name the same person. Publisher descriptions name distinct sculptures: 2001 kecker Maedchenkopf diabase 53x45x11 and 2002 Die Unberuehrbare basalt 50x23x48. Keep both TEST in one creator group.'),
        credit_creator_resolution=dict(page_id=179649522,
            creator_credit_url='https://www.flickr.com/photos/campuspartybrasil/54950782739/',
            basis='Artist is plain text but Credit supplies photographer account; broad identity audit and historical alias search have no hits. No invented Flickr NSID.'),
        alias_history_hits=alias_hits,
        census_corrections=['34609639 has SIX visible heads including the cropped far-right head.',
                           '75908598 has mother and THREE children: four separate figures.',
                           '123655719 background detail crop shows irregular wood/foliage, not a confirmed second figure; main bronze statue only.'],
        policy='All 24 photos including two semantic/identity holds remain TEST-only. No performance-based exclusion. No model or threshold changes. Public-source pilot, unknown pretraining overlap; not PICO acceptance.',
        samples=rows)
    (OUT/'draft.json').write_text(json.dumps(decision,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(photos=len(rows),annotatable=sum(r['status']!='HOLD' for r in rows),
                          holds=len(HOLDS),positive_boxes=sum(len(r['boxes']) for r in rows),
                          creator_groups=len({r['test_group'] for r in rows}))))
if __name__ == '__main__':
    main()
