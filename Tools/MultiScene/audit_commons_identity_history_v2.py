"""Exact creator-account audit; retain v1 reports and avoid uploader/substring matches."""
import argparse
import hashlib
import html
import json
import re
from pathlib import Path
from urllib.parse import unquote

def identities(row):
    metadata=row.get('publisher_metadata',{})
    if not isinstance(metadata,dict):metadata={}
    values=[v.get('value','') if isinstance(v,dict) else str(v) for v in [metadata.get(k,{}) for k in ['Artist','Credit']]]
    attribution=row.get('attribution',{})
    if isinstance(attribution,dict):values.extend(attribution.get(k,'') for k in ['AuthorProfileURL','OriginalLandingURL'])
    else:values.append(str(attribution))
    values.extend(row.get(k,'') for k in ['group','global_group','artist'])
    text=unquote(html.unescape(' '.join(str(v) for v in values)))
    accounts={a.casefold() for a in re.findall(r'flickr\.com/(?:photos|people)/([^/"<>?&#\s\\]+)',text,re.I)}
    users={u.strip().replace('_',' ').casefold() for u in re.findall(r'(?:commons\.wikimedia\.org/(?:wiki/|w/index\.php\?title=)|(?:de\.)?wikipedia\.org/wiki/)(?:User:|Benutzer:)([^/"<>?&#\\]+)',text,re.I)}
    return {'flickr_accounts':accounts,'commons_users':users}

def match_reasons(row,old):
    reasons=[]
    if row.get('page_id') is not None and row['page_id']==old.get('page_id'):reasons.append('same_page')
    if row.get('artist') and old.get('artist') and row['artist'].casefold()==old['artist'].casefold():reasons.append('same_artist_string')
    if row.get('group') and row['group'] in (old.get('group'),old.get('global_group')):reasons.append('same_group')
    a,b=identities(row),identities(old)
    for kind in a:
        if a[kind]&b[kind]:reasons.append('exact_'+kind)
    return reasons

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source',required=True,type=Path)
    p.add_argument('--datasets',required=True,type=Path)
    p.add_argument('--out',required=True,type=Path)
    args=p.parse_args();source=args.source.resolve()/'manifest.json';root=args.datasets.resolve()
    assert not args.out.exists()
    sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
    history=[];errors=[];inputs=[]
    for path in sorted(set(root.rglob('manifest.json'))|set(root.rglob('audit.json'))):
        if path==source:continue
        try:
            history.append((path,json.loads(path.read_text(encoding='utf-8-sig')).get('samples',[])))
            inputs.append(dict(path=path.relative_to(root).as_posix(),sha256=sha(path)))
        except (ValueError,OSError) as e:errors.append(dict(path=str(path),error=type(e).__name__))
    result=[]
    for row in json.loads(source.read_text(encoding='utf-8-sig'))['samples']:
        hits=[]
        for path,samples in history:
            for old in samples:
                reasons=match_reasons(row,old)
                if reasons:hits.append(dict(manifest=path.relative_to(root).as_posix(),image=old.get('image'),split=old.get('split'),group=old.get('group'),reasons=reasons))
        result.append(dict(page_id=row['page_id'],artist=row['artist'],creator_accounts={k:sorted(v) for k,v in identities(row).items()},historical_hits=hits))
    report=dict(version=2,source_manifest_sha256=sha(source),historical_inputs=inputs,errors=errors,rows=result,training_approved=False,
                scope='Exact accounts from Artist/Credit, OIV author profile/landing and group URLs. Permission/reviewer/uploader fields ignored. Same page/group/artist-string remains conservative. No unknown-alias, physical-subject or pretraining-independence guarantee.')
    args.out.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps([dict(page_id=r['page_id'],matches=len(r['historical_hits'])) for r in result]))
if __name__=='__main__':main()
