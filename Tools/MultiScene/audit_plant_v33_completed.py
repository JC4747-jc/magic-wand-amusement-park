"""Audit v33 single data-intervention run without altering frozen paired-trial auditor."""
import json,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent/'autoresearch'))
from audit_completed import audit_trial,read,require
from prepare import ROOT,sha,verify
out=ROOT/'Reports/plant-v33-autoresearch'
c=read(out/'contract.json');verify(c)
p=read(out/'plan.json');l=read(out/'ledger.json')
require(p['epochs']==15 and len(p['trials'])==len(l['trials'])==1,'Single15epoch budget')
t=p['trials'][0];r=l['trials'][0];folder=out/t['name'];run=read(folder/'run.json')
require(t['overrides']==r['overrides']=={'scale':.1},'Frozen scale override')
require(read(folder/'state.json')==r,'State/ledger mismatch')
require(run['contract_sha256']==sha(out/'contract.json'),'Contract mismatch')
require(run['dataset_version']==sha(ROOT/c['dataset']/'audit.json'),'Dataset mismatch')
prior=read(ROOT/'Reports/plant-v30-autoresearch/reduced_scale/run.json')
require(run['initializer_sha256']==prior['initializer_sha256'],'Initializer mismatch')
require({k for k in run['parameters'] if run['parameters'][k]!=prior['parameters'][k]}=={'data','project'},'Unexpected option change')
require(set(run['parameters'])==set(prior['parameters']),'Parameter keys changed')
result=audit_trial(folder,r,15,run['dataset_version'])
require(l['best_development_candidate']['sha256']==result['weight_sha256'],'Winner mismatch')
require(not l['fresh_test_performed'] and not l['deployment_accepted'],'Invalid acceptance')
print(json.dumps(dict(status='COMPLETED_EVIDENCE_VERIFIED',frozen_inputs=len(c['files']),trials=[result],auditor_limitation_resolved='Historical paired-trial auditor requires first override empty; v33 prospectively uses one scale.1 trial. Same audit_trial verifies all15 epoch reports/ranks/CSV/weights. Frozen historical auditor unchanged.',fresh_test_performed=False,deployment_accepted=False),indent=2))
