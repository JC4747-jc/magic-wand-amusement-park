"""Append two audited flowering-container TRAIN images; preserve parent partitions."""
import hashlib,json,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def norm(g):return g.lower().replace('openimages-v7:','').replace('http:','https:').replace('www.','').rstrip('/')
def main():
    src=ROOT/'Datasets/plant-flowering-v12-staging';parent=ROOT/'Datasets/plant-native-v11';out=ROOT/'Datasets/plant-native-v12';assert not out.exists()
    m=json.loads((src/'manifest.json').read_text());pixel_path=ROOT/'Reports/plant-flowering-v12-pixel-history.json';pixel=json.loads(pixel_path.read_text())
    assert pixel['source_manifest_sha256']==sha(src/'manifest.json') and not pixel['errors'] and not pixel['internal_matches']
    expected={r['image']:r for r in m['samples']};matched=set()
    for hit in pixel['matches']:
        r=expected[hit['candidate_id']];assert hit['historical_image'].replace('\\','/')==f"openimages-plant-expansion/images/{r['original_id']}.jpg",hit
        assert hit['encoded_exact'] and hit['decoded_exact'];matched.add(hit['candidate_id'])
    assert matched==set(expected)
    meta_path=ROOT/'Reports/plant-flowering-v12-source-reservation-audit.json';meta=json.loads(meta_path.read_text());assert not meta['errors']
    for hit in meta['records']:
        assert hit['path'].replace('\\','/')=='Datasets/openimages-plant-expansion/manifest.json' and hit['split'] is None and hit['reservation'] is None
    assert sha(parent/'audit.json')=='de41a31a94cdb7c4c435c2a020cbb386970cc971291da71ff65ebbb6b13bf8ea'
    a=json.loads((parent/'audit.json').read_text());assert len(a['samples'])==83
    groups={norm(r['group']) for r in a['samples']}
    for r in m['samples']:
        assert norm(r['group']) not in groups and r['reserved_split']=='train' and r['review_approved']
        assert sha(src/r['image'])==r['sha256'] and sha(src/r['label'])==r['label_sha256']
        assert len((src/r['label']).read_text().splitlines())==2
    shutil.copytree(parent,out)
    for p in out.rglob('*.cache'):p.unlink()
    for r in m['samples']:
        ip=Path('images/train')/Path(r['image']).name;lp=Path('labels/train')/Path(r['label']).name;shutil.copy2(src/r['image'],out/ip);shutil.copy2(src/r['label'],out/lp)
        a['samples'].append(dict(r,image=str(ip),label=str(lp),split='train',source_image=str((src/r['image']).relative_to(ROOT))))
    a.update(unique_images=len(a['samples']),parent_audit_sha256=sha(parent/'audit.json'),new_positive_source_sha256=sha(src/'manifest.json'),pixel_clearance_sha256=sha(pixel_path),reservation_clearance_sha256=sha(meta_path),scope='Two reviewed flowering-container images/four boxes added to TRAIN; all parent images/labels/folds retained',deployment_accepted=False)
    (out/'audit.json').write_text(json.dumps(a,indent=2));cfg=json.loads((parent/'data.yaml').read_text());cfg['path']=str(out);(out/'data.yaml').write_text(json.dumps(cfg,indent=2));print(json.dumps(dict(images=len(a['samples']),counts={s:sum(r['split']==s for r in a['samples']) for s in ['train','val','test']})))
if __name__=='__main__':main()
