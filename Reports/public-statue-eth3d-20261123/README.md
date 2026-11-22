# 石像多视角实拍离线对照

从 ETH3D DSLR Statue 的同一件石像取 5 个拍摄视角，再用相机标定制成 4 对 640×480 校正图。评审框包含人物和动物的雕塑组合，排除长方形底座。原图、校正图和叠框仅用于本机检查。[ETH3D 数据集](https://www.eth3d.net/datasets) · [数据集论文](https://www.eth3d.net/data/schoeps2017cvpr.pdf)

PC 回放走 `90928aef682fa714e7baacf8b8d2ca2980681027` 的原 PICF 收发路径。13 张冻结输入各跑三次模型对照，共 39 条原始回包。回包阈值为 .399，命中要求同类、置信度和 IoU 均达 .50。原生 Open Images 模型只在测试配置里把 Sculpture 映射为 Statue。帧号、时戳、输入和权重哈希均已核对。[冻结输入](frozen-input.json) · [视觉提示回包](picf-replies.json) · [v8n 回包](native-n-replies.json) · [v8m 回包](native-m-replies.json)

| 模型 | 直立图命中 | 校正图命中 | 左右均命中的配对 |
| --- | ---: | ---: | ---: |
| YOLOE 视觉提示 | 1/5 | 0/8 | 0/4 |
| YOLOv8n-OIV7 | 4/5 | 1/8 | 0/4 |
| YOLOv8m-OIV7 | 5/5 | 5/8 | 2/4 |

YOLOv8m 两眼均命中 494–495 与 496–497。已目检全部叠框。495–496 的校正视野里雕像明显偏小，两眼均漏检。四对图的校正纵向残差第 95 百分位为 0.065–0.160 px。雕像区域参考表面深度中位数为 1.89–2.14 m，落在项目保存的 0.2–3 m 范围内。校正采用 0.56–0.64 m 的 DSLR 基线，OpenCV SGBM 是独立的像素对照。真实同步双眼定位、异类负例、连续目标跟踪、青铜兽及真机手势仍待验证。生产石像权重保持空值。

两次有界推理依次执行，最低可用 RAM 采样 7.54 GiB，任务最高 RSS 采样约 1,021 MiB，保护未触发。[几何及提示模型结果](result.json) · [原生模型结果](native-result.json) · [提示模型资源采样](resources.json) · [原生模型资源采样](native-resources.json)

冻结输入 SHA256 为 `41fcb24d55540b3923c89b22fbe0673f70c80418866d0528cb96c4f67c1c7822`。视觉提示结果 SHA256 为 `1d8ed75e1ede6498af2a7c8bf8bec91b4f839e70b0f0bd8926a496424192afac`，原生模型结果 SHA256 为 `7c30164af970ac16ca2cda733b302ce8055fe0309a31b365b58b26f6aa808c6a`。
