"""Verify immutable reviewed plant staging batches without releasing training."""
import json,hashlib,collections
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def main():
 batches=['plant-broader-v14-staging','plant-broader-v14-batch2-staging','plant-broader-v14-multi-instance-staging','plant-broader-v14-waiting-room-staging'];seen=set();counts=collections.Counter();records=[]
 for batch in batches:
  folder=ROOT/'Datasets'/batch;manifest=folder/'manifest.json';data=json.loads(manifest.read_text());assert data['names']==['PottedPlant','CutArrangement']
  for r in data['samples']:
   assert r['original_id'] not in seen;seen.add(r['original_id']);assert r['reserved_split']=='train' and r['review_approved']
   for key,digest in [('image','sha256'),('label','label_sha256')]:assert hashlib.sha256((folder/r[key]).read_bytes()).hexdigest()==r[digest]
   assert r['attribution']['License'].rstrip('/')=='https://creativecommons.org/licenses/by/2.0'
   instance_count=0
   for line in (folder/r['label']).read_text().splitlines():
    c,x,y,w,h=map(float,line.split());assert c in (0,1) and all(0<=v<=1 for v in (x,y,w,h)) and w>0 and h>0;assert x-w/2>=-1e-7 and x+w/2<=1+1e-7 and y-h/2>=-1e-7 and y+h/2<=1+1e-7;counts[data['names'][int(c)]]+=1;instance_count+=1
   records.append(dict(original_id=r['original_id'],batch=batch,instances=instance_count,group=r['group']))
 out=dict(status='PASS_STAGED_BYTES_LABELS_AND_ATTRIBUTION',images=len(seen),instances=dict(counts),records=records,training_released=False,limitations=['Staging integrity only; each source/pixel clearance is separately recorded.','No model accuracy, independent TEST or PICO validation implied.']);target=ROOT/'Reports/plant-broader-v14-staging-integrity.json';assert not target.exists();target.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps({k:v for k,v in out.items() if k!='records'}))
if __name__=='__main__':main()
