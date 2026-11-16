"""Freeze visually reviewed public bug photos for a small candidate experiment, never deployment."""
from collections import Counter,defaultdict
import hashlib,json,math,os
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'.codex-tmp/hci-bugs-training-20261116'
REVIEW=ROOT/'TrainingData/bugs-bootstrap-review-v1.json'
SPLIT=ROOT/'TrainingData/bugs-bootstrap-split-v1.json'
# Original photographs were individually inspected at full resolution on 2026-11-16.
# Visible body/legs, no web/shadow; clipped extents end at the image edge. Not amodal boxes.
DECISIONS={
 '850bfdf25e95a3ab':([[134,16,913,643]],'Jumping spider; include fine leg tips, exclude plastic-container highlights.'),
 '1d1ef6def56f4b91':([[297,177,810,729]],'Ground spider beside fabric; include every visible leg tip, exclude shadows.'),
 '4e36a6a84c203fe7':([], 'QUARANTINE: spider holds an insect prey whose Cockroach/non-Cockroach identity is unresolved; not an empty negative.'),
 '0b3097261f961399':([[230,0,678,683]],'Orb spider: upper and lower legs clipped by photograph; extend to image boundaries, exclude web.'),
 '1f7bef3366b59a08':([[148,221,802,665]],'Tarantula on ground; include blurred rear legs and fur, not background rock.'),
 '2da7d83a6b68debc':([[97,51,953,673]],'Orange/black tarantula, all visible legs including lower hair tips.'),
 '290e908272641aef':([[164,77,850,664],[740,538,891,662]],'Two spiders: keep a separate small instance despite overlapping/interleaved legs.'),
 '38b95b228d85e5bb':([[120,200,596,794]],'Orb spider: include upper/lower leg tips; web excluded.'),
 '3b4d1022c2be3076':([[0,5,978,576]],'Hairy tarantula partially clipped at left/bottom; visible-extent convention, no inferred missing legs.'),
 '4d1a7164a8e856ad':([[29,32,984,917]],'Single brown spider; include long visible legs, exclude cast shadow.'),
 '1ee2cfb26b6fe8e4':([[196,108,718,939]],'Spider on leaf; correct top leg extent, exclude leaf and web.'),
 '050a2dbc5e91bb68':([[260,135,697,861]],'Soft-focus real spider; include leg tips, exclude surrounding silk.'),
 '1da59662fc078356':([[44,43,999,729]],'Preserved specimen, anatomically visible Spider; unusual domain retained explicitly, not a living/PICO example.'),
 '8aea1f1d12a7dc4f':([[160,148,850,852]],'Black spider on light background; include all long leg tips.'),
 '69fd61f54406189c':([[468,104,823,726]],'Dark spider on blue surface; include thin far-right and bottom tips, exclude shadows.'),
 '50f231b09e848f15':([[470,85,977,549]],'Spider in web; upper plant fragment/silk is not an extra leg; visible black leg bends start within box.'),
 '462d03bfed6e874e':([], 'QUARANTINE: severe defocus and simultaneous top/left/bottom truncation make leg extent unreliable for this initial candidate; not negative.'),
 '1e868895fdbc938d':([[243,10,946,724]],'Wall spider; include all stretched legs, exclude shadows.'),
 '3ec20e93d8182fcb':([[404,158,619,537]],'Small orb spider; extend right edge for curved leg tip, web excluded.'),
 '41eec28d727a251d':([[403,0,634,634]],'Upper leg clipped at y=0; include left short leg and bottom tips, exclude long cast shadows.'),
}
NEGATIVES=['1a4ce99db1425ee1','1d5bb9d9a8a49e84','3f65d9a5db068b25','2ca2d110708d96b5','2e8edb16c6a09beb','28b565f93c252cc0']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
    assert not REVIEW.exists() and not SPLIT.exists(),'Never overwrite a frozen review/split'
    OUT.mkdir(exist_ok=True)
    source=ROOT/'.codex-tmp/yolo-training/openimages-review/candidate-p20.json'
    gap=ROOT/'TrainingData/public-gap-annotations-v1.json';audit=ROOT/'.codex-tmp/yolo-training/candidate-group-audit.json'
    metadata={r['image_id']:r for r in load(source)['records']};group_rows={r['key']:r for r in load(audit)['records']}
    for receipt in load(audit)['source_manifests']:
        assert sha(ROOT/receipt['path'])==receipt['sha256']
    rows=[]
    for key in list(DECISIONS)+NEGATIVES:
        r=metadata[key];p=ROOT/r['local_path'];assert sha(p)==r['sha256']
        m=r['official_metadata'];assert m['License']=='https://creativecommons.org/licenses/by/2.0/' and m['AuthorProfileURL'] and m['Author']
        boxes,note=DECISIONS.get(key,([], 'Full original reviewed: no visible Spider or Cockroach. Background only for this TWO-CLASS experiment; other project objects remain present.'))
        w,h=r['size']
        for x0,y0,x1,y1 in boxes:assert 0<=x0<x1<=w and 0<=y0<y1<=h
        rows.append({'key':'oi-'+key,'source_path':Path(r['local_path']).as_posix().replace('\\','/'),'sha256':r['sha256'],'pixel_sha256':r['pixel_sha256'],'size':r['size'],
            'review':'INDIVIDUAL_FULL_ORIGINAL_REVIEW_20261116','note':note,'status':'QUARANTINED_NOT_NEGATIVE' if note.startswith('QUARANTINE:') else 'APPROVED_FOR_TWO_CLASS_CANDIDATE_ONLY',
            'boxes':[{'class':'Spider','xyxy_pixels':b,'xyxy_normalized':[v/z for v,z in zip(b,[w,h,w,h])]} for b in boxes],
            'source_boxes':r['annotations'],'source_page':m['OriginalLandingURL'],'download_url':r['download_url'],'author':m['Author'],'license':m['License'],
            'provenance_groups':['flickr-author-'+m['AuthorProfileURL'], 'audit-component-'+group_rows['oi-'+key]['component_id']],
            'background_only_for':['Cockroach','Spider'] if key in NEGATIVES else []})
    for r in load(gap)['records']:
        if r['canonical_candidate']!='Cockroach' or not r.get('eligible_for_candidate_training'):continue
        assert sha(ROOT/r['local_path'])==r['sha256']
        assert r['status']=='BOX_REVIEWED_CANDIDATE_ONLY' and len(r['boxes'])==1
        rows.append({'key':r['source_key'],'source_path':r['local_path'],'sha256':r['sha256'],'size':r['size'],'boxes':r['boxes'],
            'review':'INHERITED_SHA_BOUND_INDIVIDUAL_REVIEW_V1','review_evidence':r['review_evidence'],'source_page':r['source_page'],'download_url':r['image_url'],
            'author':r['attribution'],'license':r['photo_license_code'],'status':'APPROVED_FOR_TWO_CLASS_CANDIDATE_ONLY',
            'provenance_groups':['inat-observer-'+str(r['observer_id']),'audit-component-'+group_rows[r['source_key']]['component_id']]})
    review={'version':1,'purpose':'TWO_CLASS_CANDIDATE_LABEL_REVIEW_NOT_DEPLOYMENT_ACCEPTANCE','names':['Cockroach','Spider'],'review_date':'2026-11-16','records':rows,
        'source_receipts':[{'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p)} for p in [source,gap,audit]],
        'box_policy':'Visible body and legs; exclude web/shadows; no amodal inference. Quarantines are excluded, never negative labels.',
        'scope':'Original images retained untouched. Six backgrounds contain other HCI classes and are negatives ONLY for these two bug classes. Public-image pretraining overlap unknown. Not 13-class acceptance.'}
    REVIEW.write_text(json.dumps(review,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    eligible=[r for r in rows if r['status']=='APPROVED_FOR_TWO_CLASS_CANDIDATE_ONLY'];parent={r['key']:r['key'] for r in eligible};owners={}
    def find(k):
        while parent[k]!=k:parent[k]=parent[parent[k]];k=parent[k]
        return k
    for r in eligible:
        for group in r['provenance_groups']:
            if group in owners:
                a,b=find(r['key']),find(owners[group]);parent[max(a,b)]=min(a,b)
            else:owners[group]=r['key']
    components=defaultdict(list)
    for r in eligible:components[find(r['key'])].append(r)
    assigned={}
    for category,held in [('Cockroach',2),('Spider',3),('background',2)]:
        groups=[k for k,rs in components.items() if all((x['boxes'][0]['class'] if x['boxes'] else 'background')==category for x in rs)]
        groups.sort(key=lambda k:hashlib.sha256(('42:'+k).encode()).hexdigest());assert len(groups)>=held*2+2
        for i,key in enumerate(groups):assigned[key]='test' if i<held else 'valid' if i<held*2 else 'train'
    assert len(assigned)==len(components),'Cross-category provenance component requires manual resolution'
    frozen=[]
    for key,rs in components.items():
        for r in rs:
            frozen.append({**r,'component_id':key,'split':assigned[key]})
    counts={s:dict(Counter(r['boxes'][0]['class'] if r['boxes'] else 'background' for r in frozen if r['split']==s)) for s in ['train','valid','test']}
    split={'version':1,'purpose':'BUGS_BOOTSTRAP_CANDIDATE_NOT_PROJECT_OR_PICO_ACCEPTANCE','names':['Cockroach','Spider'],'seed':42,'review_sha256':sha(REVIEW),
        'test_frozen_before_training':True,'accepted_for_deployment':False,'split_counts':counts,'records':frozen,
        'group_policy':'Union existing global exact/perceptual audit components with Flickr author or iNaturalist observer across dates; no group shared by fine-tuning splits.',
        'test_policy':'One evaluation after best checkpoint chosen on validation; no test-based threshold/epoch tuning. These public images may overlap pretrained data; fine-tuning holdout only.',
        'limitations':['Small public-photo bootstrap, not independent PICO validation or sufficient deployment coverage','Cockroach photos were already used in a separate earlier failed candidate experiment; not a fresh project-wide test benchmark','No training/deployment acceptance for other eleven target classes','No device image sequences, real-world tracking stability or HCI gesture measurements'],
        'preset_experiment':{'initial_weight':'yolo26n.pt','epochs':20,'imgsz':416,'batch':1,'workers':0,'cache':False,'lr0':.001,'mosaic':0,'seed':42,'diagnostic_confidence':.25,'diagnostic_iou':.5,'deployment_accepted':False}}
    SPLIT.write_text(json.dumps(split,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'reviewedSpiderOriginals':len(DECISIONS),'quarantined':2,'eligibleImages':len(frozen),'split_counts':counts,'reviewSHA256':sha(REVIEW),'splitSHA256':sha(SPLIT)}))
if __name__=='__main__':main()
