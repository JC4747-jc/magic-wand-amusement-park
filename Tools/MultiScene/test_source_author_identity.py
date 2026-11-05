import unittest
from source_author_identity import author_identity


class AuthorIdentityTests(unittest.TestCase):
    def test_actual_legacy_conflict(self):
        self.assertEqual(author_identity('flickr-profile:flickr.com/people/maveric2003'),
                         author_identity('https://www.flickr.com/people/maveric2003/'))

    def test_openimages_prefix(self):
        self.assertEqual(author_identity('openimages-v7:https://flickr.com/people/Alice'),
                         author_identity('http://www.flickr.com/people/alice/'))

    def test_distinct_authors_stay_distinct(self):
        self.assertNotEqual(author_identity('https://flickr.com/people/alice'),
                            author_identity('https://flickr.com/people/alice2'))

    def test_distinct_hosts_stay_distinct(self):
        self.assertNotEqual(author_identity('https://flickr.com/people/alice'),
                            author_identity('https://example.com/people/alice'))


if __name__ == '__main__':
    unittest.main()
