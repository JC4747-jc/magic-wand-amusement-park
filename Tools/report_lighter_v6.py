"""Package assgn2 training results and same-holdout comparisons."""
import csv
import json
import zipfile
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

ROOT=Path(__file__).resolve().parents[1]/'Find lighter'
RUNS=ROOT/'runs/detect'
RUN=RUNS/'lighter-yolo26n-assgn2-v6-50e'


def main():
    metrics=json.loads((RUN/'comparison_metrics.json').read_text())
    prep=json.loads((ROOT/'detect_merged_v6_assgn2/preparation.json').read_text())
    histories={}
    for version,name in [('v5','lighter-yolo26n-freeze-v5-50e'),('v6',RUN.name)]:
        with (RUNS/name/'results.csv').open() as f: histories[version]=list(csv.DictReader(f))
    assert len(histories['v6'])==50
    columns=[k for k in histories['v6'][0] if 'loss' in k or k.startswith('metrics/')]
    fig,axes=plt.subplots(2,5,figsize=(20,8))
    for ax,col in zip(axes.flat,columns):
        for version,color in [('v5','#2563eb'),('v6','#f97316')]:
            rows=histories[version]
            ax.plot([float(r['epoch']) for r in rows],[float(r[col]) for r in rows],color=color,label=version)
        ax.set_title(col); ax.set_xlabel('Epoch'); ax.grid(alpha=.2); ax.legend()
    fig.suptitle('v5 vs v6 training histories (different datasets; descriptive only)')
    fig.tight_layout(); fig.savefig(RUN/'training_v5_vs_v6.png',dpi=150); plt.close(fig)
    lines=['# YOLO26n v6：assgn2 增量训练报告','',
           '完成 50 轮训练，使用 v5 best.pt 初始化。640×640、batch 16、AdamW、lr0=0.0005、cosine、seed 42，freeze=None。',
           '100 张新图片均可读取，单类 lighters 已统一为 lighter；原标签 161 个目标，无空标签。浏览全部 100 张缩略标注图：有侧面、斜视、俯视、手持图片，但正面商品图仍占较多，不是专门的全角度数据集。',
           '清理后接受 82 张：train 58 / valid 12 / test 12。暂排除 8 张标注疑问图、9 张与旧数据感知相似的候选、1 张像素重复副本。感知相似不是确认重复，采取保守隔离。',
           '原图、隔离图片和排除原因均保留。与待补标 v2 数据另有相似候选，未来合并时仍需去重。',
           '新数据按检测到的相似关系分组划分，已验证所识别相似对不跨集合。旧数据划分保持；总 train 653 / valid 153 / test 98，共 904 张。',
           '', '## 相同测试集上的标准评估', '',
           '| 测试集 | 模型 | Precision* | Recall* | mAP50 | mAP50–95 |',
           '|---|---|---:|---:|---:|---:|']
    for subset in ('old','freeze','assgn2'):
        for model in ('v5','v6'):
            m=metrics[f'{subset}_{model}']
            values=[m[k] for k in ('metrics/precision(B)','metrics/recall(B)','metrics/mAP50(B)','metrics/mAP50-95(B)')]
            lines.append(f'| {subset} | {model} | '+' | '.join(f'{v:.2%}' for v in values)+' |')
    lines+=['','*以上 conf=0.001 扫描曲线，Precision/Recall 是库的最佳 F1 位置汇总，不是固定阈值值。',
            '', '## confidence=0.399', '',
            '混淆矩阵 TP/FP/FN，匹配 IoU=0.45，max_det=300。部署单框模式与此多目标评估不同。',
            '', '| 测试集 | 模型 | TP | FP | FN | Precision | Recall |', '|---|---|---:|---:|---:|---:|---:|']
    for subset in ('old','freeze','assgn2'):
        for model in ('v5','v6'):
            m=metrics[f'{subset}_{model}_conf0399']
            lines.append(f"| {subset} | {model} | {m['tp']:.0f} | {m['fp']:.0f} | {m['fn']:.0f} | {m['precision']:.2%} | {m['recall']:.2%} |")
    lines+=['','## 解释限制', '',
            'assgn2 测试仅 12 张，单个样本影响较大，不能证明全面的多角度泛化。没有纯背景负样本，不能估计 PICO 空场景误报率。',
            '旧集和 freeze 测试已反复用于模型比较，应视为回归检查；PICO 和另外独立采集数据仍需实测。',
            '训练曲线两版数据不同，不能直接公平比较；*_comparison.png 使用相同测试集比较。蓝色 v5，橙色 v6。',
            '本次只训练和评估，未替换部署权重或改变 confidence/max_det 设置。',
            '', '## 文件', '',
            'results.png/results.csv：50 轮原始曲线及数字。',
            'training_v5_vs_v6.png：训练过程两色对比。',
            'old_comparison.png / freeze_comparison.png / assgn2_comparison.png：同集 PR、F1-confidence、P-confidence、R-confidence 对比。',
            'evaluation：各子集各模型的混淆矩阵、曲线和预测样例。',
            'weights/best.pt：验证最佳权重；weights/last.pt：第 50 轮权重。',
            'comparison_metrics.json：全部比较数据。']
    (RUN/'TRAINING_REPORT.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    (RUN/'data_preparation.json').write_text(json.dumps(prep,ensure_ascii=False,indent=2),encoding='utf-8')
    archive=RUNS/'lighter_v6_results_and_curves.zip'
    with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(RUN.rglob('*')):
            if p.is_file(): z.write(p,p.relative_to(RUN.parent).as_posix())
    with zipfile.ZipFile(archive) as z: assert z.testzip() is None
    print('Report and archive verified:',archive)


if __name__=='__main__': main()
