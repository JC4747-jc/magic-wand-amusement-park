"""Verify exported weight under the same plant-only inference policy as selection."""
import hashlib,json,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'object-detection-demo'))
from scene_protocol import native_class_filter
from runtime_checkpoint_selection_v57 import rank_report
def read(p):return json.loads(p.read_text())
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 folder=ROOT/'Reports/plant-v78-freeze-diagnostic';before=folder/'runtime-selection/epoch-025/report.json';after=folder/'final-runtime-val/report.json';allclass=folder/'final-val/report.json';a,b,c=map(read,[before,after,allclass]);completed=read(folder/'completed.json')
 assert completed['selected_epoch']==25 and a['dataset_version']==b['dataset_version']==c['dataset_version']
 assert b['sha256']==c['sha256']==completed['weight_sha256']==sha(folder/'train/weights/best.pt')
 assert a['allowed_classes']==b['allowed_classes']==['PottedPlant'] and c['allowed_classes']==['CutArrangement','PottedPlant']
 assert a['per_class']==b['per_class'] and a['operating_per_class']==b['operating_per_class']
 policy=read(folder/'runtime-selection/policy.json');rank,cohorts=rank_report(b,policy);assert list(rank)==completed['runtime_rank']
 clean=lambda report:[{k:r[k] for k in ['image','ground_truth','true_positive','false_positive','false_negative']} for r in report['per_image']]
 assert clean(a)==clean(b)
 service_filter=native_class_filter({0:'PottedPlant',1:'CutArrangement'},{'allowed':['PottedPlant'],'aliases':{'pottedplant':'PottedPlant'}});assert service_filter==[0]
 result=dict(status='EXPORTED_RUNTIME_REPORT_EXACTLY_REPRODUCES_SELECTED_CHECKPOINT_METRICS',selected_epoch=25,rank=rank,cohorts=cohorts,runtime_plant_ap=b['per_class'],all_class_auxiliary_diagnostic_ap=c['per_class'],report_scope='final-runtime-val is plant-only routing evidence; final-val retained as all-class auxiliary diagnostic',service_filter_actual_result=service_filter,final_report_sha256=sha(after),selected_report_sha256=sha(before),all_class_report_sha256=sha(allclass),service_policy_sha256=sha(ROOT/'object-detection-demo/scene_protocol.py'),weight_sha256=b['sha256'],runtime_per_image_counts_exact=True,labels_changed=False,checkpoint_reselected=False,test_used=False,deployment_accepted=False,limitation='Class filtering before max_det changes low-score candidates/AP; this does not change the frozen threshold, historical selected checkpoint, or deployment status.')
 out=folder/'runtime-scope-audit.json';assert not out.exists();out.write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
if __name__=='__main__':main()
