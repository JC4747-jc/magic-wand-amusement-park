# 漫画宠物 · 公制双目与实时 A/B/C/D

在保存的 BridgeStereoFusion 进入 ParkPet，由原发送器将已校正实拍帧发给本机 PC 服务。YOLO11s 仅用于本次本地候选，得到 37 条同会话 PICF v2 回包，首条用于预热。随后反复发送三张相邻的公开实拍帧，持续跟踪同一只猫。原接收器要求三次合格的双目观测，合成点捏才选中目标。桌面提供相机与头部位姿及测试手势。[运行轨迹](captures/trace.txt) · [输入与校验](verification.json)

| 源帧 | 原空间匹配器 | 左目 LiDAR | 一致采样 |
|---|---:|---:|---:|
| 180 | 2.650 m | 2.772 m | 9/9 |
| 181 | 2.633 m | 2.758 m | 9/9 |
| 182 | 2.611 m | 2.661 m | 7/9 |

Total-Recon 的左目原始深度以浮点米值解码，在 12 个时点使用左右相机配准与独立图像特征核对投影。逐帧中位误差的中位数为 5.33 px，基线为 0.33678 m。Unity 夹具使用 640×480 校正图及 425 px 焦距。表中距离对应猫检测框中心附近，猫头锚点仍待核实。[原论文补充材料](https://openaccess.thecvf.com/content/ICCV2023/supplemental/Song_Total-Recon_Deformable_Scene_ICCV_2023_supplemental.pdf)

共享手势识别到 A 描边、B 打开梦境泡、C 横挥戳泡、D 好感加一，各一次。暂停回包后角色隐藏，动作被拦截，恢复后原 ID 在第三条新双目观测重新可用，Reset 清除了描边和好感。四张场景图已查看，镜头仅在取证时对准角色并隐藏检测菜单。[描边](captures/Appearance.png) · [梦境泡](captures/Agency.png) · [戳泡后](captures/Rule.png) · [好感](captures/Deconstruction.png)

独立弗赖堡样本共 36 帧，各模型分别比对已复核的目标框。按生产空间门槛置信度 ≥0.50，两模型都是猫 7/18、狗 18/18，猫误报为狗由 YOLOv8n 的 6/18 增到 YOLO11s 的 8/18。默认 PC 配置及 Android 运行输入保持原版本。原 Bridge 的猫狗识别与位姿门控复测通过。PICO 透视、实手输入和真实头部接触仍待验。[独立录像与使用条款](https://lmb.informatik.uni-freiburg.de/resources/datasets/StereoEgomotion/) · [候选权重上游](https://github.com/ultralytics/assets/releases/tag/v8.3.0)

头两次尝试在 A 触发后停止发送手样本，原有离手超时取消了动画。继续发送静止手样本后，A 生效。第三次取证时有一帧仍在推理，截图使回包错过 0.3 s 的空间期限。最终将截图安排在无待收回包时。稳定版本最低采样可用内存 5.784 GiB，未触发资源保护。隔离工程 1,840 个原输入和本机主索引 SHA256 已复核。

复跑时按验证清单准备本地 `.codex-tmp/pet-live-metric-fixture`。入口 `input.json` 提供 `intrinsics`、`baseline`、`sourceFrames` 和 `lidarM`。每帧用五位源编号建子目录，放入 `sender.png`、`left.gray` 和 `right.gray`。彩色 PNG 采用发送器翻转前的相机行序。用 PC 服务的 `--profiles` 加载单独的候选配置，在本机 5005 运行后执行有界启动器的 `BridgePetMetricPicf`。源实拍和候选权重留在本地。
