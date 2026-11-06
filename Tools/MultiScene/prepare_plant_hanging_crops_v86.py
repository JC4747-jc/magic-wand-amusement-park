"""Derive lossless TRAIN crops from held TRAIN originals; never promote full frames."""
import copy,hashlib,json
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
SPECS=[('plant-hanging-train-source-v83',200088542,[24,0,960,1280]),('plant-hanging-train-source-v83',60784237,[0,0,760,677]),('plant-hanging-train-source-v70',134655765,[0,0,860,960])]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text())
def main():
 out=ROOT/'Datasets/plant-hanging-crop-source-v86';planpath=ROOT/'Reports/plant-hanging-crops-v86-plan.json';assert not out.exists() and not planpath.exists();planned=[]
 for name,pid,box in SPECS:
  source=ROOT/'Datasets'/name;manifest=source/'manifest.json';reservation=source/'train-reservation.json';r=next(r for r in read(manifest)['samples'] if r['page_id']==pid);assert read(reservation)['reserved_split']=='train';assert read(reservation)['manifest_sha256']==sha(manifest);assert sha(source/r['image'])==r['sha256']
  assert r['license'] in ['CC BY 4.0','CC BY-SA 4.0','CC BY 2.0'];planned.append(dict(source_manifest=manifest.relative_to(ROOT).as_posix(),source_manifest_sha256=sha(manifest),reservation_sha256=sha(reservation),original_image=(source/r['image']).relative_to(ROOT).as_posix(),original_sha256=r['sha256'],page_id=pid,crop_xyxy=box,reserved_split='train',original_full_frame_hold_retained=True))
 planpath.write_text(json.dumps(dict(samples=planned,method='Lossless RGB crop saved PNG; coordinates left,top,right,bottom exclusive. No synthetic content, resizing, model inference, label adoption or full-frame hold clearance.',training_approved=False,deployment_accepted=False),indent=2),encoding='utf-8')
 out.mkdir();(out/'images').mkdir();rows=[]
 for item,(name,pid,box) in zip(planned,SPECS):
  source=ROOT/'Datasets'/name;r=copy.deepcopy(next(r for r in read(source/'manifest.json')['samples'] if r['page_id']==pid));original=ROOT/item['original_image']
  with Image.open(original) as im:
   rgb=im.convert('RGB');w,h=rgb.size;l,t,rr,b=box;assert 0<=l<rr<=w and 0<=t<b<=h;crop=rgb.crop(box);path=out/f'images/{pid}.png';crop.save(path);decoded=hashlib.sha256(f'RGB:{crop.width}:{crop.height}:'.encode()+crop.tobytes()).hexdigest()
  r.update(image=path.relative_to(out).as_posix(),source='commons-derived-crop',sha256=sha(path),decoded_rgb_sha256=decoded,dimensions=[rr-l,b-t],reserved_split='train',annotation_status='UNLABELED_DERIVED_CROP',derivation=item,change_notice=f'Cropped original photograph to pixel rectangle{box}; encoded as PNG. Original full frame remains held, creator/license unchanged.')
  rows.append(r)
 manifest=out/'manifest.json';manifest.write_text(json.dumps(dict(names=['PottedPlant'],samples=rows,project_reserved_split='train',derivation_plan_sha256=sha(planpath),training_approved=False,deployment_accepted=False,scope='Three cropped TRAIN views of held TRAIN originals. Derived views share original source groups; original frames remain held and are not approved or independent samples across folds.'),indent=2),encoding='utf-8')
 (out/'train-reservation.json').write_text(json.dumps(dict(reserved_split='train',all_acquired_including_holds=True,manifest_sha256=sha(manifest),pre_derivation_plan_sha256=sha(planpath),inference_performed=False,training_approved=False,deployment_accepted=False),indent=2),encoding='utf-8');print(json.dumps(dict(status='THREE_LOSSLESS_TRAIN_CROPS_PREPARED_NOT_ANNOTATED',manifest_sha256=sha(manifest))))
if __name__=='__main__':main()
