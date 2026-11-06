"""Fix actual YOLO label discovery; preserve image/label bytes and all folds."""
import hashlib
import json
import shutil
from pathlib import Path
from dataset_paths import manifest_relative_path

ROOT=Path(__file__).resolve().parents[2]


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
 parent=ROOT/'Datasets/plant-hanging-v50'
 out=ROOT/'Datasets/plant-label-paths-v52'
 assert not out.exists()
 assert sha(parent/'audit.json')=='f7035956f669e6f73a95349dfaff0bfe695378e90b2a88bdf6cde8836a94671a'
 audit=json.loads((parent/'audit.json').read_text())
 fixes=[]
 for row in audit['samples']:
  image=manifest_relative_path(row['image'])
  oldlabel=manifest_relative_path(row['label'])
  expected=Path('labels')/Path(*image.parts[1:]).with_suffix('.txt')
  assert sha(parent/image)==row['sha256'] and sha(parent/oldlabel)==row['label_sha256']
  for src,dst in [(parent/image,out/image),(parent/oldlabel,out/expected)]:
   dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst);assert sha(src)==sha(dst)
  if expected.as_posix()!=oldlabel.as_posix():
   assert row['split']=='train' and not (parent/expected).exists()
   fixes.append(dict(image=image.as_posix(),old_label=oldlabel.as_posix(),new_label=expected.as_posix(),
                     label_sha256=row['label_sha256'],instances=len((parent/oldlabel).read_text().splitlines())))
  row['label']=expected.as_posix()
 assert len(fixes)==2 and sum(x['instances'] for x in fixes)==7
 # Verify the actual installed training loader's filename conversion, not a parallel assumption.
 from ultralytics.data.utils import img2label_paths
 for row in audit['samples']:
  inferred=Path(img2label_paths([str(out/manifest_relative_path(row['image']))])[0])
  assert inferred.resolve()==(out/manifest_relative_path(row['label'])).resolve()
  assert sha(inferred)==row['label_sha256']
 audit.update(parent_audit_sha256=sha(parent/'audit.json'),training_approved=False,
              scope='v50 payload bytes/folds retained, two TRAIN label filenames canonicalized for actual YOLO discovery; no semantic relabeling.')
 audit['label_path_fixes']=fixes
 (out/'audit.json').write_text(json.dumps(audit,indent=2),encoding='utf-8')
 config=json.loads((parent/'data.yaml').read_text());config['path']=str(out)
 (out/'data.yaml').write_text(json.dumps(config,indent=2),encoding='utf-8')
 hold=json.loads((parent/'review_hold.json').read_text());hold['dataset_audit_sha256']=sha(out/'audit.json')
 (out/'review_hold.json').write_text(json.dumps(hold,indent=2),encoding='utf-8')
 shutil.copy2(parent/'legacy-native-val.txt',out/'legacy-native-val.txt')
 result=dict(status='PREPARED_PATH_FIX_OPEN_ANNOTATION_HOLD',fixes=fixes,images=161,
             all_image_and_label_bytes_unchanged=True,actual_yolo_path_checks=161,
             dataset_sha256=sha(out/'audit.json'),deployment_accepted=False,
             historical_training_caveat='Previous manifest evaluator saw seven labels that YOLO training missed because filenames differed. Historical metrics remain recorded, not evidence that these targets were trained.')
 (ROOT/'Reports/plant-label-paths-v52-integrity.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
 print(json.dumps(result))


if __name__=='__main__':main()
