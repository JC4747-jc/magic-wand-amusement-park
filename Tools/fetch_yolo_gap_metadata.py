"""Bounded public metadata discovery for Cockroach/BronzeBeast; no photos or labels."""
import hashlib
import json
from pathlib import Path
import subprocess
from urllib.parse import urlencode

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '.codex-tmp/yolo-training/gap-sources'
CAP = 2 * 1024**2


def fetch(name, url):
    path = OUT / (name + '.json')
    source = OUT / (name + '.source.json')
    if path.exists() and source.exists():
        raw = path.read_bytes()
        previous = json.loads(source.read_text())
        assert previous['url'] == url and previous['sha256'] == hashlib.sha256(raw).hexdigest()
    else:
        result = subprocess.run(['curl.exe', '--silent', '--show-error', '--fail', '--location',
            '--proto', '=https', '--proto-redir', '=https', '--retry', '0', '--max-redirs', '3',
            '--connect-timeout', '10', '--max-time', '30', '--max-filesize', str(CAP),
            '--user-agent', 'MagicMR-bounded-training-data-review/1.0 (research; no bulk scraping)', url],
            capture_output=True, timeout=35, creationflags=subprocess.CREATE_NO_WINDOW)
        if result.returncode:
            raise RuntimeError(name + ': ' + result.stderr.decode(errors='replace')[:300])
        raw = result.stdout
        assert len(raw) <= CAP
        json.loads(raw)
        path.write_bytes(raw)
        source.write_text(json.dumps({'url': url, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}, indent=2))
    assert len(raw) <= CAP
    return json.loads(raw)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    photos = []
    for species in ('Periplaneta americana', 'Blattella germanica'):
        key = species.lower().replace(' ', '-')
        taxa = fetch(key + '-taxon', 'https://api.inaturalist.org/v1/taxa?' + urlencode({'q': species, 'per_page': 3}))
        matches = [r for r in taxa['results'] if r['name'] == species]
        assert len(matches) == 1, 'Exact species taxonomy not established'
        taxon = matches[0]
        observations = fetch(key + '-observations', 'https://api.inaturalist.org/v1/observations?' + urlencode({
            'taxon_id': taxon['id'], 'photo_license': 'cc0,cc-by,cc-by-sa', 'quality_grade': 'research',
            'photos': 'true', 'per_page': 20, 'order_by': 'id', 'order': 'desc', 'd2': '2026-11-15'}))
        for observation in observations['results']:
            if observation['taxon']['id'] != taxon['id']:
                continue
            # One image per observation; observation/photographer/day remain grouping keys.
            eligible = [p for p in observation.get('photos', []) if p.get('license_code') in ('cc0', 'cc-by', 'cc-by-sa')]
            if eligible:
                photo = eligible[0]
                photos.append({'canonical_candidate': 'Cockroach', 'observation_id': observation['id'],
                    'observation_url': observation['uri'], 'observed_on': observation.get('observed_on'),
                    'observer_id': observation['user']['id'], 'taxon_id': taxon['id'], 'taxon_name': species,
                    'photo': photo, 'accepted_for_training': False, 'review_status': 'NEEDS_IMAGE_SEMANTICS_AND_MANUAL_BOX'})
    commons = []
    for search in ('bronze animal China', 'bronze chimera', 'bronze beast'):
        key = 'commons-' + search.replace(' ', '-')
        result = fetch(key, 'https://commons.wikimedia.org/w/api.php?' + urlencode({'action': 'query', 'format': 'json',
            'generator': 'search', 'gsrsearch': search, 'gsrnamespace': 6, 'gsrlimit': 20,
            'prop': 'imageinfo', 'iiprop': 'url|size|sha1|extmetadata', 'iiurlwidth': 1024}))
        assert 'error' not in result, result.get('error')
        for page in result.get('query', {}).get('pages', {}).values():
            if page.get('imageinfo'):
                commons.append({'canonical_candidate': 'BronzeBeast', 'page_id': page['pageid'], 'title': page['title'],
                    'image_info': page['imageinfo'][0], 'accepted_for_training': False,
                    'review_status': 'NEEDS_LICENSE_MATERIAL_OBJECT_ID_SEMANTICS_AND_MANUAL_BOX'})
    commons = list({r['page_id']: r for r in commons}.values())
    report = {'purpose': 'METADATA_CANDIDATES_ONLY', 'images_downloaded': False,
        'caps': {'metadata_file_bytes': CAP, 'max_species_queries': 2, 'observations_per_species': 20,
                 'commons_queries': 3, 'commons_results_per_query': 20, 'parallelism': 1, 'retries': 0},
        'cockroach_candidates': photos, 'bronze_candidates': commons}
    (OUT / 'candidate-index.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'cockroach_candidates': len(photos), 'bronze_candidates': len(commons), 'images_downloaded': False}))


if __name__ == '__main__':
    main()
