"""Bounded real TCP inference on local PICO-named historical JPEGs; no holdout/device claim."""
import os
os.environ['OMP_NUM_THREADS']='1'
os.environ['MKL_NUM_THREADS']='1'
os.environ['YOLO_AUTOINSTALL']='false'
import argparse,hashlib,json,math,socket,struct,subprocess,sys,time
from pathlib import Path
from PIL import Image
import psutil

ROOT=Path(__file__).resolve().parents[1]
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def pixels(p):
 with Image.open(p) as im:
  assert im.width*im.height<=8*1024**2
  rgb=im.convert('RGB');return hashlib.sha256(str(rgb.size).encode()+rgb.tobytes()).hexdigest()
def dhash(p):
 with Image.open(p) as im:
  small=im.convert('L').resize((9,8));values=list(small.get_flattened_data())
  return sum((values[y*9+x]>values[y*9+x+1])<<(y*8+x) for y in range(8) for x in range(8))
def boxes(p,w,h):
 result=[]
 for line in p.read_text().splitlines():
  values=[float(v) for v in line.split()];assert values[0]==0 and all(math.isfinite(v) for v in values)
  coords=values[1:];assert all(0<=v<=1 for v in coords)
  if len(coords)==4:
   x,y,bw,bh=coords;box=[(x-bw/2)*w,(y-bh/2)*h,(x+bw/2)*w,(y+bh/2)*h]
  else:
   assert len(coords)>=6 and len(coords)%2==0
   box=[min(coords[::2])*w,min(coords[1::2])*h,max(coords[::2])*w,max(coords[1::2])*h]
  result.append(box)
 return result
def iou(a,b):
 intersection=max(0,min(a[2],b[2])-max(a[0],b[0]))*max(0,min(a[3],b[3])-max(a[1],b[1]))
 return intersection/max(1e-9,(a[2]-a[0])*(a[3]-a[1])+(b[2]-b[0])*(b[3]-b[1])-intersection)
