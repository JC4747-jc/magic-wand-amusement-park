import unittest
import numpy as np
from image_orientation import inference_image, original_detections


class OrientationTests(unittest.TestCase):
    def test_asymmetric_pixels_and_roundtrip(self):
        image=np.arange(18).reshape(2,3,3)
        rotated=inference_image(image,180)
        np.testing.assert_array_equal(rotated[0,0],image[-1,-1])
        np.testing.assert_array_equal(inference_image(rotated,180),image)
        self.assertTrue(rotated.flags.c_contiguous)
        self.assertIs(inference_image(image,0),image)

    def test_boxes_restore_edges_centers_and_metadata(self):
        d=dict(x1=10.,y1=30.,x2=100.,y2=200.,confidence=.9,**{'class':'Lighter'})
        r=original_detections([d],640,480,180)[0]
        self.assertEqual(r['bbox'],[540.,280.,630.,450.])
        self.assertEqual((r['centerX'],r['centerY']),(585.,365.))
        self.assertEqual(r['confidence'],.9)
        self.assertEqual(original_detections([r],640,480,180)[0]['bbox'],[10.,30.,100.,200.])
        self.assertEqual(d['x1'],10.)
        full=dict(x1=0,y1=0,x2=640,y2=480)
        self.assertEqual(original_detections([full],640,480,180)[0]['bbox'],[0,0,640,480])

    def test_empty_default_and_invalid(self):
        ds=[]
        self.assertIs(original_detections(ds,640,480,0),ds)
        self.assertEqual(original_detections(ds,640,480,180),[])
        with self.assertRaises(ValueError): inference_image(np.zeros((2,2)),90)
        with self.assertRaises(ValueError): original_detections([],640,480,90)


if __name__=='__main__': unittest.main()
