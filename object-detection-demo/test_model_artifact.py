import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from model_artifact import verify_artifact
from discovery import DiscoveryPool


class ArtifactTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.weight = self.root / 'best.pt'
        self.weight.write_bytes(b'fixture-only-no-model-inference')
        self.digest = hashlib.sha256(self.weight.read_bytes()).hexdigest()

    def metadata(self, **values):
        (self.root / 'model.json').write_text(json.dumps(values))

    def test_legacy_without_sidecar(self):
        p, digest = verify_artifact(self.weight, {'validation': 'MAIN_BASELINE'})
        self.assertEqual(p['validation'], 'MAIN_BASELINE')
        self.assertEqual(digest, self.digest)

    def test_hash_mismatch_rejected(self):
        self.metadata(sha256='0' * 64)
        with self.assertRaisesRegex(ValueError, 'MODEL_HASH_MISMATCH'):
            verify_artifact(self.weight, {'validation': 'VALIDATED'})

    def test_legacy_metadata_without_status_preserves_pending(self):
        self.metadata(sha256=self.digest)
        p, _ = verify_artifact(self.weight, {'validation': 'PICO_PENDING'})
        self.assertEqual(p['validation'], 'PICO_PENDING')

    def test_discovery_cannot_hide_artifact_rejection(self):
        self.metadata(sha256=self.digest, status='REJECTED_VALIDATION')
        config = self.root / 'profiles.json'
        config.write_text(json.dumps({'tree': {'weights': 'best.pt',
            'allowed': ['Tree'], 'validation': 'VALIDATED'}}))
        pool = DiscoveryPool(config, self.root)
        self.assertIn('REJECTED_VALIDATION', pool.sources[0]['validation']['tree'])
        self.assertEqual(pool.sources[0]['sha256'], self.digest)

    def test_sidecar_cannot_promote_profile(self):
        self.metadata(sha256=self.digest, status='VALIDATED')
        p, _ = verify_artifact(self.weight, {'validation': 'REJECTED_VALIDATION'})
        self.assertIn('REJECTED_VALIDATION', p['validation'])

    def test_cached_weight_cannot_adopt_replacement_metadata(self):
        _, loaded_hash = verify_artifact(self.weight, {'validation': 'PICO_PENDING'})
        self.weight.write_bytes(b'different-file-at-cached-path')
        replacement_hash = hashlib.sha256(self.weight.read_bytes()).hexdigest()
        self.metadata(sha256=replacement_hash, status='VALIDATED')
        with self.assertRaisesRegex(ValueError, 'MODEL_CHANGED_RESTART_REQUIRED'):
            verify_artifact(self.weight, {'validation': 'VALIDATED'}, loaded_hash=loaded_hash)

    def test_unchanged_cached_weight_remains_usable(self):
        p, digest = verify_artifact(self.weight, {'validation': 'PICO_PENDING'}, loaded_hash=self.digest)
        self.assertEqual(digest, self.digest)
        self.assertEqual(p['validation'], 'PICO_PENDING')

    def test_malformed_metadata(self):
        for text in ('[]', '{broken'):
            (self.root / 'model.json').write_text(text)
            with self.assertRaisesRegex(ValueError, 'MODEL_METADATA_INVALID'):
                verify_artifact(self.weight, {})

    def test_auxiliary_alias_cannot_become_interactive_target(self):
        self.metadata(sha256=self.digest, classes={'0': 'PottedPlant', '1': 'CutArrangement'},
                      allowed_classes=['PottedPlant'])
        profile = {'allowed': ['PottedPlant'], 'aliases': {'cutarrangement': 'PottedPlant'}}
        with self.assertRaisesRegex(ValueError, 'MODEL_AUXILIARY_CLASS_ROUTED'):
            verify_artifact(self.weight, profile)
        config = self.root / 'profiles.json'
        config.write_text(json.dumps({'plant': dict(profile, weights='best.pt')}))
        with self.assertRaisesRegex(ValueError, 'MODEL_AUXILIARY_CLASS_ROUTED'):
            DiscoveryPool(config, self.root)

    def test_declared_native_allowlist_and_legacy_profiles_remain_usable(self):
        self.metadata(sha256=self.digest, classes={'0': 'PottedPlant', '1': 'CutArrangement'},
                      allowed_classes=['PottedPlant'])
        profile = {'allowed': ['PottedPlant'], 'aliases': {'pottedplant': 'PottedPlant'}}
        result, digest = verify_artifact(self.weight, profile)
        self.assertEqual(result, profile)
        self.assertEqual(digest, self.digest)

    def test_invalid_declared_allowlist_fails_closed(self):
        for declared in (None, [], 'PottedPlant', ['Absent']):
            self.metadata(sha256=self.digest, classes={'0': 'PottedPlant'}, allowed_classes=declared)
            with self.subTest(declared=declared), self.assertRaisesRegex(ValueError, 'MODEL_CLASS_POLICY_INVALID'):
                verify_artifact(self.weight, {'allowed': ['PottedPlant']})


if __name__ == '__main__':
    unittest.main()
