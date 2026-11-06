"""Regression gates must reject lost old targets even when aggregate metrics pass."""
from copy import deepcopy
import unittest
from runtime_checkpoint_selection_v57 import rank_report

def fixture():
 names=[f'images/val/{i}.jpg' for i in range(30)]
 rows=[dict(image=n,ground_truth=int(i<22),true_positive=int(i<22),false_positive=0,false_negative=0) for i,n in enumerate(names)]
 report=dict(split='val',excluded_samples=[],allowed_classes=['PottedPlant'],operating_confidence=.35,imgsz=640,rect=True,max_det=8,nms_iou=.7,per_image=rows,images=30,ground_truth_instances=22,operating_per_class=[dict(name='PottedPlant',true_positive=22,false_positive=0,false_negative=0)],per_class=[dict(name='PottedPlant',mAP50_95=.8)])
 return report,dict(val_images=names,regression_images=names[:13],target_instances=22)

class Gates(unittest.TestCase):
 def test_full_pass_cannot_hide_old_cohort_loss(self):
  report,policy=fixture()
  for row in report['per_image'][:3]:row.update(true_positive=0,false_negative=1)
  report['operating_per_class'][0].update(true_positive=19,false_negative=3)
  score,groups=rank_report(report,policy)
  self.assertGreater(groups['full']['recall'],.8)
  self.assertLess(groups['original13']['recall'],.8)
  self.assertEqual(score[0],0)
 def test_missing_or_duplicated_image_is_rejected(self):
  report,policy=fixture();report['per_image'][-1]=deepcopy(report['per_image'][0])
  with self.assertRaises(AssertionError):rank_report(report,policy)
 def test_native_or_wrong_runtime_policy_is_rejected(self):
  report,policy=fixture();report['rect']=False
  with self.assertRaises(AssertionError):rank_report(report,policy)
 def test_both_cohorts_pass(self):
  report,policy=fixture();self.assertEqual(rank_report(report,policy)[0][0],1)

if __name__=='__main__':unittest.main()
