# HCI 像素虫：蟑螂/蜘蛛候选训练，测试未通过

本次实际执行了 YOLO26n 的 Cockroach / Spider 两类 GPU 微调，完成 20 个 epoch、460 个批次、454 次优化器更新并保存 best.pt / last.pt。**模型未通过冻结测试：两类 mAP50 和 mAP50–95 均为 0，conf=0.25 时漏掉全部 7 个测试目标。没有部署、没有发布候选权重，正式 bugs 配置仍为空。** 脚本退出成功仅证明训练/评估完成，不代表识别质量达标。

## 数据与划分

逐张检查 20 张 Spider 原图并复核叠框，修正可见腿部范围、保留双实例、排除蛛网/影子；猎物身份不明和严重失焦的两张被隔离，不能当作负样本。继承 14 张已逐图审查、SHA 绑定的 Cockroach 候选；另逐张检查 6 张无可见虫体的背景图，它们只对这两个虫类作负样本，不能用作 13 类项目的空标签。

共冻结 38 张：train 23（9 蟑螂、12 蜘蛛、2 背景），valid 7（2/3/2），test 8（3/3/2）；test 中一张含两只蜘蛛，因此共有 7 个目标。按原全局像素/感知分组，再合并 Flickr 作者或 iNaturalist 观察者（跨日期）确保微调划分不共享同组。审核、分组、来源、作者、许可与文件摘要保存在 TrainingData/bugs-bootstrap-*.json 和 image-credits.json。

公开图可能与预训练重叠；蟑螂图也用于此前另一失败实验，**这是本次微调的分组留出诊断，不是新的项目级独立测试或 PICO 域验收**。原图不改动。边框/标签是本项目审核增量，图片的原许可和署名保留；本报告中的图像来源见 image-credits.json。数据格式参考 [Open Images 官方下载与格式](https://storage.googleapis.com/openimages/web/download_v7.html)。

## 训练、失败分析与复用检查

复用已有 yolo26n.pt，没有额外模型下载。固定 Ultralytics 8.4.212、Torch 2.4.0+cu124、seed=42、imgsz=416、batch=1、workers=0、cache=False、20 epoch、AdamW lr0=0.001、mosaic=0；按验证 fitness 选择 best checkpoint，随后一次原生 test 评估及固定 conf=0.25 的逐图错误记录，不用测试结果调阈值或重新训练本划分。

Windows Torch 2.15.0+cpu 重新加载的类别名和参数有限性检查通过；仅对 7 张 valid 图再做诊断，不重用 test。即使降到诊断阈值 0.001，其最高验证置信度也仅约 0.0484；可见把蜘蛛判为蟑螂、框落在虫体之外、猫背景出现低分候选。见 validation-failures.png。原生 test 在 0.001 下 mAP 仍为 0，问题不只是部署阈值过高。

训练分类损失下降但验证损失波动，数据量/视角覆盖不足与未有效收敛是待验证因素，不能凭一次实验确定单一根因。后续扩大并审核训练/验证数据，覆盖小虫、远景、遮挡、相似昆虫与 PICO 光照；只用训练/验证部分比较学习方案。正式验收另需不同拍摄场次/背景/物体的 PICO 数据。不得把当前模型加入正式线路，也不盲目在相同测试集上调整 epoch 或阈值。

## 内存、显存与磁盘

首轮在第 1 epoch 后采样系统可用内存 2.805 GiB，触发原 3 GiB 下限退出，未读 test。随后等待空闲回收，增加 CUDA 导入前至少 6 GiB 可用 RAM；对本任务的 systemd 临时 scope 设置 MemoryHigh=2560M、MemoryMax=3G、MemorySwapMax=0、CPUQuota=200%，关闭训练 DataLoader pin_memory，并回收空闲原生分配。数据、模型和学习参数不变，未降低守卫，未改全局 WSL 配置或重启。

完成轮最低采样可用 RAM 3.661 GiB，最大采样进程 RSS 2208 MiB；Torch GPU 峰值分配约 169 MiB。任务输出瞬时最大采样约 64.55 MiB、最终约 25.20 MiB，低于 128 MiB 预算，保留两个权重而非每 epoch 检查点。总训练回调时段约 79 秒；数据是回调/五秒采样及分配器峰值，不是连续全设备峰值。日志采用 1 MiB 前缀/64 KiB 尾部、总量 8 MiB、只清理已记录成功日志的策略；本轮写入约 110 KiB，旧失败证据保留。C/D/WSL 空间下限分别为 20/25/8 GiB，GPU 空闲下限 1 GiB，900 秒内部及 960 秒外部时限继续生效。

临时进程组内存控制依据 [systemd resource-control](https://www.freedesktop.org/software/systemd/man/latest/systemd.resource-control.html)，启动方式见 [systemd-run](https://www.freedesktop.org/software/systemd/man/latest/systemd-run.html)。共享 F2G 的五个关键依赖文件及正式服务配置摘要不变。Windows 复载最低采样 RAM 7.702 GiB、RSS 377.45 MiB；训练、复载及渲染串行，无 Unity 构建并发。

## 复核入口与交付

Tools/prepare_bugs_bootstrap.py 保存逐图决策并拒绝覆盖冻结文件；Tools/train_bugs_bootstrap_bounded.ps1 使用独立 WSL 环境和原守卫；Tools/train_bugs_bootstrap.py 实际训练与评估；Tools/validate_bugs_bootstrap.py 复载，Tools/render_bugs_bootstrap_diagnostics.py 仅渲染既有结果。训练实现依据 [Ultralytics 官方 Train 文档](https://docs.ultralytics.com/modes/train/)。复现须具备原项目的数据缓存与独立环境，不是仅克隆这次报告即可训练；原图可按清单链接恢复并逐文件核对 SHA，QA/冻结文件也须通过摘要检查。

GitHub 交付审核清单、工具、结果和图表。未达标 best.pt / last.pt 仅保存在本地 .codex-tmp/yolo-training/artifacts/bugs-bootstrap-20261116-b，文件大小/摘要见 manifest，不作为验证包上传。本轮未改 Unity/Android 运行时代码、未更新 APK，PICO 未连接。真实 YOLO、全场景动画/模型、美术和设备验收 Goal 继续 active。
