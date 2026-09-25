"""Audit new assgn2 data and render every image with its supplied boxes."""
import io
import json
import zipfile
import sys
from PIL import Image, ImageDraw
import audit_lighter_v2 as audit

audit.ZIP=audit.ROOT/'assgn 2.v1i.yolo26.zip'
audit.OUT=audit.ROOT/'audit_assgn2'
audit.ALLOW_POLYGONS=True
if len(sys.argv) == 3:
    audit.ZIP = audit.ROOT / sys.argv[1]
    audit.OUT = audit.ROOT / sys.argv[2]

if __name__=='__main__':
    audit.main()
    path=audit.OUT/'audit.json'
    report=json.loads(path.read_text())
    report['additional_overlaps']={}
    for source in ('audit_lighter_v2','audit_lighter_v3','audit_assgn2'):
        if source == audit.OUT.name:
            continue
        previous=json.loads((audit.ROOT/source/'audit.json').read_text())['records']
        report['additional_overlaps'][source]=[{'new':r['name'],'previous':s['name'],'exact':r['digest']==s['digest'],'distance':(r['dhash']^s['dhash']).bit_count()} for r in report['records'] for s in previous if r['digest']==s['digest'] or (r['dhash']^s['dhash']).bit_count()<=4]
    path.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    with zipfile.ZipFile(audit.ZIP) as z:
        for page in range((len(report['records'])+24)//25):
            sheet=Image.new('RGB',(1500,1500),'white')
            draw=ImageDraw.Draw(sheet)
            for i,r in enumerate(report['records'][page*25:(page+1)*25]):
                im=Image.open(io.BytesIO(z.read(r['name']))).convert('RGB').resize((300,270))
                d=ImageDraw.Draw(im)
                for _,x,y,w,h in r['boxes']:
                    d.rectangle(((x-w/2)*300,(y-h/2)*270,(x+w/2)*300,(y+h/2)*270),outline='red',width=2)
                x0,y0=i%5*300,i//5*300
                sheet.paste(im,(x0,y0))
                draw.text((x0+2,y0+272),f'{page*25+i}: '+r['name'].split('/')[-1][:28],fill='black')
            sheet.save(audit.OUT/f'full_review_{page+1}.jpg')
    print('additional overlaps',{s:len(v) for s,v in report['additional_overlaps'].items()})
