"""Single native-CPU validation intervention: multi-label NMS off, no selection."""
import hashlib,json,inspect
from pathlib import Path
from ultralytics import YOLO
from ultralytics.models.yolo.detect.val import DetectionValidator
from ultralytics.utils import nms
ROOT=Path(__file__).resolve().parents[2]


class SingleLabelValidator(DetectionValidator):
    def postprocess(self,preds):
        outputs=nms.non_max_suppression(preds,self.args.conf,self.args.iou,
            nc=0 if self.args.task=='detect' else self.nc,multi_label=False,
            agnostic=self.args.single_cls or self.args.agnostic_nms,
            max_det=self.args.max_det,end2end=self.end2end,rotated=self.args.task=='obb')
        return [dict(bboxes=x[:,:4],conf=x[:,4],cls=x[:,5],extra=x[:,6:]) for x in outputs]


def main():
    out=ROOT/'Reports/plant-v56-native-single-label-diagnostic'
    assert not out.exists();out.mkdir()
    prior=json.loads((ROOT/'Reports/plant-v56-native-cpu-diagnostic/protocol.json').read_text())
    weight=ROOT/'Reports/plant-v56-official-diagnostic/train/weights/best.pt'
    assert hashlib.sha256(weight.read_bytes()).hexdigest()==prior['weight_sha256']
    options=prior['options'].copy();options['project']=str(out)
    (out/'protocol.json').write_text(json.dumps(dict(weight_sha256=prior['weight_sha256'],options=options,
        intervention='Only native postprocess multi_label=False instead of True; project path changed for evidence isolation.',
        implementation_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),deployment_accepted=False),indent=2),encoding='utf-8')
    metrics=YOLO(weight).val(validator=SingleLabelValidator,**options)
    result=dict(results=metrics.results_dict,names=metrics.names,box_ap=metrics.box.ap.tolist(),
                box_ap_class_index=metrics.box.ap_class_index.tolist(),deployment_accepted=False)
    (out/'metrics.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result))


if __name__=='__main__':main()
