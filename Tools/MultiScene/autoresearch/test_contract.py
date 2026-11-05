import unittest,tempfile,hashlib
from pathlib import Path
from prepare import verify
class ContractTests(unittest.TestCase):
 def test_changed_heldout_label_rejected(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);p=root/'val.txt';p.write_text('original');c={'files':[{'path':'val.txt','sha256':hashlib.sha256(p.read_bytes()).hexdigest()}]};verify(c,root);p.write_text('modified')
   with self.assertRaises(ValueError):verify(c,root)
 def test_missing_input_rejected(self):
  with tempfile.TemporaryDirectory() as d:
   with self.assertRaises(ValueError):verify({'files':[{'path':'missing.pt','sha256':'0'*64}]},Path(d))
 def test_parent_escape_rejected(self):
  with tempfile.TemporaryDirectory() as d:
   with self.assertRaises(ValueError):verify({'files':[{'path':'../escape','sha256':'0'*64}]},Path(d))
if __name__=='__main__':unittest.main()
