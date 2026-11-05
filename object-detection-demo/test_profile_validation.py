import json
import tempfile
import unittest
from pathlib import Path

from discovery import DiscoveryPool
from scene_protocol import load_profile


class ProfileValidationTests(unittest.TestCase):
    def test_unspecified_non_lighter_status_is_not_baseline(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'candidate.pt').write_bytes(b'fixture-only-not-a-model')
            config = root / 'profiles.json'
            config.write_text(json.dumps({'tree': {'weights': 'candidate.pt',
                'allowed': ['Tree'], 'aliases': {'tree': 'Tree'}}}))
            self.assertEqual(load_profile('tree', config)['validation'], 'UNVALIDATED')
            self.assertEqual(DiscoveryPool(config, root).sources[0]['validation']['tree'], 'UNVALIDATED')

    def test_empty_or_invalid_status_cannot_escape_validation_gate(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'candidate.pt').write_bytes(b'fixture-only-not-a-model')
            config = root / 'profiles.json'
            for status in (None, '', '   ', False, 0, [], {}):
                with self.subTest(status=status):
                    config.write_text(json.dumps({'tree': {'weights': 'candidate.pt',
                        'allowed': ['Tree'], 'validation': status}}))
                    self.assertEqual(load_profile('tree', config)['validation'], 'UNVALIDATED')
                    self.assertEqual(DiscoveryPool(config, root).sources[0]['validation']['tree'], 'UNVALIDATED')

    def test_explicit_rejection_and_lighter_baseline_preserved(self):
        with tempfile.TemporaryDirectory() as directory:
            config = Path(directory) / 'profiles.json'
            config.write_text(json.dumps({'tree': {'weights': 'candidate.pt',
                'validation': 'REJECTED_VALIDATION'}, 'lighter': {'weights': 'baseline.pt'}}))
            self.assertEqual(load_profile('tree', config)['validation'], 'REJECTED_VALIDATION')
            self.assertEqual(load_profile('lighter', config)['validation'], 'MAIN_BASELINE')


if __name__ == '__main__':
    unittest.main()
