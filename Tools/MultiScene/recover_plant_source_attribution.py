"""Recover per-image attribution by exact source bytes; never mutate dataset audits."""
import json,hashlib
from pathlib import Path
from audit_plant_cutflower_v11_candidates import norm
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 data=ROOT/'Datasets/plant-broader-v14';audit=data/'audit.json';a=json.loads(audit.read_text());index={};errors=[]
 for p in sorted((ROOT/'Datasets').glob('*/manifest.json')):
  try:m=json.loads(p.read_text(encoding='utf-8-sig'))
  except Exception as e:errors.append(dict(path=str(p.relative_to(ROOT)),error=str(e)));continue
  for r in m.get('samples',[]):
   if r.get('attribution') and r.get('original_id'):index.setdefault(r['original_id'],[]).append((p,r))
 rows=[]
 for r in a['samples']:
  image=data/r['image'];assert sha(image)==r['sha256'];matches=[]
  for p,s in index.get(r['original_id'],[]):
   ip=p.parent/s['image']
   if not ip.is_file() or sha(ip)!=r['sha256'] or norm(s.get('group',''))!=norm(r.get('group','')):continue
   matches.append(dict(manifest=str(p.relative_to(ROOT)),manifest_sha256=sha(p),attribution=s['attribution']))
  variants={json.dumps(v['attribution'],sort_keys=True) for v in matches};original=r.get('attribution');resolved=original or (json.loads(next(iter(variants))) if len(variants)==1 else None)
  rows.append(dict(original_id=r['original_id'],split=r['split'],sha256=r['sha256'],original_attribution_present=bool(original),status='ORIGINAL_PRESENT' if original else 'RECOVERED_EXACT_SOURCE' if resolved else 'HOLD_MISSING_OR_CONFLICTING_PROVENANCE',attribution=resolved,source_matches=matches))
 report=dict(scope='Per-image attribution recovery only; existing audit/data hashes and labels unchanged',dataset_audit_sha256=sha(audit),images=len(rows),recovered=sum(r['status']=='RECOVERED_EXACT_SOURCE' for r in rows),unresolved=sum(r['attribution'] is None for r in rows),manifest_errors=errors,samples=rows,training_or_deployment_acceptance=False);out=ROOT/'Reports/plant-broader-v14-attribution-recovery.json';assert not out.exists();out.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({k:v for k,v in report.items() if k!='samples'}))
if __name__=='__main__':main()
