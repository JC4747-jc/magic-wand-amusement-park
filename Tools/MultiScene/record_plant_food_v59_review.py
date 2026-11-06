"""Record visual absence review before any inference or training."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
NOTES = {
    114303973: 'Close-up chopped lettuce, carrots and dried fruit in dressing; no planted container or cut-flower arrangement.',
    74777139: 'Whole lettuce leaves presented as salad in white serving bowl with condiments/menu; no planted container.',
    37230225: 'Low-light takeaway salad and bread in food tray; no plant container or flower arrangement.',
    84673520: 'Loose salad leaves and tomatoes on dining plate with cutlery; no planted container. Creator account history hit requires hold.',
    15015048: 'Cut lettuce wedges on white serving plate; no planted container or cut-flower arrangement.',
    130649159: 'Cabbage/lettuce leaves, grated carrot and radish on plate; no planted container or flower arrangement.',
    44661929: 'Loose lettuce salad in white bowl with serving spoons; no planted container or flower arrangement.',
    9946868: 'Lettuce, eggs, olives and tomatoes on dining plate; no planted container or flower arrangement.',
    37013757: 'Lettuce, beans, rice and tomatoes on plate; no planted container or flower arrangement.',
    187984327: 'Lettuce, chopped onion and pepper in red serving bowl; no planted container or flower arrangement.',
    83010788: 'Cooked meat with lettuce garnish, soup, rice and squash; no planted container. Missing direct creator link held pending role review.',
    163384967: 'Lettuce salad with dressing and garnish on square plate; no planted container. Missing direct creator link held pending role review.',
}


def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    source = ROOT/'Datasets/plant-food-train-source-v59'
    manifest = json.loads((source/'manifest.json').read_text(encoding='utf-8'))
    identities = json.loads((ROOT/'Reports/plant-food-train-v59-identity-history-v2.json').read_text())
    creators = json.loads((ROOT/'Reports/plant-food-train-v59-creator-links.json').read_text())
    hits = {r['page_id'] for r in identities['rows'] if r['historical_hits']}
    missing = {manifest['samples'][i]['page_id'] for i in creators['missing_creator_link_indices']}
    assert hits == {84673520} and missing == {84673520, 83010788, 163384967}
    rows = []
    for row in manifest['samples']:
        assert sha(source/row['image']) == row['sha256']
        rows.append(dict(page_id=row['page_id'], image=row['image'], image_sha256=row['sha256'],
                         observation=NOTES[row['page_id']], original_actually_viewed=True,
                         reserved_split='train', visual_negative_for=['PottedPlant', 'CutArrangement'],
                         source_identity_hold=row['page_id'] in hits|missing,
                         label_payload_created=False, training_approved=False))
    assert len(rows) == 12
    out = ROOT/'Reports/plant-food-train-v59-original-review.json'
    assert not out.exists()
    out.write_text(json.dumps(dict(samples=rows, source_manifest_sha256=sha(source/'manifest.json'),
        candidate_negative_photos=9, held_photos=3, pixel_history_pending=True,
        training_approved=False, deployment_accepted=False), indent=2), encoding='utf-8')
    print('Reviewed12 originals; nine candidate negatives; three identity holds; no labels adopted')


if __name__ == '__main__': main()
