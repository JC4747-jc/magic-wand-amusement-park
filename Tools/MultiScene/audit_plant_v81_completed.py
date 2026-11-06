"""Require all epochs, frozen inputs, runtime ranks and exact weight lineage."""
import csv,json,hashlib
from pathlib import Path
import torch
from runtime_checkpoint_selection_v57 import rank_report
ROOT=Path(__file__).resolve().parents[2]
def read(p):return json.loads(p.read_text(encoding='utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 out=ROOT/'Reports/plant-v81-binary-diagnostic';dest=out/'independent-audit.json';assert not dest.exists()
 completed=read(out/'completed.json');plan=read(out/'protocol.json')
 for row in plan['files']:assert sha(ROOT/row['path'])==row['sha256'],row['path']
 selection=read(out/'runtime-selection/selection.json');policy=read(out/'runtime-selection/policy.json')
 records=selection['records'];assert [r['epoch'] for r in records]==list(range(1,31))
 assert [int(r['epoch']) for r in csv.DictReader((out/'train/results.csv').open())]==list(range(1,31))
 best=None;bestepoch=None;bestrecord=None
 for row in records:
  p=out/f"runtime-selection/epoch-{row['epoch']:03d}/report.json";report=read(p)
  assert sha(p)==row['report_sha256'] and report['sha256']==row['checkpoint_sha256']
  rank,cohorts=rank_report(report,policy);assert list(rank)==row['rank'] and cohorts==row['cohorts']
  selected=best is None or rank>best;assert row['selected']==selected
  if selected:best=rank;bestepoch=row['epoch'];bestrecord=row
 raw=out/'runtime-selection/runtime-best.pt';final=out/'train/weights/best.pt'
 assert sha(raw)==selection['best_sha256']==bestrecord['checkpoint_sha256']
 assert completed['selected_epoch']==bestepoch and completed['runtime_rank']==list(best)
 assert sha(final)==completed['weight_sha256']
 report=read(out/'final-val/report.json');assert report['sha256']==sha(final) and sha(out/'final-val/report.json')==completed['report_sha256']
 rawckpt=torch.load(raw,map_location='cpu',weights_only=False);finalckpt=torch.load(final,map_location='cpu',weights_only=False)
 source=rawckpt.get('ema') or rawckpt['model'];target=finalckpt.get('ema') or finalckpt['model']
 for name,tensor in source.state_dict().items():assert torch.equal(tensor.float(),target.state_dict()[name].float()),name
 assert (out/'train/weights/native-fitness-best.pt').is_file()
 runtime=read(out/'freeze-runtime.json');assert [r['epoch'] for r in runtime['records']]==list(range(1,31))
 assert all(r['all_frozen_state_exact'] and r['frozen_parameters']==117 and r['frozen_bn']==39 for r in runtime['records'])
 initializer=ROOT/'Reports/plant-binary-initializer-v80/initializer.pt'
 assert sha(initializer)==runtime['initializer_sha256']==plan['initializer_sha256']
 initialckpt=torch.load(initializer,map_location='cpu',weights_only=False);initialmodel=initialckpt.get('ema') or initialckpt['model']
 initial=initialmodel.state_dict();finalstate=target.state_dict();prefixes=tuple(f'model.{i}.' for i in range(10))
 frozen=[k for k in initial if k.startswith(prefixes)];assert frozen
 def normalized(t):return t.half() if t.is_floating_point() else t
 assert all(torch.equal(normalized(initial[k]),normalized(finalstate[k])) for k in frozen), 'Selected checkpoint frozen backbone differs from initializer'
 changed=[k for k in initial if not k.startswith(prefixes) and not torch.equal(normalized(initial[k]),normalized(finalstate[k]))]
 assert changed, 'No trainable neck/head state changed'

 result=dict(status='ALL30_RUNTIME_SELECTION_EVIDENCE_VERIFIED',selected_epoch=bestepoch,rank=best,
  raw_selected_sha256=sha(raw),stripped_best_sha256=sha(final),frozen_files_verified=len(plan['files']),
  all30_report_hashes_verified=True,all30_rank_and_earliest_tie_decisions_verified=True,
  all30_frozen_runtime_checks_verified=True,frozen_backbone_tensors_exact_after_checkpoint_half_normalization=len(frozen),changed_neck_head_state_tensors=len(changed),
  native_fitness_best_preserved=True,strip_optimizer_model_tensors_exact=True,
  runtime_gate_pass=bool(best[0]),deployment_accepted=False,
  limitation='Even a development gate pass requires annotation clearance, independent TEST and PICO validation.')
 dest.write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
if __name__=='__main__':main()
