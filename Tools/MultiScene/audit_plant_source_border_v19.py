"""Flag saturated red edge runs for source-quality review, never modify labels/images."""
import json,hashlib
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
def main():
 data=ROOT/'Datasets/plant-rooted-v18';audit=data/'audit.json';a=json.loads(audit.read_text());rows=[]
 for r in a['samples']:
  with Image.open(data/r['image']) as im:
   im=im.convert('RGB');w,h=im.size;px=im.load();scores={}
   for name,coords in [('top',[(x,min(1,h-1)) for x in range(w)]),('bottom',[(x,max(0,h-2)) for x in range(w)]),('left',[(min(1,w-1),y) for y in range(h)]),('right',[(max(0,w-2),y) for y in range(h)])]:
    values=[px[x,y] for x,y in coords];scores[name]=sum(red>170 and red>green*1.6 and red>blue*1.6 for red,green,blue in values)/len(values)
  if sum(v>=.8 for v in scores.values())>=2:rows.append(dict(original_id=r['original_id'],split=r['split'],image=r['image'],sha256=r['sha256'],red_edge_fractions=scores,status='VISUAL_SOURCE_REVIEW_REQUIRED'))
 report=dict(dataset_sha256=hashlib.sha256(audit.read_bytes()).hexdigest(),scanned_images=len(a['samples']),method='At least two image edges have >=80% saturated red pixels; heuristic flag only, not proof of annotation overlay or rejection',candidates=rows,labels_modified=False,images_modified=False,training_approved=False)
 (ROOT/'Reports/plant-train-instance-audit-v19/red-border-scan.json').write_text(json.dumps(report,indent=2));print(json.dumps(dict(scanned=report['scanned_images'],candidates=rows)))
if __name__=='__main__':main()
