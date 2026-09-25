"""Prepare deduplicated detection labels and a portable review handoff."""
import hashlib
import json
import zipfile
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'Find lighter'
AUDIT = ROOT / 'audit_lighter_v3'
OUT = ROOT / 'lighter_v3_bbox_review'


def main():
    archive = OUT.with_suffix('.zip')
    if OUT.exists() or archive.exists():
        raise SystemExit('Output already exists; refusing to overwrite.')
    report = json.loads((AUDIT/'audit.json').read_text(encoding='utf-8'))
    if report['errors'] or report['crc_error']:
        raise SystemExit('Resolve audit errors first.')
    OUT.mkdir()
    groups, mapping = defaultdict(list), []
    for r in report['records']:
        groups[r['digest']].append(r)
    conflicts = []
    with zipfile.ZipFile(ROOT/'detect lighters.v3i.yolo26.zip') as z:
        for digest, rows in groups.items():
            # Keep first deterministic record; never combine possibly inconsistent labels.
            unique_boxes = {json.dumps(sorted(r['boxes'])) for r in rows}
            if len(unique_boxes)>1:
                conflicts.append([r['name'] for r in rows])
            for i,r in enumerate(rows):
                group = '01_unique_candidates' if i == 0 else '02_exact_duplicates_reference'
                name = r['split']+'__'+Path(r['name']).name
                image = OUT/group/'images'/name
                label = OUT/group/'labels'/(Path(name).stem+'.txt')
                original = OUT/'03_original_polygon_labels'/(Path(name).stem+'.txt')
                for p in (image,label,original):
                    p.parent.mkdir(parents=True,exist_ok=True)
                image.write_bytes(z.read(r['name']))
                label.write_text(''.join('0 '+' '.join(f'{v:.9f}' for v in b[1:])+'\n' for b in r['boxes']),encoding='utf-8')
                source_label = r['name'].replace('/images/','/labels/').rsplit('.',1)[0]+'.txt'
                original.write_bytes(z.read(source_label))
                mapping.append({'file':image.relative_to(OUT).as_posix(),'original':r['name'],'original_split':r['split'],'pixel_sha256':digest,'kept':i==0,'duplicate_label_conflict':len(unique_boxes)>1})
        ref = OUT/'04_reference'
        ref.mkdir()
        for n in ('data.yaml','README.dataset.txt','README.roboflow.txt'):
            (ref/('source_'+n)).write_bytes(z.read(n))
    for group in ('01_unique_candidates','02_exact_duplicates_reference'):
        (OUT/group/'classes.txt').write_text('lighter\n',encoding='utf-8')
    (ref/'mapping.json').write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')
    (ref/'similarity_candidates.json').write_text(json.dumps({'near_pairs':report['near_pairs'],'exact_label_conflicts':conflicts},ensure_ascii=False,indent=2),encoding='utf-8')
    for p in AUDIT.glob('samples_*.jpg'):
        (ref/p.name).write_bytes(p.read_bytes())
    kept = sum(m['kept'] for m in mapping)
    report_text = f'''# v3 打火机数据检查与交接

原 ZIP：detect lighters.v3i.yolo26.zip；包内 Roboflow 版本为 1，文件名 v3i 是本地命名。
285 张图片，原 train/valid/test = 190/87/8；类别 lighter，ID 0。
ZIP CRC、图片解码、图片标签配对、坐标及转换框边界检查通过。0 张空标签。
319 个原多边形已转换为水平外接矩形（YOLO 五列检测格式）。原轮廓存档在 03_original_polygon_labels。
7 组像素完全重复；唯一图片 {kept} 张，额外重复副本 {285-kept} 张。重复图标签不一致组 {len(conflicts)} 组，详见参考 JSON。
内部相似候选 127 对，其中跨旧划分 68 对；dHash 距离 <=4，仅用于筛查，未将相似图直接判定为重复。
与旧 detect_merged_v3_quad、v2 数据：未发现像素完全重复或上述阈值内相似候选；不能排除旋转、裁剪等其他变体。

目视抽查 60 张：包含桌面、床铺、杂物、暗光、手持和远距离小目标，更贴近实测环境。
抽查框大多覆盖打火机本体，未见上一批大量空标签情况；这不是对全部图片的人工验收。
拍摄环境、打火机外观较集中，且存在同场景连续拍摄图，后续应按场景/序列分组划分，不能直接使用原随机划分来报告泛化效果。

文件夹说明：
- 01_unique_candidates/images 与 labels：去掉完全重复副本后的候选图片和转换检测框。
- 02_exact_duplicates_reference：重复副本，仅供核对，不要重复加入训练。
- 03_original_polygon_labels：原多边形，供追溯，不要混进检测框 labels 文件夹。
- 04_reference：来源、映射、相似候选及 60 张抽查拼图（红框为转换框）。

交给标注同学时：逐张检查全部可辨认打火机是否标全、框是否紧贴本体，火焰和手不标。
遮挡、严重模糊或类型不确定的目标记录疑问；保持类别 lighter / ID 0，保持文件名。
多目标图片必须全部标注，即使部署仅显示一个框。
只需编辑 01_unique_candidates 中同名 labels，回传整包 ZIP；疑似重复或不适用图片记入反馈，不必删除。
本包没有训练用 data.yaml；source_data.yaml 仅为来源存档。正式训练前需完成复核与分组划分。
原 ZIP、旧数据及部署模型未改动，本次未启动训练。
'''
    (AUDIT/'REPORT.md').write_text(report_text,encoding='utf-8')
    (OUT/'请先阅读_检查及标注说明.txt').write_text(report_text,encoding='utf-8-sig')
    (OUT/'检查反馈.txt').write_text('完成数量：\n未完成文件：\n疑似漏标、错标（文件名及说明）：\n疑似重复或建议排除：\n',encoding='utf-8-sig')
    # Verify all 285 image/label pairs and all converted five-column records.
    for m in mapping:
        image = OUT/m['file']
        label = image.parent.parent/'labels'/(image.stem+'.txt')
        for line in label.read_text().splitlines():
            assert len(line.split())==5 and line.split()[0]=='0'
        assert image.is_file() and label.is_file()
    with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(OUT.rglob('*')):
            if p.is_file():
                z.write(p,p.relative_to(OUT.parent).as_posix())
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        assert sum('/images/' in n for n in z.namelist())==285
    print(json.dumps({'unique':kept,'duplicates':285-kept,'label_conflict_groups':len(conflicts),'zip_mb':round(archive.stat().st_size/1024**2,2)},ensure_ascii=False))


if __name__ == '__main__':
    main()
