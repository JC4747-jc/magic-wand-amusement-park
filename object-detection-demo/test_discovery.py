import json
import tempfile
import unittest
from pathlib import Path
from discovery import DiscoveryPool, detection_limit, inference_limit, filter_candidates


class DiscoveryTests(unittest.TestCase):
    def test_dense_capacity_filters_before_output_limit(self):
        detections=[{'class':'Cat','confidence':.7} for _ in range(20)]
        detections += [{'class':'Statue','confidence':.69,'id':i} for i in range(40)]
        thresholds={'Cat':.8,'Statue':.65}
        for limit in (8,14,32):
            self.assertEqual(inference_limit(thresholds,limit),300)
            result=filter_candidates(detections,thresholds,limit)
            self.assertEqual([d['id'] for d in result],list(range(limit)))
            self.assertEqual(inference_limit({'Statue':.65},limit),limit)

    def test_profile_capacity_shared_model_and_lighter_default(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            (root/'shared.pt').touch();(root/'lighter.pt').touch()
            config=root/'profiles.json'
            config.write_text(json.dumps({
                'statue':{'weights':'shared.pt','max_det':32,'aliases':{'statue':'Statue'}},
                'pet':{'weights':'shared.pt','max_det':14,'aliases':{'cat':'Cat'}},
                'lighter':{'weights':'lighter.pt','max_det':1,'aliases':{'lighter':'Lighter'}}}))
            pool=DiscoveryPool(config,root)
            self.assertEqual(pool.profile(pool.sources[0],{0:'statue',1:'cat'})['max_det'],32)
            self.assertEqual(pool.profile(pool.sources[0],{0:'cat'})['max_det'],14)
            self.assertEqual(pool.profile(pool.sources[1],{0:'lighter'})['max_det'],8)

    def test_invalid_capacity_rejected(self):
        for value in (True,False,0,-1,1.5,'32',None):
            with self.assertRaises(ValueError):detection_limit(value)

    def test_name_mapping_shared_weight_budget_and_missing(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            (root/'coco.pt').touch();(root/'lighter.pt').touch()
            profiles={'pet':{'weights':'coco.pt','aliases':{'cat':'Cat','dog':'Dog'}},
                      'plant':{'weights':'coco.pt','aliases':{'potted plant':'PottedPlant'}},
                      'room':{'weights':'coco.pt','aliases':{}},
                      'lighter':{'weights':'lighter.pt','aliases':{'lighter':'Lighter'}},
                      'tree':{'weights':None},'bugs':{'weights':'missing.pt'}}
            config=root/'profiles.json';config.write_text(json.dumps(profiles))
            pool=DiscoveryPool(config,root)
            self.assertEqual(len(pool.sources),2)
            source=pool.next_source()
            profile=pool.profile(source,{99:'dog',17:'potted plant',500:'chair'})
            self.assertEqual(profile['routes'],{'Dog':'pet','PottedPlant':'plant'})
            self.assertNotIn('chair',profile['aliases'])
            second=pool.next_source()
            self.assertEqual(pool.profile(second,{42:'lighter'})['routes'],{'Lighter':'lighter'})
            self.assertIs(pool.next_source(),source)

    def test_spider_only_profile_does_not_enable_other_bug_class(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);(root/'bugs.pt').touch()
            config=root/'profiles.json';config.write_text(json.dumps({'bugs':{'weights':'bugs.pt','allowed':['Spider'],'aliases':{'spider':'Spider'}}}))
            pool=DiscoveryPool(config,root)
            profile=pool.profile(pool.next_source(),{0:'Spider',1:'Cockroach'})
            self.assertEqual(profile['allowed'],['Spider'])
            self.assertEqual(profile['routes'],{'Spider':'bugs'})

    def test_bronze_beast_existing_class_routes_to_statue(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);(root/'statue.pt').touch()
            config=root/'profiles.json';config.write_text(json.dumps({'statue':{'weights':'statue.pt','allowed':['BronzeBeast'],'aliases':{'bronze beast':'BronzeBeast'}}}))
            pool=DiscoveryPool(config,root)
            self.assertEqual(pool.profile(pool.next_source(),{0:'bronze beast'})['routes'],{'BronzeBeast':'statue'})

    def test_unmapped_classes_fail_explicitly(self):
        with self.assertRaises(ValueError):
            DiscoveryPool.profile({'aliases':{},'scenes':{'pet'}},{0:'chair'})


if __name__=='__main__':unittest.main()
