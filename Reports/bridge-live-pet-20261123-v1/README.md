# 漫画宠物 · Bridge 实时识别与位姿门控

2026-11-23。在保存的 BridgeStereoFusion 中点选 Pet，进入 ParkPet。原 PicoYoloFrameSender 对公开实拍左图编码，经原 TCP 发送 PICF v2，由已发布 PC 服务和 YOLOv8n 实时推理。每条回包核对帧号、时间戳、场景、尺寸和权重哈希。[源码与逐项复核](verification.json) · [运行轨迹](captures/trace.txt)

首帧用于加载模型，随后 6 张猫图和 6 张狗图全部正确分类，同一段内保持各自的实例 ID。与既有人工框对比，猫的 IoU 为 0.880–0.949，狗为 0.903–0.970。查看了两段各自的首尾叠框。输入图像来自带左右通道的研究录像，左图缩至 640×360 并上下各补 60 像素，原始左右帧均记录 SHA256。[逐帧 IoU](optical-boxes.json) · [研究录像和使用条款](https://lmb.informatik.uni-freiburg.de/resources/datasets/StereoEgomotion/)

猫段注入采集位姿不可用，狗段注入头部跟踪丢失。原接收器在两段均继续发布识别结果，且未生成或选中对应空间实例。直接请求 A 描边、B 梦境、C 戳泡和 D 好感时，四个动作均被拦截。实拍画面没有已核实的公制标定，空间选中、头部触碰、实手输入及 PICO 现场施法待验。

Unity 6000.0.33f1 单工作进程通过。最低采样可用内存 5.851 GiB，Unity 与服务的最高采样常驻内存分别为 1,643.32 MiB 和 766.1 MiB，候选盘采样净增至多 1 MiB。主索引与隔离工程原 1,840 个输入文件恢复核对通过。变更属于 Editor 验证入口和有界启动器，Android 生产输入不变。

复跑时按验证清单的 `frames` 顺序准备本地 PNG，放到 `.codex-tmp/pet-live-recognition-fixture/01.png` 至 `12.png`。PNG 在送入原相机发送器之前需要按相机原始行序保存。启动本机 5005 的已发布 PC 服务，使用 `Tools/run_room_magic_review_bounded.ps1` 的 `BridgePetLivePicf` 检查。研究原图及叠框保存在本地。
