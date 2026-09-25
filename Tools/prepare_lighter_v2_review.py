"""Separate review candidates from empty labels; do not claim training readiness."""
import json
import zipfile
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'Find lighter'
AUDIT = ROOT / 'audit_lighter_v2'
OUT = ROOT / 'lighter_v2_review'


def main():
    report = json.loads((AUDIT/'audit.json').read_text(encoding='utf-8'))
    if OUT.exists():
        raise SystemExit(f'Refusing to overwrite {OUT}')
    OUT.mkdir()
    with zipfile.ZipFile(ROOT/'lighter.v2i.yolo26.zip') as z:
        for r in report['records']:
            parts = Path(r['name']).parts
            if len(parts) != 3 or parts[0] not in ('train','valid','test') or parts[1] != 'images' or '..' in parts:
                raise ValueError(r['name'])
            group = 'annotated_candidates' if r['boxes'] else 'empty_labels_needs_review'
            image_path = OUT/group/Path(r['name'])
            label_name = r['name'].replace('/images/','/labels/').rsplit('.',1)[0]+'.txt'
            label_path = OUT/group/label_name
            image_path.parent.mkdir(parents=True,exist_ok=True)
            label_path.parent.mkdir(parents=True,exist_ok=True)
            image_path.write_bytes(z.read(r['name']))
            label_path.write_bytes(z.read(label_name))
        for name in ('README.dataset.txt','README.roboflow.txt','data.yaml'):
            (OUT/('source_'+name)).write_bytes(z.read(name))
    counts = {s:dict(Counter('annotated' if r['boxes'] else 'empty' for r in report['records'] if r['split']==s)) for s in ('train','valid','test')}
    text = f'''# 新打火机数据集检查报告

文件：lighter.v2i.yolo26.zip。文件名被重命名，包内 Roboflow 版本实际为 1。

- ZIP CRC 校验通过，1000 张图片均可解码，图片标签配对齐全。
- 实际类别：单类 lighter（ID 0）；845 个框，已检查数值有限性、中心坐标及宽高范围。
- 574 张有框；426 张为空标签（42.6%）。抽样发现多个空标签图片包含可见打火机，不能当成已确认的负样本。
- 原划分：train 800 / valid 100 / test 100。
- 分组计数：{json.dumps(counts, ensure_ascii=False)}。
- 解码像素完全重复组：0；与旧 detect_merged_v3_quad 的像素完全重复：0。
- dHash 距离 <=4 的相似图片候选：1927 对，其中跨划分 826 对；与旧数据相似候选 1025 对。这是筛查结果，不是确认重复，白底产品图可能误匹配。
- ZIP 中 data.yaml 同名出现 3 次，读取到的内容一致；无需更改原 ZIP。

## 目视抽查

固定随机种子抽查 60 张，见 samples_1.jpg 至 samples_3.jpg，红框来自原标签。
例如样本 2、4、20、26、30、36、44、50、52 可见打火机而无框；样本 11 的产品侧视图未标注。
图片混合了白底商品图、手持实拍、椅子、地面等场景；同场景连续帧较多。
因此，有框的 574 张也只是候选，不能据此认定每张图片中的所有打火机都已标注。

## 已完成的整理

- lighter_v2_review/annotated_candidates：574 张有标注候选，保留原划分和标签。
- lighter_v2_review/empty_labels_needs_review：426 张空标签待核查、补标图，保留原标签。
- 原始来源信息和 YAML 留在 lighter_v2_review/source_*。
- 原始压缩包、旧数据和模型均保留；本次没有启动训练或替换部署模型。

## 下一步

优先修复可见打火机的漏标，逐张核对所有目标；不能用模型预测直接当作真实标签。
按拍摄序列分组，复核相似图后去重，避免连续帧跨训练和评估集。
与旧数据合并前排查旧验证、测试集相似图；保留独立的 PICO 实测测试集。
补标完成后再合并训练，能降低将打火机误教成背景的风险。
'''
    (AUDIT/'REPORT.md').write_text(text,encoding='utf-8')
    print(json.dumps(counts))
    print(OUT)


if __name__ == '__main__':
    main()
