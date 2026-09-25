"""Create a verified, self-contained annotation handoff from audited records."""
import hashlib
import json
import shutil
import zipfile
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'Find lighter'
AUDIT = ROOT / 'audit_lighter_v2'
SOURCE = ROOT / 'lighter_v2_review'
DEST = ROOT / 'lighter_v2_annotation_handoff'


def main():
    archive = DEST.with_suffix('.zip')
    if DEST.exists() or archive.exists():
        raise SystemExit('Handoff already exists; refusing to overwrite.')
    records = json.loads((AUDIT / 'audit.json').read_text(encoding='utf-8'))['records']
    DEST.mkdir()
    manifest = []
    for r in records:
        source_group = 'annotated_candidates' if r['boxes'] else 'empty_labels_needs_review'
        group = '02_review_existing_574' if r['boxes'] else '01_relabel_empty_426'
        original = Path(r['name'])
        name = r['split'] + '__' + original.name
        image = DEST / group / 'images' / name
        label = DEST / group / 'labels' / (Path(name).stem + '.txt')
        image.parent.mkdir(parents=True, exist_ok=True)
        label.parent.mkdir(parents=True, exist_ok=True)
        src_image = SOURCE / source_group / original
        src_label = SOURCE / source_group / r['split'] / 'labels' / (original.stem + '.txt')
        for src, dst in ((src_image, image), (src_label, label)):
            shutil.copy2(src, dst)
            if hashlib.sha256(src.read_bytes()).digest() != hashlib.sha256(dst.read_bytes()).digest():
                raise RuntimeError(f'Copy verification failed: {dst}')
        manifest.append(dict(image=image.relative_to(DEST).as_posix(), label=label.relative_to(DEST).as_posix(), original=r['name'], original_split=r['split'], original_box_count=len(r['boxes'])))
    for group in ('01_relabel_empty_426', '02_review_existing_574'):
        (DEST/group/'classes.txt').write_text('lighter\n', encoding='utf-8')
    info = DEST / '03_reference'
    info.mkdir()
    for p in AUDIT.glob('samples_*.jpg'):
        shutil.copy2(p, info/p.name)
    shutil.copy2(AUDIT/'REPORT.md', info/'original_audit_report.md')
    for p in SOURCE.glob('source_*'):
        shutil.copy2(p, info/p.name)
    (info/'file_mapping.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    instructions = '''打火机数据集检查与重新标注交接说明

一、任务与文件夹
本包共 1000 张原始图片，附带每张图片对应的现有 YOLO TXT 标签。
01_relabel_empty_426：426 张空标签图片，优先逐张检查并补标。抽查发现许多图片存在打火机，但没有框，不能直接视作负样本。
02_review_existing_574：574 张已有标注图片，检查是否漏框、错框、框不紧或包含火焰。已有标注共 845 个框，未逐张人工验收。
03_reference：抽查示例、原检查报告、来源信息和文件名映射。示例红框是旧标签，只供参考，不是标准答案，不要拿示例拼图作训练图片。
每个任务文件夹中的 images 是原图，labels 是同名 TXT；classes.txt 定义唯一类别 lighter（ID 0）。

二、统一标注规则
1. 标注类别只用 lighter，小写，类别 ID 为 0。圈住打火机本体，不单独标火焰、手、文字或背景。
2. 每张图中所有可辨认的打火机都要标注。即使部署只显示一个框，训练图片也必须标全多个打火机。
3. 使用紧贴物体的水平矩形框。开启的盖子属于本体；火焰不属于本体。物体边界截断在图像内，不凭空延伸到图外。
4. 可辨认的遮挡目标要标注，并保持一致口径；严重遮挡、极小、模糊、反射或结构不确定的目标写入“检查反馈.txt”，不要随意认定为背景。
5. 只有逐张确认完全没有打火机的图片，才保留空标签，并在反馈中记录为“已确认负样本”。
6. 已有框也要逐个检查；商品拼图中的多个视图如清晰呈现打火机，应完整标注。疑似不适用的长柄点火器等记录反馈，待统一范围。
7. 疑似重复、连续帧或水印图请记录，不必删除。后续按拍摄序列和相似图片重新划分训练与测试，避免评估偏高。

三、标注软件与交付格式
可使用支持 YOLO 检测框的标注工具。导入 images 和对应 labels，并核对类别为 lighter。
若使用 Roboflow，上传图片及对应 TXT，并核对已有框成功导入。最终导出 YOLO 检测框格式（YOLO26/YOLOv8 等），保留原图、不额外增强。
TXT 每行格式：0 x_center y_center width height。四个坐标值以图像宽高归一化到 0～1。
不要改动图片内容或文件名；交付图片与同名 TXT。如软件自动重命名，请保留它导出的全部图片和标签，并在反馈中说明。
本包文件名前的 train__/valid__/test__ 仅用于追溯旧划分，不要求你按它们划分。原始映射见 03_reference/file_mapping.json。
03_reference/source_data.yaml 仅为来源存档，其路径不是本交接包的训练配置。

四、回传
标注完成后，回传整个文件夹的 ZIP 和“检查反馈.txt”。注明完成数量、未完成文件、确认无目标的文件及疑问。
本次只做检查与标注，不需要训练。原模型与原始 ZIP 已在项目中保留。
'''
    (DEST/'请先阅读_标注交接说明.txt').write_text(instructions, encoding='utf-8-sig')
    (DEST/'检查反馈.txt').write_text('标注人：\n完成日期：\n已完成数量：\n未完成图片：\n\n已确认负样本（文件名）：\n\n疑似重复或建议排除（文件名、原因）：\n\n不确定目标或标注问题（文件名、说明）：\n', encoding='utf-8-sig')
    with zipfile.ZipFile(archive, 'x', zipfile.ZIP_DEFLATED) as z:
        for p in sorted(DEST.rglob('*')):
            if p.is_file():
                z.write(p, p.relative_to(DEST.parent).as_posix())
    with zipfile.ZipFile(archive) as z:
        if z.testzip() is not None:
            raise RuntimeError('ZIP CRC failure')
        assert sum('/images/' in n for n in z.namelist()) == 1000
        assert sum('/labels/' in n for n in z.namelist()) == 1000
    print(json.dumps({'folder':str(DEST), 'zip':str(archive), 'size_mb':round(archive.stat().st_size/1024**2,2), 'groups':dict(Counter(m['image'].split('/')[0] for m in manifest)), 'verified_images':1000, 'verified_labels':1000},ensure_ascii=False))


if __name__ == '__main__':
    main()
