import tempfile
import unittest
from pathlib import Path
from export_training_workspace import reviewed_split_lists


class PortableSplitListTests(unittest.TestCase):
    def test_auxiliary_subset_preserved_without_filling_held_image(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'native-val.txt').write_text('./images/val/a.jpg\n')
            rows = [{'split': 'val', 'image': 'images/val/a.jpg'},
                    {'split': 'val', 'image': 'images/val/held.jpg'}]
            self.assertEqual(reviewed_split_lists(root, {'val': 'native-val.txt'}, rows),
                             {'native-val.txt': ['images/val/a.jpg']})

    def test_training_image_cannot_leak_into_validation_list(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'val.txt').write_text('./images/train/a.jpg\n')
            with self.assertRaisesRegex(ValueError, 'crosses reviewed folds'):
                reviewed_split_lists(root, {'val': 'val.txt'},
                                     [{'split': 'train', 'image': 'images/train/a.jpg'}])

    def test_empty_or_duplicate_membership_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for text in ('', './images/val/a.jpg\n./images/val/a.jpg\n'):
                (root / 'val.txt').write_text(text)
                with self.assertRaises(ValueError):
                    reviewed_split_lists(root, {'val': 'val.txt'},
                                         [{'split': 'val', 'image': 'images/val/a.jpg'}])


if __name__ == '__main__':
    unittest.main()
