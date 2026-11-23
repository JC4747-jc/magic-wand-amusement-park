# 虫类家居增量 · CUDA 微调与回归

从上轮双类权重 `9426311…9b89` 继续训练冻结的 168 张训练图和 33 张开发验证图。CUDA 单进程实际完成 20 轮、1,680 批和 1,673 次优化器更新，参数发生变化。用 AdamW、批大小 2、640 px 输入和 .00035 初始学习率。Ultralytics 按开发组原生验证指标选出 `best.pt`，开发 mAP50 为 .3770、mAP50–95 为 .1899。训练期间读取独立测试图 0 张，完成后冻结最优权重哈希再回放。配对开发组在 .50 置信度、.50 同类 IoU 时，命中数 8→6、未匹配框 7→4。[冻结方案](../../TrainingData/bugs-house-finetune-plan-20261123.json) · [有效输入](training-input-records.json) · [实际训练记录](training.json) · [逐轮日志](epochs.csv)

最佳权重经原 PICF 服务取得 140 条 CPU 回包。前一权重在相同帧上的原始回包用同一计分程序复算。表中每格依次为命中数和未匹配框数，门槛为置信度 .50、同类且 IoU≥.50。[回包](../public-statue-bug-house-finetune-20261123/raw-picf.json) · [统计与配准判定](../public-statue-bug-house-finetune-20261123/result.json) · [实拍叠框](../public-statue-bug-house-finetune-20261123/motion-finetune-photo-review.jpg)

| 样本组 | 帧数 | 上一权重 | 新权重 |
| --- | ---: | ---: | ---: |
| 独立实拍照片 | 17 | 3 / 4 | 2 / 0 |
| 专用模型回归照片 | 21 | 5 / 7 | 4 / 1 |
| 原回归照片 | 15 | 3 / 0 | 2 / 1 |
| 跳蛛抬腿连续实拍 | 13 | 3 / 0 | 0 / 0 |
| DASH 仿生机器人对照 | 9 | 0 / 0 | 0 / 0 |

本次新权重在原回归组首度命中一个宽度不足 64 px 的实例。独立组的两只小目标仍未命中，蟑螂正常和慢放、跳蛛腾空以及半尺度录像也没有新增命中。把已保存回包的计分门槛调到 .399，独立组旧、新命中均为 3，未匹配框从 6 降到 0；跳蛛抬腿仍从 6 帧降到 0。该对照用于分析误报与漏检，不用于反选权重或生产门槛。DASH 上两个双类权重均无误框，旧专用 v16 的四帧同 ID 误锁仍留作单列回归。[固定阈值对照](../public-statue-bug-house-finetune-20261123/thresholds-and-review.json)

训练约 141 秒，最低采样 RAM 5.99 GiB，进程最高采样 RSS 2,576.99 MiB。D 盘本轮训练目录最高采样约 63 MiB。PC 复测最低采样 RAM 7.70 GiB，两次资源保护均未触发。最佳和末轮权重留存在本机。生产默认虫类权重仍为空，Android、PICO 及现场 A/B/C/D 尚待验收。[训练资源](resources.json) · [回放资源](../public-statue-bug-house-finetune-20261123/resources.json)

最优候选 SHA256 `a44be972be92b58c74ed0b44b3e9ef8d86d37cac5247a87d1bf03635410f5344`，末轮 `83b59fd11384fea45fb1a6814f309032b1ab08f7322e6e6a3456154eb0a0b037`，训练记录 `ac10ba63106b9166a718322da4fd08da5d72dbc417bfcb45fc0fe0fab6165569`，回放评分 `f75c8593abb117adfbf6aba8272c5e72c1d51f357241c70c967b038b022ff900`。
