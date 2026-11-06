"""Independently verify target-only labels and actual installed YOLO loader."""
import hashlib,json
from pathlib import Path
from dataset_paths import manifest_relative_path as rel
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 import ultralytics
 from ultralytics.data.dataset import YOLODataset
 from ultralytics.data.utils import img2label_paths
 data=ROOT/'Datasets/plant-binary-v79';parent=ROOT/'Datasets/plant-houseplant-v75';a=json.loads((data/'audit.json').read_text());b=json.loads((parent/'audit.json').read_text());assert len(a['samples'])==len(b['samples'])==194
 counts={k:0 for k in ['train','val','test']};plant=dict(counts);backgrounds=0
 for row,old in zip(a['samples'],b['samples']):
  for k in ['image','label','sha256','split','group','source']:assert row[k]==old[k]
  assert sha(data/rel(row['image']))==row['sha256']==sha(parent/rel(old['image']))
  assert sha(data/rel(row['label']))==row['label_sha256'] and sha(parent/rel(old['label']))==old['label_sha256']
  expected=[l for l in (parent/rel(old['label'])).read_bytes().splitlines() if l.strip() and l.split()[0]==b'0'];actual=[l for l in (data/rel(row['label'])).read_bytes().splitlines() if l.strip()];assert actual==expected
  assert all(l.split()[0]==b'0' and len(l.split())==5 for l in actual)
  discovered=Path(img2label_paths([str(data/rel(row['image']))])[0]);assert discovered.resolve()==(data/rel(row['label'])).resolve()
  counts[row['split']]+=1;plant[row['split']]+=len(actual);backgrounds+=int(row['split']=='train' and not actual)
 assert counts=={'train':160,'val':30,'test':4} and plant=={'train':147,'val':22,'test':5}
 loader=YOLODataset(img_path=str(data/'images/train'),data={'names':{0:'PottedPlant'},'nc':1},imgsz=640,augment=False,batch_size=4)
 assert len(loader)==160 and sum(len(r['cls']) for r in loader.labels)==147 and all(int(c)==0 for r in loader.labels for c in r['cls'].reshape(-1))
 assert sum(not len(r['cls']) for r in loader.labels)==backgrounds
 report=dict(status='ALL194_IMAGES_FOLDS_TARGET_LINES_AND_ACTUAL_LOADER_VERIFIED',dataset_sha256=sha(data/'audit.json'),images=194,counts=counts,plant_instances=plant,actual_training_images=len(loader),actual_train_backgrounds=backgrounds,all388_payload_sha_verified=True,all_parent_image_bytes_and_target_label_lines_exact=True,all_parent_labels_sha_still_exact=True,ultralytics=ultralytics.__version__,test_inference_performed=False,training_approved=False,deployment_accepted=False,limitation='Integrity audit only; original annotation hold and independent TEST/PICO requirements retained.')
 out=ROOT/'Reports/plant-binary-v79-loader-check.json';assert not out.exists();out.write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
if __name__=='__main__':main()
