# 青铜兽材质复核在新回包上的表现

沿用冻结的 CLIP ViT-B/32 权重和青铜对陶质提示，读取同一项目 YOLOE 候选已上传的两组原 PICF 回包。16 张克利夫兰馆藏图和 44 张户外青铜马画面共提供 50 个 BronzeBeast 框。按原回包坐标裁切，门槛为青铜平均余弦分减去陶质分 ≥0。另外对照加入动物对人物分数的同符号要求。两种规则均保持检测类别。未用本轮留出结果调整提示、裁框、阈值或权重。[冻结规则](protocol.json) · [50 个裁切得分](proposal-scores.json) · [馆藏原回包](../public-statue-native-holdout-cma-20261123/picf-visual-replies.json) · [户外原回包](../public-statue-bronze-horse-20261123/picf-replies.json)

| 组别 | 原回包命中 | 材质过滤后命中 | 未匹配框变化 |
| --- | ---: | ---: | ---: |
| 馆藏，置信度 .50 | 4/4 件青铜动物 | 4/4 | 5 → 1 |
| 户外，置信度 .50 | 37/44 张 | 37/44 | 0 → 0 |
| 户外，置信度 .399 | 41/44 张 | 41/44 | 0 → 0 |

已目检。馆藏过滤掉石质人物胸像、陶质人物和两件陶质动物的误框，余下未匹配框是同一青铜鹿的第二框。附加动物形态比较与单独材质规则的保留结果一致。重新汇总时核对了类别，石质人物误框与人物评审框的 IoU 达标仍计为误报，本次修正复用同一批 CLIP 得分。[结果](result.json) · [重新评分记录](rescoring.json)

户外 44 张来自同一件青铜马。对照校方照片确认该物件为圣克拉拉大学 Bronco 雕塑。校方只给出高于 10 英尺的整体描述，论文给出裁剪后扫描高度 3.2 米，缺少能把该范围对应到当前 COLMAP 模型的物理基线或标定点，前一轮相对视差仍未转换为米制距离。[校方照片](https://www.scu.edu/careercenter/) · [校方公布资料](https://www.scu.edu/news-and-events/press-releases/2014/october-2014/santa-clara-university-unveils-new-bronco-statue-symbolizing-new-era-at-scu.html) · [数据集论文](https://storage.googleapis.com/t2-downloads/paper/tanks-and-temples.pdf)

原沃尔特斯留出仍有高分陶质误框。材质过滤保持离线候选，默认权重为空。最低可用 RAM 采样 6.92 GiB，进程树最高 RSS 采样约 1,549 MiB，保护未触发。原片和核对图留在本机。[既有沃尔特斯反例](../statue-walters-material-20261123/README.md) · [资源采样](resources.json)

得分 SHA256 `186dc61e1caf5b887c848c00f309adcd58ac80938f309c89db9aab8d77fe0ea3`，分类修正后的结果 SHA256 `abc7d1b35b48b45893f9c130d32f226b7e7741ee5247f8d83d166f63575de89c`。
