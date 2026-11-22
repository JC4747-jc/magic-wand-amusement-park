"""Protocol/resource-policy tests with explicit predictors; not recognition accuracy."""
from dataclasses import dataclass
import io
import json
from pathlib import Path
import struct
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from scene_protocol import read_packet
from scene_router import SceneRouter


@dataclass
class Config:
    conf: float = .4
    max_det: int = 1


class SceneRouterTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.main_path = (self.root / 'main.pt').resolve()
        self.main_path.write_bytes(b'main')
        self.config_path = self.root / 'profiles.json'
        self.profiles = {}
        for scene, path, label in [('lighter','main.pt','Lighter'),('plant','plant.pt','PottedPlant'),('pet','pet.pt','Cat')]:
            (self.root / path).write_bytes(scene.encode())
            self.profiles[scene] = dict(weights=path, allowed=[label], aliases={label.lower():label},
                                        validation='PUBLIC_BASELINE_ONLY_PICO_PENDING')
        self.config_path.write_text(json.dumps(self.profiles))
        self.loaded, self.calls = [], []
        def load(path):
            self.loaded.append(path.name)
            label = {'plant.pt':'pottedplant', 'pet.pt':'cat'}[path.name]
            return SimpleNamespace(names={0:label})
        def predict(model, image, device, config, classes):
            self.calls.append((model.names, config, classes))
            return [SimpleNamespace(names=model.names)]
        def decode(result):
            return [dict(class_id=0, **{'class':result.names[0]}, confidence=.9,x1=0,y1=0,x2=5,y2=5)]
        self.router = SceneRouter(SimpleNamespace(names={0:'lighter'}), self.main_path, self.config_path,
                                  predict, decode, Config(), 'cpu', loader=load)

    def infer(self, scene, frame_id=1):
        return self.router.infer(SimpleNamespace(scene=scene, frame_id=frame_id, image=None))[0]

    def test_missing_scene_does_not_load_or_infer(self):
        self.assertTrue(self.infer('tree')['status'].startswith('SCENE_NOT_CONFIGURED'))
        self.assertEqual(self.loaded, [])
        self.assertEqual(self.calls, [])

    def test_switching_keeps_only_main_and_one_auxiliary(self):
        for frame, scene in enumerate(['plant','plant','pet','plant','discovery','discovery'],1):
            reply = self.infer(scene, frame)
            self.assertEqual(reply['status'], 'OK')
            self.assertFalse(reply['pose_available'])
            self.assertLessEqual(sum(path != self.main_path for path in self.router.models),1)
            self.assertLessEqual(len(self.router.models),2)
        self.assertEqual(self.loaded[:3], ['plant.pt','pet.pt','plant.pt'])
        self.assertEqual(len(self.calls),6, 'Discovery must run only one predictor per frame')

    def test_route_identity_validation_and_weights_change(self):
        first = self.infer('pet',1)
        second = self.infer('pet',2)
        self.assertEqual(second['model_validation'],'PUBLIC_BASELINE_ONLY_PICO_PENDING')
        self.assertEqual(second['detections'][0]['target_scene'],'pet')
        self.assertEqual(first['detections'][0]['instance_id'],second['detections'][0]['instance_id'])
        (self.root/'pet.pt').write_bytes(b'replacement with different size')
        failed = self.infer('pet',3)
        self.assertTrue(failed['status'].startswith('MODEL_CHANGED_RESTART_REQUIRED'))
        self.assertEqual(failed['detections'],[])
        self.assertEqual(len(self.calls),2)

    def test_park_target_keeps_id_across_brief_observation_dropout(self):
        with patch('scene_router.time.monotonic', side_effect=[10, 10.82, 12.5]):
            before = self.infer('plant', 1)['detections'][0]['instance_id']
            reacquired = self.infer('plant', 2)['detections'][0]['instance_id']
            expired = self.infer('plant', 3)['detections'][0]['instance_id']
        self.assertEqual(reacquired, before)
        self.assertNotEqual(expired, before)

    def test_v1_v2_packet_boundaries_and_unknown_profile(self):
        for scene_id in (None,0,4,7):
            header = bytes([1]) if scene_id is None else bytes([2,scene_id])
            data = io.BytesIO(b'PICF'+header+struct.pack('<IqIII',9,1000,64,48,3)+b'jpg'+b'NEXT')
            packet = read_packet(data.read)
            self.assertEqual(packet[4],b'jpg')
            self.assertEqual(packet[5],{None:None,0:'lighter',4:'room',7:'discovery'}[scene_id])
            self.assertEqual(data.read(),b'NEXT')
        with self.assertRaisesRegex(ValueError,'Unknown scene'):
            read_packet(io.BytesIO(b'PICF'+bytes([2,8])).read)


if __name__ == '__main__':
    unittest.main()
