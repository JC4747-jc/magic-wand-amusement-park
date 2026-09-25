"""Run existing audit against the freeze v3 export, plus v2 overlap checks."""
import json
import audit_lighter_v2 as audit

audit.ZIP = audit.ROOT / 'detect lighters.v3i.yolo26.zip'
audit.OUT = audit.ROOT / 'audit_lighter_v3'
audit.ALLOW_POLYGONS = True

if __name__ == '__main__':
    audit.main()
    path = audit.OUT / 'audit.json'
    report = json.loads(path.read_text(encoding='utf-8'))
    previous = json.loads((audit.ROOT/'audit_lighter_v2/audit.json').read_text(encoding='utf-8'))['records']
    report['v2_exact_overlap'] = [{'new':r['name'],'v2':s['name']} for r in report['records'] for s in previous if r['digest']==s['digest']]
    report['v2_near_overlap'] = [{'new':r['name'],'v2':s['name'],'distance':(r['dhash']^s['dhash']).bit_count()} for r in report['records'] for s in previous if r['digest']!=s['digest'] and (r['dhash']^s['dhash']).bit_count() <= 4]
    report['edge_violations'] = [{'name':r['name'],'box':b} for r in report['records'] for b in r['boxes'] if min(b[1]-b[3]/2,b[2]-b[4]/2)<-0.002 or max(b[1]+b[3]/2,b[2]+b[4]/2)>1.002]
    path.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print('v2 exact overlap',len(report['v2_exact_overlap']),'v2 similar candidates',len(report['v2_near_overlap']),'edge violations',len(report['edge_violations']))
