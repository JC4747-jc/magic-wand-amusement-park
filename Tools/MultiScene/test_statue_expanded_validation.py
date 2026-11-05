import copy
import unittest
from statue_expanded_validation_selection import score_report

def fixture():
    counts=[14,2,1,1,1,1,1]+[2]+[1]*10+[0]
    rows=[dict(image=f'images/val/{i}.jpg',ground_truth=n,true_positive=n,false_positive=0,false_negative=0) for i,n in enumerate(counts)]
    return dict(split='val',images=19,ground_truth_instances=33,excluded_samples=[],allowed_classes=['Statue'],operating_confidence=.65,imgsz=640,rect=True,max_det=32,nms_iou=.7,per_image=rows,per_class=[dict(name='Statue',mAP50_95=.8)])

class ExpandedValidationChecks(unittest.TestCase):
    old=[f'images/val/{i}.jpg' for i in range(7)]
    def test_both_cohorts_pass(self):
        self.assertEqual(score_report(fixture(),self.old)[0],1)
    def test_pooled_recall_cannot_hide_new_cohort_failure(self):
        r=fixture()
        for row in r['per_image'][8:12]:row.update(true_positive=0,false_negative=1)
        self.assertGreater(29/33,.8)
        self.assertEqual(score_report(r,self.old)[0],0)
    def test_single_background_fp_blocks_gate(self):
        r=fixture();r['per_image'][-1]['false_positive']=1
        self.assertEqual(score_report(r,self.old)[0],0)
    def test_test_split_is_rejected(self):
        r=fixture();r['split']='test'
        with self.assertRaises(AssertionError):score_report(r,self.old)
    def test_preprocessing_change_is_rejected(self):
        r=fixture();r['max_det']=4
        with self.assertRaises(AssertionError):score_report(r,self.old)

if __name__=='__main__':unittest.main()
