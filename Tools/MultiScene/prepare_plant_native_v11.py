"""Release four reviewed TRAIN negatives only after metadata and pixel audits."""
import hashlib,json,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    src=ROOT/'Datasets/plant-cutflower-v11-staging';parent=ROOT/'Datasets/plant-native-v10';out=ROOT/'Datasets/plant-native-v11'
    assert not out.exists(),'Immutable output exists'
    m=json.loads((src/'manifest.json').read_text());p=ROOT/'Reports/plant-cutflower-v11-pixel-history.json';pixel=json.loads(p.read_text())
    assert pixel['source_manifest_sha256']==sha(src/'manifest.json') and not pixel['errors'] and not pixel['internal_matches']
    expected={r['image']:r for r in m['samples']}
    matched=set()
    for hit in pixel['matches']:
        assert hit['candidate_id'] in expected
        row=expected[hit['candidate_id']]
        original=f"openimages-plant-fresh-general-source-v4/images/{row['original_id']}.jpg"
        assert hit['historical_image'].replace('\\','/')==original,hit
        assert hit['encoded_exact'] and hit['decoded_exact'];matched.add(hit['candidate_id'])
    assert matched==set(expected),'Missing known parent lineage'
    metadata=ROOT/'Reports/plant-cutflower-v11-source-reservation-audit.json';meta=json.loads(metadata.read_text())
    assert not meta['errors']
    for row in meta['records']:
        assert row['path'].replace('\\','/')=='Datasets/openimages-plant-fresh-general-source-v4/manifest.json'
        assert row['split'] is None and row['reservation'] is None
    a=json.loads((parent/'audit.json').read_text());assert sha(parent/'audit.json')=='e74e2af7b6be77fd03fc2c95544c6d27fe55911c3df25fc630b33d28db1f3cdc'
    old_groups={r['group'].lower().replace('openimages-v7:','').replace('www.','').rstrip('/') for r in a['samples']}
    for row in m['samples']:
        assert row['group'].lower().replace('www.','').rstrip('/') not in old_groups
        assert row['reserved_split']=='train' and row['negative'] and row['review_approved']
        assert sha(src/row['image'])==row['sha256'] and sha(src/row['label'])==row['label_sha256']
        assert not (src/row['label']).read_text().strip()
    shutil.copytree(parent,out)
    for cache in out.rglob('*.cache'):cache.unlink()
    for row in m['samples']:
        image=Path('images/train')/Path(row['image']).name;label=Path('labels/train')/Path(row['label']).name
        shutil.copy2(src/row['image'],out/image);shutil.copy2(src/row['label'],out/label)
        a['samples'].append(dict(row,image=str(image),label=str(label),split='train',source_image=str((src/row['image']).relative_to(ROOT))))
    a.update(unique_images=len(a['samples']),parent_audit_sha256=sha(parent/'audit.json'),new_negative_source_sha256=sha(src/'manifest.json'),pixel_clearance_sha256=sha(p),reservation_clearance_sha256=sha(metadata),scope='Four reviewed original cut-flower negatives added to TRAIN; all parent samples/folds/labels retained; publisher test split is not project TEST',deployment_accepted=False)
    (out/'audit.json').write_text(json.dumps(a,indent=2))
    cfg=json.loads((parent/'data.yaml').read_text());cfg['path']=str(out);(out/'data.yaml').write_text(json.dumps(cfg,indent=2))
    print(json.dumps(dict(images=len(a['samples']),train=sum(r['split']=='train' for r in a['samples']),val=sum(r['split']=='val' for r in a['samples']),test=sum(r['split']=='test' for r in a['samples']))))
if __name__=='__main__':main()
