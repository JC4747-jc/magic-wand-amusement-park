"""Standalone PC webcam lighter test, defaulting to v6. No TCP/PICO required."""
import argparse
import time
from pathlib import Path
import cv2
from ultralytics import YOLO

ROOT = Path(__file__).resolve().parents[1]
DEFAULT = ROOT/'Find lighter/runs/detect/lighter-yolo26n-assgn2-v6-50e/weights/best.pt'


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--camera',type=int,default=0)
    parser.add_argument('--weights',type=Path,default=DEFAULT)
    parser.add_argument('--conf',type=float,default=.399)
    args=parser.parse_args()
    if not args.weights.is_file(): parser.error(f'Missing model: {args.weights}')
    if not 0<=args.conf<=1: parser.error('conf must be between 0 and 1')
    print(f'Model: {args.weights.resolve()}\nCamera: {args.camera}',flush=True)
    model=YOLO(str(args.weights))
    cap=cv2.VideoCapture(args.camera,cv2.CAP_DSHOW)
    title='PC Lighter Debug - V6 default | Q: quit | S: screenshot'
    try:
        if not cap.isOpened():
            raise RuntimeError('Cannot open webcam. Close other camera apps or try --camera 1.')
        cv2.namedWindow(title,cv2.WINDOW_NORMAL)
        previous=time.perf_counter()
        while True:
            ok,frame=cap.read()
            if not ok: raise RuntimeError('Camera frame read failed; check camera connection.')
            start=time.perf_counter()
            result=model.predict(frame,conf=args.conf,imgsz=640,max_det=1,verbose=False)[0]
            predict_ms=(time.perf_counter()-start)*1000
            now=time.perf_counter()
            fps=1/max(now-previous,1e-6)
            previous=now
            picture=result.plot()
            lines=[args.weights.parent.parent.name,
                   f'Output {fps:.1f} FPS | predict {predict_ms:.1f} ms | conf {args.conf} | max_det 1']
            if result.boxes is None or len(result.boxes)==0:
                lines.append('NO LIGHTER DETECTED')
            else:
                box=result.boxes[0]
                x1,y1,x2,y2=box.xyxy[0].tolist()
                lines.append(f'Confidence {float(box.conf[0]):.3f} | center ({(x1+x2)/2:.0f}, {(y1+y2)/2:.0f}) pixels')
                lines.append(f'Box ({x1:.0f}, {y1:.0f}) - ({x2:.0f}, {y2:.0f}) | 2D only')
            picture=cv2.copyMakeBorder(picture,110,0,0,max(0,800-picture.shape[1]),cv2.BORDER_CONSTANT)
            for i,line in enumerate(lines):
                cv2.putText(picture,line,(10,22+24*i),cv2.FONT_HERSHEY_SIMPLEX,.5,(255,255,255),1)
            cv2.imshow(title,picture)
            key=cv2.waitKey(1)&255
            if key in (27,ord('q')) or cv2.getWindowProperty(title,cv2.WND_PROP_VISIBLE)<1: break
            if key==ord('s'):
                folder=ROOT/'Find lighter/debug_captures'
                folder.mkdir(exist_ok=True)
                path=folder/f'webcam_{time.time_ns()}.png'
                if cv2.imwrite(str(path),picture): print(f'Saved: {path}',flush=True)
    finally:
        cap.release()
        cv2.destroyAllWindows()


if __name__=='__main__': main()
