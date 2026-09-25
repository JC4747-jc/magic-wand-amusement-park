"""Clean assgn2, preserve v5 holdouts, fine-tune and compare v5/v6."""
import json
import random
import shutil
import zipfile
from collections import Counter, defaultdict
from pathlib import Path
import train_lighter_v5 as workflow

ROOT=workflow.DATA_ROOT
OLD=ROOT/'detect_merged_v5_freeze'
OUT=ROOT/'detect_merged_v6_assgn2'
AUDIT=ROOT/'audit_assgn2'
RUN=workflow.RUNS/'lighter-yolo26n-assgn2-v6-50e'
workflow.OUT=OUT
workflow.BASE=workflow.RUN/'weights/best.pt'
workflow.RUN=RUN
workflow.BASE_LABEL,workflow.NEW_LABEL='v5','v6'
workflow.EVAL_SUBSETS=('old','freeze','assgn2')


def prepare():
    if OUT.exists(): raise SystemExit(f'Refusing overwrite: {OUT}')
    report=json.loads((AUDIT/'audit.json').read_text())
    rows=report['records']
    # Full 100-image contact-sheet review: missing objects or ambiguous extent/type.
    issues={16:'multiple lighters omitted',18:'rightmost lighter omitted',40:'possible second lighter omitted',50:'possible black lighter omitted',57:'outdoor lighter types incompletely annotated',58:'silver lighter omitted',68:'black object type/extent unclear',96:'extent of handheld object questionable'}
    excluded={rows[i]['name']:reason for i,reason in issues.items()}
    for pair in report['near_old_pairs']:
        excluded.setdefault(pair['new'],'conservative quarantine: perceptual match to old data, not confirmed duplicate')
    for group in report['exact_duplicate_groups']:
        for n in group[1:]: excluded.setdefault(n,'pixel-identical duplicate')
    candidates=[r for r in rows if r['name'] not in excluded]
    parent={r['name']:r['name'] for r in candidates}
    def find(n):
        while parent[n]!=n:
            parent[n]=parent[parent[n]]; n=parent[n]
        return n
    for p in report['near_pairs']:
        if p['a'] in parent and p['b'] in parent: parent[find(p['a'])]=find(p['b'])
    groups=defaultdict(list)
    for r in candidates: groups[find(r['name'])].append(r)
    groups=list(groups.values()); random.Random(42).shuffle(groups); groups.sort(key=len,reverse=True)
    targets={'train':len(candidates)*.7,'valid':len(candidates)*.15,'test':len(candidates)*.15}
    splits=defaultdict(list)
    for g in groups:
        s=max(targets,key=lambda x:targets[x]-len(splits[x])); splits[s].extend(g)
    manifest=[]
    for s in targets:
        for kind in ('images','labels'):
            (OUT/s/kind).mkdir(parents=True,exist_ok=True)
            for p in (OLD/s/kind).iterdir():
                if p.is_file(): shutil.copy2(p,OUT/s/kind/p.name)
    quarantine=OUT/'excluded_reference'
    with zipfile.ZipFile(ROOT/'assgn 2.v1i.yolo26.zip') as z:
        for s,records in splits.items():
            for r in records:
                name='assgn2_'+Path(r['name']).name
                (OUT/s/'images'/name).write_bytes(z.read(r['name']))
                (OUT/s/'labels'/(Path(name).stem+'.txt')).write_text(''.join('0 '+' '.join(f'{v:.9f}' for v in b[1:])+'\n' for b in r['boxes']),encoding='utf-8')
                manifest.append(dict(source='assgn2',original=r['name'],split=s,image=name,group=find(r['name'])))
        for n,reason in excluded.items():
            p=quarantine/n; p.parent.mkdir(parents=True,exist_ok=True); p.write_bytes(z.read(n))
            label=n.replace('/images/','/labels/').rsplit('.',1)[0]+'.txt'
            p=quarantine/label; p.parent.mkdir(parents=True,exist_ok=True); p.write_bytes(z.read(label))
        for n in ('README.dataset.txt','README.roboflow.txt','data.yaml'):
            (OUT/('source_assgn2_'+n)).write_bytes(z.read(n))
    config=f'path: {OUT.as_posix()}\ntrain: train/images\nval: valid/images\ntest: test/images\nnc: 1\nnames: [lighter]\n'
    (OUT/'data.yaml').write_text(config,encoding='utf-8')
    for subset in ('old','freeze'):
        paths=[OUT/'test/images'/Path(line).name for line in (OLD/f'{subset}_test.txt').read_text().splitlines() if line]
        (OUT/f'{subset}_test.txt').write_text('\n'.join(map(str,paths))+'\n',encoding='utf-8')
    (OUT/'assgn2_test.txt').write_text('\n'.join(str(OUT/'test/images'/m['image']) for m in manifest if m['split']=='test')+'\n',encoding='utf-8')
    for subset in workflow.EVAL_SUBSETS:
        (OUT/f'{subset}_eval.yaml').write_text(config.replace('test: test/images',f'test: {subset}_test.txt'),encoding='utf-8')
    assignments={m['original']:m['split'] for m in manifest}
    assert all(assignments[p['a']]==assignments[p['b']] for p in report['near_pairs'] if p['a'] in assignments and p['b'] in assignments)
    summary={'accepted':len(candidates),'excluded':excluded,'new_counts':dict(Counter(m['split'] for m in manifest)),'total_counts':{s:len(list((OUT/s/'images').iterdir())) for s in targets},'manifest':manifest}
    (OUT/'preparation.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in summary.items() if k not in ('excluded','manifest')}))


if __name__=='__main__':
    import sys
    if sys.argv[1]=='prepare': prepare()
    elif sys.argv[1]=='train': workflow.train()
    elif sys.argv[1]=='evaluate': workflow.evaluate()
