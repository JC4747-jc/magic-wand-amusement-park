import unittest
from jumping_spider_runtime_checkpoint_selection import rank_report
class SelectionTests(unittest.TestCase):
 def report(self,p=.9,r=.8):
  return dict(split='val',images=63,ground_truth_instances=13,false_positive_images=0,excluded_samples=[],allowed_classes=['JumpingSpider'],operating_confidence=.65,imgsz=640,rect=True,max_det=8,nms_iou=.7,operating_per_class=[dict(name='JumpingSpider',precision=p,recall=r)],per_class=[dict(name='JumpingSpider',mAP50_95=.6)])
 def test_gate_has_priority(self):self.assertGreater(rank_report(self.report(.9,.8)),rank_report(self.report(.89,1)))
 def test_rejects_native_subset(self):
  r=self.report();r['images']=62
  with self.assertRaises(AssertionError):rank_report(r)
 def test_rejects_test_selection(self):
  r=self.report();r['split']='test'
  with self.assertRaises(AssertionError):rank_report(r)
 def test_rejects_auxiliary_routing(self):
  r=self.report();r['allowed_classes'].append('CutArrangement')
  with self.assertRaises(AssertionError):rank_report(r)
 def test_rejects_threshold_change(self):
  r=self.report();r['operating_confidence']=.5
  with self.assertRaises(AssertionError):rank_report(r)
 def test_background_false_positive_blocks_gate(self):
  r=self.report(1.,1.);r['false_positive_images']=1
  self.assertEqual(rank_report(r)[0],0)
if __name__=='__main__':unittest.main()
