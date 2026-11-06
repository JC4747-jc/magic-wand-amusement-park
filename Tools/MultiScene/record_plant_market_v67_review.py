"""Record actual twenty-original census before inference or label adoption."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
NOTES={181871142:'People buying harvested produce in cartons under blue umbrella; no visible planted container or flower arrangement.',71372286:'Close-up harvested vegetables and beans in plastic trays; no rooted containers.',100373042:'Tomatoes on wooden market stand with cabbage at right; no rooted containers.',100283802:'Mixed harvested corn, tomatoes, potatoes and tied greens on wooden market stalls; no planted pots visible.',112394824:'Harvested squash and roots foreground; distant ornamental vegetation behind display needs container census.',22876877:'Harvested leeks with bare roots and celery/cabbage cut bases; no soil-filled planter.',66406924:'Historic monochrome market scene with flower-like background forms; material/creator and target census hold.',25649961:'Indoor market harvested fruit, vegetables and bundled greens; no planted container visible.',106246798:'Harvested tomatoes, onions and potatoes in boxes; historical creator-account hit requires hold.',102660181:'Cut-flower bunches in metal bucket beside squash; auxiliary CutArrangement target exists, not empty negative.',87622862:'Farm stand with many planted roof/ground/hanging flowers; positive census required, not empty negative.',51294815:'Harvested peppers, tomatoes, onions and beans in crates/sacks; no rooted container visible.',74919738:'Harvested tomatoes, cucumbers and leafy herb bundles in large woven basket; no planted pot visible.',76938812:'Dry beans/chickpeas in sacks with price sign; no rooted plant or cut arrangement.',73606065:'Street market with harvested leafy greens and peppers in crates/cardboard; no planted pot visible.',28033854:'Indoor produce market with multiple hanging containers at upper left; contents/census hold.',66561624:'Tilted indoor produce display with bananas and harvested greens; no planted pot visible.',15861894:'Produce baskets with tall cut flowers in narrow vase at upper right and background greenery; auxiliary census/creator hold.',71360318:'Outdoor harvested vegetables in wooden trays with vendor/van; no planted container visible.',96671253:'Harvested leafy bundles and sliced vegetables with vendor; historical creator-account hit requires hold.'}
VISUAL_HOLDS={112394824,66406924,102660181,87622862,28033854,15861894}


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
 source=ROOT/'Datasets/plant-market-train-source-v67';mp=source/'manifest.json';m=json.loads(mp.read_text())
 identity=json.loads((ROOT/'Reports/plant-market-train-v67-identity-history-v2.json').read_text())
 creators=json.loads((ROOT/'Reports/plant-market-train-v67-creator-links.json').read_text())
 identityholds={r['page_id'] for r in identity['rows'] if r['historical_hits']}
 missing={m['samples'][i]['page_id'] for i in creators['missing_creator_link_indices']}
 assert identityholds=={106246798,96671253} and missing=={66406924,102660181,15861894}
 rows=[]
 for r in m['samples']:
  assert sha(source/r['image'])==r['sha256']
  rows.append(dict(page_id=r['page_id'],image=r['image'],image_sha256=r['sha256'],original_actually_viewed=True,
   observation=NOTES[r['page_id']],reserved_split='train',visual_census_hold=r['page_id'] in VISUAL_HOLDS,
   creator_history_hold=r['page_id'] in identityholds|missing,
   candidate_negative=r['page_id'] not in VISUAL_HOLDS|identityholds|missing,annotation_approved=False))
 assert len(rows)==20 and sum(r['candidate_negative'] for r in rows)==12
 out=ROOT/'Reports/plant-market-train-v67-original-review.json';assert not out.exists()
 out.write_text(json.dumps(dict(samples=rows,source_manifest_sha256=sha(mp),candidate_negative_photos=12,
  held_photos=8,acquisition_failures=m['failures'],pixel_history_pending=True,training_approved=False,deployment_accepted=False),indent=2),encoding='utf-8')
 print('Twenty originals viewed; twelve candidate negatives, eight held; no labels adopted')


if __name__=='__main__':main()
