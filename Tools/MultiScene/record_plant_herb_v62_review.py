"""Store actual original-image review, before labels or model predictions."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
NOTES={
66493232:'One sparse soil-grown plant in large terracotta pot clipped at bottom; empty dark vessel in left background is not a plant target.',
61738410:'Packaged hydroponic basil, multiple cropped packages; rooted-container domain and full instance ownership hold.',
90466407:'One clear soil-grown basil in black nursery pot on white saucer, balcony background; detached leaf on saucer excluded from rooted target.',
146040709:'Small herb at window opening with almost entirely occluded container; container evidence and target bounds hold.',
146665257:'Long balcony planter plus separate partial soil planter at top; requires complete two-container census.',
146636995:'Wilted basil in foreground black pot, background jar possibly supporting additional plant; ownership/material hold.',
148029028:'One basil plant in decorated ceramic soil pot clipped at right/bottom; explicit visible container target.',
55902169:'Purple/green basil stems in grow bag with adjacent cropped planted bags; full multi-container census hold.',
36674871:'Single soil basil in small black nursery pot on green tile; shadow excluded from target.',
81993287:'Image appears to be a print/photo depiction of six named herb pots; depiction/material and instance census hold, not automatically real plants or empty negative.',
51440928:'Basil in foreground mug/planter; background partial container at left and metal vessel at right need contents/census review.',
58339222:'Sparse woody herb in front terracotta pot plus planted pots behind/left and cropped right foliage; full instance ownership hold.'}


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
 source=ROOT/'Datasets/plant-herb-train-source-v62';m=json.loads((source/'manifest.json').read_text())
 rows=[]
 for r in m['samples']:
  assert sha(source/r['image'])==r['sha256']
  rows.append(dict(page_id=r['page_id'],image=r['image'],image_sha256=r['sha256'],observation=NOTES[r['page_id']],
   original_actually_viewed=True,reserved_split='train',annotation_approved=False))
 assert len(rows)==12
 out=ROOT/'Reports/plant-herb-train-v62-original-review.json';assert not out.exists()
 out.write_text(json.dumps(dict(source_manifest_sha256=sha(source/'manifest.json'),samples=rows,
  training_approved=False,deployment_accepted=False,pixel_history_pending=True),indent=2),encoding='utf-8')
 print('All12 originals reviewed; no labels adopted')


if __name__=='__main__':main()
