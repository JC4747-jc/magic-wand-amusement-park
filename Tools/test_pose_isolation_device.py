"""Bounded A/B capture on a connected, awake PICO. No synthetic rotation.

Requires installed Pose 1.11.1, running YOLO, and both ADB reverses. Uses a real
record-only stereo server solely to exercise image transport. Writes evidence,
not a claim of real-model accuracy or successful physical registration.
"""
import json
from pathlib import Path
import re
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
ADB = 'D:/Unity/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
SERIAL = 'PA921AMGKA310157G'
PACKAGE = 'com.yn.picmagicmr.stereo.pose'
OUT = ROOT/'.codex-tmp/pose-isolation-20261102-pc-readiness'
DETECTOR_LOG = ROOT/'.codex-tmp/pose-isolation-detector.stdout.log'


def adb(*args):
    return subprocess.run([ADB,'-s',SERIAL,*args],capture_output=True,text=True,check=True,timeout=20).stdout


def phase(name, enabled=True, launch=True):
    offset=DETECTOR_LOG.stat().st_size
    with (OUT/(name+'.log')).open('w',encoding='utf-8') as log:
        capture=subprocess.Popen([ADB,'-s',SERIAL,'logcat','-T','1','-v','threadtime','Unity:I','*:S'],stdout=log,stderr=subprocess.STDOUT)
        try:
            if launch:
                adb('shell','am','force-stop',PACKAGE)
                adb('shell','am','start','-W','-n',PACKAGE+'/com.unity3d.player.UnityPlayerActivity',
                    '--ez','pose_enabled',str(enabled).lower(),'--ez','pose_diagnostic','true')
            print('PHASE '+name+' capturing 35 seconds',flush=True)
            time.sleep(35)
        finally:
            capture.terminate(); capture.wait(timeout=5)
    with DETECTOR_LOG.open('rb') as f:
        f.seek(offset); metrics=f.read().decode('utf-8',errors='replace')
    (OUT/(name+'-detector.log')).write_text(metrics,encoding='utf-8')
    fps=[float(x) for x in re.findall(r'(?<!_)fps=([\d.]+)',metrics)][2:]
    headset=(OUT/(name+'.log')).read_text(encoding='utf-8',errors='replace')
    result=dict(phase=name,metric_samples=len(fps),mean_fps=sum(fps)/len(fps) if fps else None,
                timeout_events=headset.count('state=YOLO_TIMEOUT'),
                pose_failures=headset.count('[PoseLink] failure='),
                pose_ready='[PoseLink] ready' in headset,
                tracking_lost=headset.count('state=TRACKING_LOST'))
    print(json.dumps(result),flush=True)
    return result


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    adb('reverse','tcp:5005','tcp:5005'); adb('reverse','tcp:5006','tcp:5006')
    adb('shell','am','force-stop','com.yn.picmagicmr.stereo.repair')
    results=[]; server=None
    try:
        results.append(phase('pose-disabled',False))
        results.append(phase('server-absent'))
        with (OUT/'sidecar.log').open('w',encoding='utf-8') as log:
            server=subprocess.Popen([sys.executable,'-u',str(ROOT/'object-detection-demo/pose_bridge.py'),'--record-only'],stdout=log,stderr=subprocess.STDOUT,cwd=ROOT)
            results.append(phase('image-transport'))
            server.terminate(); server.wait(timeout=5); server=None
            results.append(phase('server-killed',launch=False))
            server=subprocess.Popen([sys.executable,'-u',str(ROOT/'object-detection-demo/pose_bridge.py'),'--record-only'],stdout=log,stderr=subprocess.STDOUT,cwd=ROOT)
            results.append(phase('server-recovered',launch=False))
    finally:
        if server is not None: server.terminate(); server.wait(timeout=5)
        (OUT/'summary.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
        # Clear lab-only intent flags; leave the new app in ordinary mode.
        adb('shell','am','force-stop',PACKAGE)
        adb('shell','am','start','-W','-n',PACKAGE+'/com.unity3d.player.UnityPlayerActivity')


if __name__=='__main__': main()
