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
RUN=RUNS/'lighter-yolo26n-korek-v7-50e'


def main():
    metrics=json.loads((RUN/'comparison_metrics.json').read_text())
    prep=json.loads((ROOT/'detect_merged_v7_korek/preparation.json').read_text())
    histories={}
    for version,name in [('v6','lighter-yolo26n-assgn2-v6-50e'),('v7',RUN.name)]:
        with (RUNS/name/'results.csv').open() as f: histories[version]=list(csv.DictReader(f))
    assert len(histories['v7'])==50
    columns=[k for k in histories['v7'][0] if 'loss' in k or k.startswith('metrics/')]
    fig,axes=plt.subplots(2,5,figsize=(20,8))
    for ax,col in zip(axes.flat,columns):
        for version,color in [('v6','#2563eb'),('v7','#f97316')]:
            rows=histories[version]
            ax.plot([float(r['epoch']) for r in rows],[float(r[col]) for r in rows],color=color,label=version)
        ax.set_title(col); ax.set_xlabel('Epoch'); ax.grid(alpha=.2); ax.legend()
    fig.suptitle('v6 vs v7 training histories (different datasets; descriptive only)')
    fig.tight_layout(); fig.savefig(RUN/'training_v6_vs_v7.png',dpi=150); plt.close(fig)
    lines=['# YOLO26n v7：Korek 增量训练报告','',
           '完成 50 轮，从 v6 best.pt 初始化。640×640、batch 16、AdamW、lr0=0.0005、cosine、seed 42、freeze=None。',
           '原始 200 张图片、285 个框，均可读且无空标签。全部缩略标注图已浏览，包含手持、侧面、俯视和商品图片。',
           f"保留 {prep['accepted']} 张；新增划分 {prep['new_counts']}；合并总划分 {prep['total_counts']}。",
           '排除 16 张标注存疑图，并保守隔离与既有数据感知相似的候选，共隔离 21 张。感知相似不等于确认重复。原文件及排除原因保留。',
           '按可识别拍摄系列和感知相似关系分组；这不能保证排除所有未识别的场景关联。旧测试集划分不变。未把待补标的 v2 数据加入训练。',
           '', '## 相同测试集上的标准评估', '',
           '| 测试集 | 模型 | Precision* | Recall* | mAP50 | mAP50–95 |',
           '|---|---|---:|---:|---:|---:|']
    for subset in ('old','freeze','assgn2','korek'):
        for model in ('v6','v7'):
            m=metrics[f'{subset}_{model}']
            values=[m[k] for k in ('metrics/precision(B)','metrics/recall(B)','metrics/mAP50(B)','metrics/mAP50-95(B)')]
            lines.append(f'| {subset} | {model} | '+' | '.join(f'{v:.2%}' for v in values)+' |')
    lines+=['','*以上 conf=0.001 扫描曲线，Precision/Recall 是库的最佳 F1 位置汇总，不是固定阈值值。',
            '', '## confidence=0.399', '',
            '混淆矩阵 TP/FP/FN，匹配 IoU=0.45，max_det=300。部署单框模式与此多目标评估不同。',
            '', '| 测试集 | 模型 | TP | FP | FN | Precision | Recall |', '|---|---|---:|---:|---:|---:|---:|']
    for subset in ('old','freeze','assgn2','korek'):
        for model in ('v6','v7'):
            m=metrics[f'{subset}_{model}_conf0399']
            lines.append(f"| {subset} | {model} | {m['tp']:.0f} | {m['fp']:.0f} | {m['fn']:.0f} | {m['precision']:.2%} | {m['recall']:.2%} |")
    lines+=['','## 解释限制', '',
            'Korek 测试仅 27 张，assgn2 测试仅 12 张，单个样本影响较大，不能证明全面的多角度泛化。没有纯背景负样本，不能估计 PICO 空场景误报率。',
            '旧集和 freeze 测试已反复用于模型比较，应视为回归检查；PICO 和另外独立采集数据仍需实测。',
            '训练曲线两版数据不同，不能直接公平比较；*_comparison.png 使用相同测试集比较。蓝色 v6，橙色 v7。',
            '本次只训练和评估，未替换部署权重或改变 confidence/max_det 设置。',
            '', '## 文件', '',
            'results.png/results.csv：50 轮原始曲线及数字。',
            'training_v6_vs_v7.png：训练过程两色对比。',
            'old_comparison.png / freeze_comparison.png / assgn2_comparison.png / korek_comparison.png：同集 PR、F1-confidence、P-confidence、R-confidence 对比。',
            'evaluation：各子集各模型的混淆矩阵、曲线和预测样例。',
            'weights/best.pt：验证最佳权重；weights/last.pt：第 50 轮权重。',
            'comparison_metrics.json：全部比较数据。']
    (RUN/'TRAINING_REPORT.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    (RUN/'data_preparation.json').write_text(json.dumps(prep,ensure_ascii=False,indent=2),encoding='utf-8')
    archive=RUNS/'lighter_v7_results_and_curves.zip'
    with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(RUN.rglob('*')):
            if p.is_file(): z.write(p,p.relative_to(RUN.parent).as_posix())
    with zipfile.ZipFile(archive) as z: assert z.testzip() is None
    print('Report and archive verified:',archive)


if __name__=='__main__': main()
