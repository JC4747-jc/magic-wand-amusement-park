import hashlib,tempfile,unittest
from pathlib import Path
from stage_plant_broader_v14_batch2 import validate_review_row
class ReviewGateTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=Path(self.temp.name);(self.root/'image.jpg').write_bytes(b'source image bytes');(self.root/'label.txt').write_text('');self.row=dict(original_reviewed=True,visual_annotation_approved=True,source_image='image.jpg',source_sha256=hashlib.sha256(b'source image bytes').hexdigest(),label='label.txt',label_sha256=hashlib.sha256(b'').hexdigest(),negative=True,original_negative_reviewed=True,overlay_reviewed=False)
 def test_reviewed_empty_negative_needs_no_fake_overlay_review(self):
  validate_review_row(self.row,self.root)
 def test_negative_without_original_negative_review_rejected(self):
  self.row['original_negative_reviewed']=False
  with self.assertRaises(AssertionError):validate_review_row(self.row,self.root)
 def test_nonempty_label_cannot_be_declared_negative(self):
  (self.root/'label.txt').write_text('1 .5 .5 .2 .2\n');self.row['label_sha256']=hashlib.sha256((self.root/'label.txt').read_bytes()).hexdigest()
  with self.assertRaises(AssertionError):validate_review_row(self.row,self.root)
 def test_positive_requires_actual_overlay_review(self):
  (self.root/'label.txt').write_text('1 .5 .5 .2 .2\n');self.row.update(negative=False,label_sha256=hashlib.sha256((self.root/'label.txt').read_bytes()).hexdigest())
  with self.assertRaises(AssertionError):validate_review_row(self.row,self.root)
 def test_changed_source_bytes_rejected(self):
  (self.root/'image.jpg').write_bytes(b'changed')
  with self.assertRaises(AssertionError):validate_review_row(self.row,self.root)
if __name__=='__main__':unittest.main()
