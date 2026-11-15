"""Prepare a SHA-bound small delivery; no training, Git mutation, credentials or push."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import time

os.environ.setdefault('OMP_NUM_THREADS','1')
os.environ.setdefault('OPENBLAS_NUM_THREADS','1')
os.environ.setdefault('OPENCV_FFMPEG_THREADS','1')

ROOT = Path(__file__).resolve().parents[1]
REPORTS = ROOT/'Reports/github-delivery-20261115'
PREVIEWS = ROOT/'Previews/scene-animation-20261115'
CANDIDATE = ROOT/'object-detection-demo/weights/candidates/gap-candidate-20261115-a'
EXPECTED_BEST = 'ba8c4259fafd94d218925f62197898997c3f5c87743d1209466781b4c844baeb'


def sha(path):
    value=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(64*1024),b''):value.update(block)
    return value.hexdigest()


def copy_verified(source,target):
    assert source.is_file() and source.stat().st_size<16*1024**2
    target.parent.mkdir(parents=True,exist_ok=True)
    if target.exists():assert sha(source)==sha(target),'Refusing changed existing delivery: '+str(target)
    else:shutil.copy2(source,target)
    assert sha(source)==sha(target)


def main():
    import cv2
    import numpy as np
    from PIL import Image
    assert shutil.disk_usage(ROOT).free>20*1024**3
    REPORTS.mkdir(parents=True,exist_ok=True);PREVIEWS.mkdir(parents=True,exist_ok=True)
    previews=[];total=0;start=time.monotonic();cv2.setNumThreads(1)
    for group in ('pet-motion','insect-motion','room-motion','bronze-motion'):
        for movie in sorted((ROOT/'.codex-tmp'/group).glob('*.mp4')):
            assert time.monotonic()-start<60 and movie.stat().st_size<8*1024**2
            pngs=sorted(movie.with_suffix('').glob('*.png'));assert 2<=len(pngs)<=144
            fps=30 if group=='room-motion' else 24
            samples={0,len(pngs)//4,len(pngs)//2,len(pngs)-1}
            cap=cv2.VideoCapture(str(movie));count=0;colors=[];frames=[]
            try:
                assert cap.isOpened() and abs(cap.get(cv2.CAP_PROP_FPS)-fps)<.05
                while True:
                    ok,bgr=cap.read()
                    if not ok:break
                    assert count<len(pngs) and bgr.shape==(640,640,3)
                    if count in samples:
                        rgb=cv2.cvtColor(bgr,cv2.COLOR_BGR2RGB)
                        with Image.open(pngs[count]) as image:original=np.asarray(image.convert('RGB'))
                        error=float(np.abs(rgb.astype(np.int16)-original.astype(np.int16)).mean())
                        assert error<8,'Preview color mismatch: '+movie.name
                        colors.append(error);frames.append(rgb)
                    count+=1
            finally:cap.release()
            assert count==len(pngs),'Preview frame count mismatch: '+movie.name
            motion=max(float(np.abs(f.astype(np.int16)-frames[0].astype(np.int16)).mean()) for f in frames[1:])
            assert motion>.03,'Static preview: '+movie.name
            target=PREVIEWS/movie.name;copy_verified(movie,target)
            total+=target.stat().st_size;assert total<16*1024**2
            previews.append({'path':target.relative_to(ROOT).as_posix(),'bytes':target.stat().st_size,'sha256':sha(target),
                'frames':count,'fps':fps,'maximum_sampled_color_error_255':max(colors),'maximum_sampled_motion_delta_255':motion,
                'scope':'Offline Unity preview; not PICO footage or runtime performance evidence'})
    assert len(previews)==9
    source=ROOT/'.codex-tmp/yolo-training/artifacts/gap-candidate-20261115-a'
    assert sha(source/'best.pt')==EXPECTED_BEST
    for name in ('best.pt','report.json','results.csv','args.yaml','runner.py','frozen-dataset.json','windows-reload.json'):
        copy_verified(source/name,CANDIDATE/name)
    for source_name,target_name in (
        ('.codex-tmp/pretrained-cat-baseline-check.json','pretrained-cat-baseline-check.json'),
        ('.codex-tmp/yolo-training/log-guard-validation.json','log-guard-validation.json'),
        ('.codex-tmp/apk-validation.json','apk-validation.json')):
        copy_verified(ROOT/source_name,REPORTS/target_name)
    for variant in ('main','pose','repair','allscenes'):
        source=ROOT/f'.codex-tmp/android-{variant}-bronze-final.log.resources.json'
        data=json.loads(source.read_text(encoding='utf8'));data.pop('samples',None);data.pop('backendArguments',None)
        (REPORTS/f'android-{variant}-resources.json').write_text(json.dumps(data,indent=2)+'\n',encoding='utf8')
    manifest={'purpose':'REVIEWABLE_TRAINED_CANDIDATE_AND_ANIMATION_DELIVERY','target_branch':'restore-4d-lighter',
        'upload_status':'PREPARED_NOT_PUSHED','previews':previews,'preview_total_bytes':total,
        'candidate':{'path':(CANDIDATE/'best.pt').relative_to(ROOT).as_posix(),'bytes':(CANDIDATE/'best.pt').stat().st_size,
            'sha256':EXPECTED_BEST,'classes':['Cockroach','BronzeBeast'],'status':'REJECTED_INDEPENDENT_TEST',
            'deployment_accepted':False,'reason':'Frozen public test at confidence .25 / IoU .5 missed all three instances; tiny public test is not PICO accuracy.'},
        'pretrained_cat_baseline':{'path':'yolov8n.pt','classes':80,'cat_class_id':15,'dog_class_id':16,
            'basic_cat_class_needs_training_from_scratch':False,'pico_domain_acceptance':False},
        'excluded':['duplicate last.pt checkpoint','raw photos/datasets','Unity Library/Temp/Logs','virtual environments',
            'SDK/NDK installations','credentials/signing keys','APK binaries'],
        'sources':['https://developers.openai.com/cookbook/examples/codex/using_goals_in_codex',
            'https://docs.ultralytics.com/datasets/detect/coco/', 'https://docs.ultralytics.com/guides/finetuning-guide/',
            'https://docs.ultralytics.com/modes/train/', 'https://docs.github.com/en/pull-requests/reference/pull-request-merges',
            'https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github']}
    (REPORTS/'delivery-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf8')
    (PREVIEWS/'CREDITS.txt').write_text(
        'Model sources, modifications and licenses: Assets/StreamingAssets/MagicMRAssetCredits.txt and individual LICENSE/source records under Assets/ThirdParty.\n'
        'Cat: kenchoo Bicolor Cat, CC BY 4.0. Dog: Wildfire Games / 0 A.D. Wolfhound, CC BY-SA 3.0. Bronze bull: Tina / Poly Haven, CC0.\n'
        'Tree, plant and room source attribution remains in their source asset folders. Original insect/jellyfish work is described in project reports.\n'
        'Offline Unity previews, not PICO recordings or device performance acceptance.\n',encoding='utf8')
    print(json.dumps({'previews':len(previews),'preview_bytes':total,'candidate_bytes':manifest['candidate']['bytes'],
                      'report':str(REPORTS/'delivery-manifest.json')}))


if __name__=='__main__':main()
