import unittest
from pathlib import PurePosixPath
from dataset_paths import manifest_relative_path


class DatasetPathTests(unittest.TestCase):
    def test_windows_audit_resolves_under_linux_root(self):
        path = manifest_relative_path(r'images\val\sample.jpg')
        self.assertEqual(PurePosixPath('/home/user/data') / path,
                         PurePosixPath('/home/user/data/images/val/sample.jpg'))
        self.assertEqual(path.with_suffix('.txt').name, 'sample.txt')
        self.assertEqual(path.stem, 'sample')

    def test_linux_and_mixed_paths_use_same_parts(self):
        expected = ('labels', 'test', 'sample.txt')
        for value in ('labels/test/sample.txt', r'labels\test/sample.txt'):
            self.assertEqual(manifest_relative_path(value).parts, expected)

    def test_absolute_and_escaping_paths_fail_clearly(self):
        for value in ('/tmp/sample.jpg', r'C:\data\sample.jpg', r'\\host\data\a.jpg',
                      '../sample.jpg', r'images\..\sample.jpg', '', None):
            with self.subTest(value=value), self.assertRaises(ValueError):
                manifest_relative_path(value)


if __name__ == '__main__':
    unittest.main()
