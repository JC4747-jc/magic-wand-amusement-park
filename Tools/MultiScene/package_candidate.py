"""Package an actual trained candidate without modifying runtime configuration."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

parser = argparse.ArgumentParser()
parser.add_argument('--run', required=True)
parser.add_argument('--data', required=True)
parser.add_argument('--validation', required=True)
parser.add_argument('--out', required=True)
parser.add_argument('--status', choices=['REJECTED_VALIDATION', 'REJECTED_INDEPENDENT_TEST', 'REJECTED_OBSERVED_REGRESSION', 'REJECTED_NEGATIVE_REGRESSION', 'LOCAL_TEST_PENDING',
    'LOCAL_TEST_PASSED_PICO_PENDING', 'NOT_SELECTED_NO_VALIDATION_IMPROVEMENT'], required=True)
parser.add_argument('--test')
parser.add_argument('--test-data', help='Explicit separately held-out test dataset directory; defaults to training dataset')
parser.add_argument('--negative-test', help='Separate negative-only regression report for the same weight')
parser.add_argument('--allowed-classes', nargs='+', help='Explicit scene allowlist for a checkpoint retaining unrelated native classes')
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
fire = root/'object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt'
if sha(fire) != 'fd4a31001feb66127a8c527ffb1e0ba32bfe9f72566cfde4f2f9b5fc0c3e7ca7':
    raise ValueError('Original lighter baseline SHA changed; inspect before packaging')
run, data, out = Path(args.run), Path(args.data), Path(args.out)
if out.exists():
    raise ValueError('Immutable candidate output already exists')
params = json.loads((run/'run.json').read_text(encoding='utf-8'))
report = json.loads(Path(args.validation).read_text(encoding='utf-8'))
best = run/'train/weights/best.pt'
version = sha(data/'audit.json')
hold_path = data/'review_hold.json'
review_hold = json.loads(hold_path.read_text(encoding='utf-8-sig')) if hold_path.exists() else None
if review_hold and review_hold.get('dataset_audit_sha256') != version:
    raise ValueError('Dataset review hold version mismatch')
if review_hold and review_hold.get('status') == 'OPEN' and args.status == 'LOCAL_TEST_PASSED_PICO_PENDING':
    raise ValueError('Open annotation review hold prevents marking a candidate locally passed')
recorded_versions = [params[key] for key in ('dataset_version', 'dataset_sha256') if key in params]
if not recorded_versions or any(value != version for value in recorded_versions) or report['dataset_version'] != version:
    raise ValueError('Training and validation dataset versions differ')
if report['sha256'] != sha(best) or report['split'] != 'val':
    raise ValueError('Validation is not from this best.pt and validation split')
test = None
if args.test:
    test = json.loads(Path(args.test).read_text(encoding='utf-8'))
    test_data = Path(args.test_data) if args.test_data else data
    if test['sha256'] != sha(best) or test['dataset_version'] != sha(test_data/'audit.json') or test['split'] != 'test':
        raise ValueError('Test report model or dataset mismatch')
if args.status == 'LOCAL_TEST_PASSED_PICO_PENDING' and test is None:
    raise ValueError('Cannot mark locally tested without actual test report')
if args.status == 'REJECTED_INDEPENDENT_TEST' and test is None:
    raise ValueError('Independent test rejection requires the matching actual test report')
if args.status == 'REJECTED_OBSERVED_REGRESSION' and test is None:
    raise ValueError('Observed regression rejection requires the matching actual report')
negative_test = None
if args.negative_test:
    negative_test = json.loads(Path(args.negative_test).read_text(encoding='utf-8'))
    if negative_test['sha256'] != sha(best) or negative_test['ground_truth_instances'] != 0 or not negative_test['images']:
        raise ValueError('Negative regression must contain actual negative images evaluated with this weight')
if args.status == 'REJECTED_NEGATIVE_REGRESSION' and (negative_test is None or not negative_test['false_positive_images']):
    raise ValueError('Negative regression rejection requires actual false-positive evidence')
import torch
import ultralytics
from ultralytics import YOLO
model = YOLO(best)
route = {'statue':'ParkStatue','tree':'ParkTree','spider':'ParkBugs','jumpingspider':'ParkBugs',
         'cockroach':'ParkBugs','cat':'ParkPet','dog':'ParkPet',
         'potted plant':'ParkPlant','pottedplant':'ParkPlant',
         'chair':'ParkRoom','couch':'ParkRoom','bed':'ParkRoom','dining table':'ParkRoom'}
names = model.names
active = set(args.allowed_classes) if args.allowed_classes else set(names.values())
if not active.issubset(set(names.values())):
    raise ValueError('Scene allowlist contains a class absent from checkpoint')
if any(name.lower() not in route for name in active):
    raise ValueError('Explicit Unity mapping needed for unknown class')
out.mkdir(parents=True)
shutil.copy2(best,out/'best.pt')
shutil.copy2(run/'train/args.yaml',out/'train-args.yaml')
last = run/'train/weights/last.pt'
checkpoint = torch.load(last, map_location='cpu', weights_only=False) if last.exists() else {}
can_resume = checkpoint.get('optimizer') is not None and checkpoint.get('epoch', -1) >= 0
metadata = dict(architecture=str(model.model.yaml.get('yaml_file','YOLO detection')),
    task=model.task, classes=names, allowed_classes=sorted(active),
    unity={i:route[n.lower()] if n in active else None for i,n in names.items()},
    sha256=sha(out/'best.pt'),torch=torch.__version__,ultralytics=ultralytics.__version__,
    dataset=str(data.resolve()),dataset_sha256=version,annotation_review_hold=review_hold,
    data_sources=json.loads((data/'audit.json').read_text(encoding='utf-8'))['sources'],
    parameters=params,validation=report,test=test,status=args.status,
    test_freshness='PREVIOUSLY_OBSERVED_REGRESSION_NOT_FRESH_TEST' if args.status == 'REJECTED_OBSERVED_REGRESSION' else None,
    negative_regression=negative_test,
    service_routed=False,pico_validation='NOT_PERFORMED for this model',
    training_outputs=str((run/'train').resolve()),
    resume_weight=str(last.resolve()) if can_resume else None,
    last_weight=str(last.resolve()) if last.exists() else None,
    optimizer_resume_available=can_resume,
    last_weight_usage='Exact optimizer resume' if can_resume else 'Fine-tuning initializer only; optimizer state stripped or unavailable',
    validation_report=str(Path(args.validation).resolve()),
    test_report=str(Path(args.test).resolve()) if args.test else None,
    test_dataset=str(test_data.resolve()) if args.test else None,
    inference_timing_scope=report['timing_scope'],
    warm_mean_inference_ms=report['warm_mean_ms'],
    limitations=['Candidate packaging does not authorize deployment or prove PICO performance'])
dataset_audit = json.loads((data/'audit.json').read_text(encoding='utf-8'))
if dataset_audit.get('scope'):
    metadata['dataset_scope'] = dataset_audit['scope']
if 'JumpingSpider' in active:
    metadata['class_scope'] = {'JumpingSpider': 'Salticidae family category; not a single species or all spiders'}
    metadata['service_aliases'] = {'jumpingspider': 'Spider'}
    metadata['limitations'].append('Spider service alias is for existing Bugs scene compatibility; native category remains JumpingSpider')
(out/'model.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
print(json.dumps(dict(path=str((out/'best.pt').resolve()),sha256=metadata['sha256'],
                     status=args.status,service_routed=False)))
