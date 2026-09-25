"""Summarize completed training and package curves, metrics, and checkpoint."""
import csv
import json
import zipfile
from pathlib import Path

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

ROOT=Path(__file__).resolve().parents[1]
RUNS=ROOT/'Find lighter/runs/detect'
RUN=RUNS/'lighter-yolo26n-freeze-v5-50e'


def main():
    metrics=json.loads((RUN/'comparison_metrics.json').read_text())
    with (RUN/'results.csv').open() as f:
        current=list(csv.DictReader(f))
    assert len(current)==50, f'Only {len(current)} epochs completed'
    with (RUNS/'lighter-yolo26n-quad-v4-50e/results.csv').open() as f:
        old=list(csv.DictReader(f))
    columns=[k for k in current[0] if 'loss' in k or k.startswith('metrics/')]
    fig,axes=plt.subplots(2,5,figsize=(20,8))
    for ax,col in zip(axes.flat,columns):
        for label,rows,color in [('v4',old,'#2563eb'),('v5',current,'#f97316')]:
            if col in rows[0]:
                ax.plot([float(r['epoch']) for r in rows],[float(r[col]) for r in rows],label=label,color=color)
        ax.set_title(col); ax.set_xlabel('Epoch'); ax.grid(alpha=.2); ax.legend()
    fig.suptitle('Training histories: v4 vs v5 (different training/validation datasets; descriptive only)')
    fig.tight_layout(); fig.savefig(RUN/'training_v4_vs_v5.png',dpi=150); plt.close(fig)
    lines=['# YOLO26n v5 训练与同集对比报告','',
           '本次完成 50 轮增量训练，从 v4 最佳权重初始化，未替换 PICO 部署模型。',
           '训练设置：640×640，batch 16，AdamW，lr0=0.0005，cosine，seed=42，freeze=None；使用 RTX 5070 Laptop GPU。',
           '数据：旧集 553 张 + 新增候选 269 张 = 822 张；train 595 / valid 141 / test 86。',
           '新集 285 张中，暂排除 14 张重复且标签不同的记录及 2 张涉及漏标的同拍摄图片；转换多边形为矩形。浏览了全部 285 张缩略标注预览，非逐像素标注认证。',
           '新数据按拍摄分钟序列和 dHash 相似关系分组；新 train 188 / valid 41 / test 40，已校验已识别相似对不跨集合。旧 train/valid/test 保持原划分。',
           '训练加载器自动移除了两个训练文件中的重复框（一张旧 QUAD 图片、一张新 freeze 图片）。',
           '', '## 同一测试集的比较', '',
           '| 测试集 | 模型 | Precision* | Recall* | mAP50 | mAP50–95 |',
           '|---|---|---:|---:|---:|---:|']
    for subset,title in [('old','旧测试集 46 张'),('freeze','新增测试集 40 张')]:
        for model in ('v4','v5'):
            m=metrics[f'{subset}_{model}']
            vals=[m[k] for k in ('metrics/precision(B)','metrics/recall(B)','metrics/mAP50(B)','metrics/mAP50-95(B)')]
            lines.append(f'| {title} | {model} | '+' | '.join(f'{v:.2%}' for v in vals)+' |')
    lines+=['','*标准评估以 conf=0.001 扫描置信度；Precision/Recall 为库按最佳 F1 位置汇总的值，不等于固定 0.399 的结果。',
            '', '## 固定 confidence=0.399', '',
            '以下 TP/FP/FN 来自混淆矩阵，匹配 IoU=0.45，max_det=300；保留多目标评估，不代表只显示一个框时的部署指标。',
            '', '| 测试集 | 模型 | TP | FP | FN | Precision | Recall |', '|---|---|---:|---:|---:|---:|---:|']
    for subset in ('old','freeze'):
        for model in ('v4','v5'):
            m=metrics[f'{subset}_{model}_conf0399']
            lines.append(f"| {subset} | {model} | {m['tp']:.0f} | {m['fp']:.0f} | {m['fn']:.0f} | {m['precision']:.2%} | {m['recall']:.2%} |")
    lines+=['','## 曲线与文件', '',
            '- results.png / results.csv：本次逐轮损失及验证指标。',
            '- training_v4_vs_v5.png：蓝色 v4、橙色 v5；两次训练/验证数据不同，只展示学习过程，不能据此公平判断优劣。',
            '- old_comparison.png / freeze_comparison.png：在相同测试图片上对比 PR、F1-confidence、Precision-confidence、Recall-confidence。',
            '- evaluation：各模型、各测试集的混淆矩阵和原始曲线及预测样例。',
            '- weights/best.pt：验证集选择的最佳权重；weights/last.pt：第 50 轮权重。',
            '- args.yaml / comparison_metrics.json：参数及完整评估数字。',
            '', '## 适用范围', '',
            '两组测试均是含打火机图片，不能据此估计没有打火机时的误报率。新增测试仍来自同一来源及少量拍摄环境，不能替代 PICO 实测。旧测试被多次比较使用，属于回归检查，不能称为首次盲测。',
            '本次不纳入 v2 的 1000 张待补标数据，避免把漏标打火机当背景。部署模型保持原版，下一步应进行 PICO A/B 实测。']
    (RUN/'TRAINING_REPORT.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    archive=RUNS/'lighter_v5_results_and_curves.zip'
    with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(RUN.rglob('*')):
            if p.is_file(): z.write(p,p.relative_to(RUN.parent).as_posix())
    with zipfile.ZipFile(archive) as z: assert z.testzip() is None
    print('50 epochs verified; report and ZIP ready:',archive)


if __name__=='__main__': main()
