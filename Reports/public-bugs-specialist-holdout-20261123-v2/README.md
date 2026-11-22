# 虫类 · 原生蜘蛛类别与蟑螂专用候选

复用已有的 YOLOv8n-OIV7 和 YOLOv8m-OIV7 权重，核对两者的 601 类中原生编号 482 为 Spider。在独立配置中只开放该类。蟑螂 v16 使用原 .65 门槛，仍只开放 Cockroach。使用 `90928ae` 的 PC 识别模块，对照 `mr/jc@96d581f` 核对运行文件一致。[Ultralytics Open Images 模型](https://docs.ultralytics.com/datasets/detect/open-images-v7/) · [权重发布](https://github.com/ultralytics/assets/releases/tag/v8.3.0)

## 新留出照片

21 张新照片包括 12 张蜘蛛、5 张蟑螂、4 张盲蛛或衣鱼。均为 iNaturalist CC0 实拍，每张来自不同的摄影账号。采集前核对 39 个既有登记表和 256 张原图文件。每个模型使用相同整帧及四个 384×384 窗口，共 318 条 PICF v2 回包，回包逐条验证帧号、时间、图像尺寸、场景、权重哈希和类别。蜘蛛回包阈值 .399，计分 .50；蟑螂回包与计分均为 .65。同类 IoU 至少 .50 计命中。[冻结样本、阈值与来源](frozen-input.json) · [逐图结果](result.json)

| 候选 | 整帧 TP / FP / FN | 四窗 TP / FP / FN | CPU 整帧 / 四窗累计中位耗时 |
| --- | ---: | ---: | ---: |
| YOLOv8n-OIV7 Spider | 0 / 0 / 12 | 0 / 0 / 12 | 46.26 / 231.99 ms |
| YOLOv8m-OIV7 Spider | 3 / 0 / 9 | 1 / 0 / 11 | 207.48 / 1,098.27 ms |
| v16 Cockroach | 1 / 2 / 4 | 2 / 2 / 3 | 200.50 / 1,047.04 ms |

已目检框和错类。YOLOv8m 命中的蜘蛛整帧 IoU 为 .955–.990，叶片上一只宽约 65 px 的蜘蛛漏检。v16 整帧将两只 *Zygiella x-notata* 认成蟑螂，置信度分别为 .873 和 .824。四窗保留一处蜘蛛错类，另对一只正确检出的蟑螂新增重叠误框。4 张盲蛛或衣鱼无计分框。各份权重独立回放，并行双模型、现场跟踪与 PICO 手势皆待验。[目检对照](photo-review.jpg) · [n 原始回包](oiv7_n-wire.jsonl) · [m 原始回包](oiv7_m-wire.jsonl) · [v16 原始回包](v16-wire.jsonl)

## 样本库存与执行记录

既有 118 张训练图含 44 个蜘蛛框和 41 个蟑螂框，其中每类只有 3 个框的宽度小于 64 px。Open Images 官方验证标注中有 62 张含 Spider 的图，剔除已在项目虫类登记表出现的 21 张和 1 张群体或图示后，剩余 40 张可人工筛查。40 张的官方框均超过原图宽度十分之一，且无官方遮挡标记，适合补一般外观，现场小目标和遮挡仍须另补。[Open Images 官方说明与标注](https://storage.googleapis.com/openimages/web/download_v7.html) · [库存与候选来源](openimages-spider-inventory.json)

采集时一次代理断连，核对已下载原图和登记表哈希后，仅恢复未完成查询，累计传输 16.73 MiB。首轮回放在发包前发现共用函数读取旧帧目录，改为传入新帧路径后通过，冻结输入未变。完整回放约 65 秒，最低采样可用 RAM 7.44 GiB，进程树 RSS 最高采样 1,095 MiB，保护未触发。[资源记录](resources.json)

两条分支均在真实识别与错类上未达验收。维持正式 `bugs.weights` 为空。保留本轮 21 张作跨类回归，下一轮训练输入优先补真实环境小蜘蛛、复杂表面和容易被误认成蟑螂的蜘蛛，独立照片另行冻结。PICO 可用后从生产入口验真实识别、目标跟随、A/B/C/D 和延迟。