def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--output',required=True,type=Path);args=parser.parse_args()
 out=args.output.resolve();assert out.is_relative_to((ROOT/'.codex-tmp').resolve()) and not out.exists()
 frames=sorted((ROOT/'Find lighter/test/images').glob('*.jpg'));assert len(frames)==14 and sum(p.stat().st_size for p in frames)<16*1024**2
 weight=ROOT/'object-detection-demo/weights/best_yolo26n_assgn2_v6_50e.pt';config=ROOT/'object-detection-demo/scene_profiles.json'
 hashes={str(p.relative_to(ROOT)):sha(p) for p in [weight,config,ROOT/'object-detection-demo/detector_config.py',ROOT/'object-detection-demo/tcp_server.py']}
 initialDisk=psutil.disk_usage(str(ROOT)).free;began=time.monotonic();minRam=1e99;maxRss=0;process=None
 def guard():
  nonlocal minRam,maxRss
  ram=psutil.virtual_memory().available;disk=psutil.disk_usage(str(ROOT)).free
  rss=psutil.Process().memory_info().rss
  if process and process.poll() is None:
   server=psutil.Process(process.pid)
   for owned in [server]+server.children(recursive=True):
    try:rss+=owned.memory_info().rss
    except psutil.NoSuchProcess:pass
  minRam=min(minRam,ram);maxRss=max(maxRss,rss)
  assert ram>=3*1024**3 and rss<3*1024**3,'RAM guard'
  assert disk>=20*1024**3 and initialDisk-disk<64*1024**2,'Disk guard'
  assert time.monotonic()-began<180,'Three minute budget'
  if out.exists():assert sum(p.stat().st_size for p in out.iterdir() if p.is_file())<8*1024**2,'Output guard'
 guard();known={};perceptual=[]
 for split in ['train','valid']:
  images=sorted((ROOT/'Find lighter'/split/'images').glob('*.jpg'));assert len(images)<200
  for p in images:
   known.setdefault(pixels(p),[]).append(p.relative_to(ROOT).as_posix());perceptual.append((p.relative_to(ROOT).as_posix(),dhash(p)))
 frameRows=[]
 for p in frames:
  with Image.open(p) as im:w,h=im.size
  label=p.parent.parent/'labels'/(p.stem+'.txt')
  hcode=dhash(p);near=sorted([{'image':name,'hamming':(hcode^code).bit_count()} for name,code in perceptual],key=lambda x:x['hamming'])
  frameRows.append({'image':p.relative_to(ROOT).as_posix(),'imageSHA256':sha(p),'pixelSHA256':pixels(p),'label':label.relative_to(ROOT).as_posix(),'labelSHA256':sha(label),'size':[w,h],'draftBoxes':boxes(label,w,h),'exactPixelMatchesToLocalTrainValid':known.get(pixels(p),[]),'perceptualClosestToLocalTrainValid':near[:3],'perceptualMatchesAtHamming4':[x for x in near if x['hamming']<=4]})
 with socket.socket() as probe:probe.bind(('127.0.0.1',5005));port=5005
 out.mkdir(parents=True);responses=[]
 try:
  with (out/'server.log').open('w',encoding='utf-8') as log:
   process=subprocess.Popen([sys.executable,'-u',str(ROOT/'object-detection-demo/tcp_server.py'),'--headless','--host','127.0.0.1','--device','cpu'],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0)
   for session in [0,1]:
    deadline=time.monotonic()+30
    while True:
     guard();assert process.poll() is None,'Server exited'
     try:client=socket.create_connection(('127.0.0.1',port),timeout=1);break
     except OSError:
      if time.monotonic()>deadline:raise TimeoutError('Startup/reconnect deadline')
      time.sleep(.1)
    with client:
     client.settimeout(15)
     with client.makefile('rb') as replyStream:
      rows=frameRows if session==0 else frameRows[:1]
      for index,row in enumerate(rows,1):
       guard();payload=(ROOT/row['image']).read_bytes();w,h=row['size'];timestamp=100000+index
       # Mix v1 and v2 lighter frames on the same live service; fresh session restarts at 1.
       protocol=b'\x01' if index%2 else b'\x02\x00'
       t=time.monotonic();client.sendall(b'PICF'+protocol+struct.pack('<IqIII',index,timestamp,w,h,len(payload))+payload)
       raw=replyStream.readline(65536);assert raw.endswith(b'\n'),'Missing bounded reply'
       reply=json.loads(raw);elapsed=time.monotonic()-t
       assert reply['frame_id']==index and reply['timestamp']==timestamp and reply['width']==w and reply['height']==h and reply['scene']=='lighter' and reply['status']=='OK',reply
       detections=reply['detections'];assert len(detections)<=1
       for d in detections:
        assert d['class']=='Lighter' and .399<=d['confidence']<=1
        assert all(math.isfinite(d[k]) for k in ['x1','y1','x2','y2','centerX','centerY'])
        assert 0<=d['x1']<d['x2']<=w+.01 and 0<=d['y1']<d['y2']<=h+.01
       best=max((iou(b,[d[k] for k in ['x1','y1','x2','y2']]) for b in row['draftBoxes'] for d in detections),default=0)
       responses.append({'session':session,'image':row['image'],'elapsedSeconds':elapsed,'bestDraftIoU':best,'reply':reply})
   guard()
 finally:
  if process and process.poll() is None:
   server=psutil.Process(process.pid);children=server.children(recursive=True)
   process.terminate()
   try:process.wait(timeout=5)
   except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
   for child in children:
    try:child.terminate()
    except psutil.NoSuchProcess:pass
   _,alive=psutil.wait_procs(children,timeout=5)
   for child in alive:
    try:child.kill()
    except psutil.NoSuchProcess:pass
   psutil.wait_procs(alive,timeout=5)
 assert all(sha(ROOT/p)==v for p,v in hashes.items())
 report={'status':'PASS_REAL_WEIGHT_TCP_REPLAY_NOT_INDEPENDENT_ACCURACY_OR_LIVE_PICO','identities':hashes,'frames':frameRows,'responses':responses,'summary':{'originalFrames':14,'requests':len(responses),'framesWithDetection':sum(bool(x['reply']['detections']) for x in responses if x['session']==0),'framesWithDraftIoU50':sum(x['bestDraftIoU']>=.5 for x in responses if x['session']==0),'exactTrainValidOverlapImages':sum(bool(x['exactPixelMatchesToLocalTrainValid']) for x in frameRows),'perceptualFlaggedTestImages':sum(bool(x['perceptualMatchesAtHamming4']) for x in frameRows),'reconnectFrame1Passed':True},'resources':{'minSampledAvailableRAMGiB':minRam/1024**3,'maxSampledParentServerTreeRSSMiB':maxRss/1024**2,'seconds':time.monotonic()-began,'ramFloorGiB':3,'diskGrowthLimitMiB':64,'sampling':'Between requests and startup attempts, not continuous peaks'},'limitations':['PICO provenance inferred from local filenames/export; original capture session identity unavailable.','v6 training preparation/audits absent locally; historical/pretraining overlap remains unknown even if no local exact duplicate.','Supplied polygon/box labels not reapproved; derived box IoU is diagnostic only.','dHash Hamming <=4 flags similarity; not proof of v6 training membership or independent capture.','Input files replayed individually; tracker IDs across different photos do not prove physical identity.','No independent holdout accuracy, negative specificity, stereo positioning, live camera, real hand or animation acceptance.'],'trainingPerformed':False,'downloads':0,'deploymentChanged':False}
 (out/'result.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in report.items() if k not in ['frames','responses']}))
if __name__=='__main__':main()
