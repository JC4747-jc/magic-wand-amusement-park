import unittest
from train import validate_plan
class PlanTests(unittest.TestCase):
 def plan(self):return dict(epochs=15,trials=[dict(name='baseline',overrides={'lr0':.0001},hypothesis='baseline')])
 def contract(self):return dict(editable_training_fields=['lr0','freeze','scale'])
 def test_valid(self):validate_plan(self.plan(),self.contract())
 def test_threshold_forbidden(self):
  p=self.plan();p['trials'][0]['overrides']['conf']=.1
  with self.assertRaises(ValueError):validate_plan(p,self.contract())
 def test_excess_trials(self):
  p=self.plan();p['trials']*=4
  with self.assertRaises(ValueError):validate_plan(p,self.contract())
 def test_changed_budget(self):
  p=self.plan();p['epochs']=50
  with self.assertRaises(ValueError):validate_plan(p,self.contract())
 def test_nan(self):
  p=self.plan();p['trials'][0]['overrides']['lr0']=float('nan')
  with self.assertRaises(ValueError):validate_plan(p,self.contract())
if __name__=='__main__':unittest.main()
