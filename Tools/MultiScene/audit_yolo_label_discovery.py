"""Check manifest/actual YOLO label-discovery agreement, including empty negatives."""
import argparse
import hashlib
import json
from pathlib import Path
from ultralytics.data.utils import img2label_paths
from dataset_paths import manifest_relative_path


def audit(root):
 root=Path(root).resolve()
 manifest=root/'audit.json'
 data=json.loads(manifest.read_text(encoding='utf-8'))
 errors=[]
 for row in data['samples']:
  image=root/manifest_relative_path(row['image'])
  label=root/manifest_relative_path(row['label'])
  expected=Path(img2label_paths([str(image)])[0])
  reasons=[]
  if expected.resolve()!=label.resolve():reasons.append('manifest_path_differs_from_yolo_discovery')
  if not expected.is_file():reasons.append('yolo_label_missing')
  elif hashlib.sha256(expected.read_bytes()).hexdigest()!=row['label_sha256']:reasons.append('yolo_label_payload_differs_from_manifest')
  if reasons:errors.append(dict(image=row['image'],recorded_label=row['label'],yolo_expected=str(expected.relative_to(root)),split=row['split'],reasons=reasons))
 return dict(status='FAIL' if errors else 'PASS',dataset_audit_sha256=hashlib.sha256(manifest.read_bytes()).hexdigest(),
             samples_checked=len(data['samples']),errors=errors,
             scope='Actual installed Ultralytics img2label_paths and payload SHA; not semantic annotation approval.')


if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--data',required=True);parser.add_argument('--out',required=True)
 args=parser.parse_args();out=Path(args.out)
 if out.exists():raise FileExistsError('Preserve existing audit')
 result=audit(args.data);out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(result,indent=2),encoding='utf-8')
 print(json.dumps(result));raise SystemExit(1 if result['errors'] else 0)
