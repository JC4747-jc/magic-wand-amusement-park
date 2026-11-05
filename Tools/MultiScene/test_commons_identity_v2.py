import unittest
from audit_commons_identity_history_v2 import match_reasons

def sample(account):
    return dict(publisher_metadata={'Artist':{'value':f'<a href="https://www.flickr.com/people/{account}/">Author</a>'}})

class IdentityRegression(unittest.TestCase):
    def test_short_alias_not_substring(self):
        self.assertEqual(match_reasons(sample('eltb'),sample('weltbild-schweiz')),[])
    def test_same_account_is_detected(self):
        self.assertIn('exact_flickr_accounts',match_reasons(sample('12950131@N06'),sample('12950131@N06')))
    def test_oiv_attribution_is_detected(self):
        self.assertIn('exact_flickr_accounts',match_reasons(sample('eltb'),{'attribution':{'AuthorProfileURL':'https://flickr.com/photos/eltb/'}}))
    def test_shared_upload_bot_not_creator(self):
        a,b=sample('a'),sample('b')
        for row in [a,b]:row['publisher_metadata']['Permission']={'value':'User:Flickr_upload_bot'}
        self.assertEqual(match_reasons(a,b),[])
    def test_null_page_ids_not_same_page(self):
        self.assertEqual(match_reasons({'page_id':None},{'page_id':None}),[])
    def test_same_page_preserved(self):
        self.assertIn('same_page',match_reasons({'page_id':2},{'page_id':2}))
    def test_string_attribution_supported(self):
        self.assertEqual(match_reasons(sample('a'),{'attribution':'Photographer unknown'}),[])
    def test_full_commons_account_not_first_name(self):
        a={'artist':'https://commons.wikimedia.org/wiki/User:Alice_One'}
        b={'artist':'https://commons.wikimedia.org/wiki/User:Alice_Two'}
        self.assertEqual(match_reasons(a,b),[])

if __name__=='__main__':unittest.main()
