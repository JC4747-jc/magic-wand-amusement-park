# 火机部署权重：录制图像 TCP 回放与划分审核

当前部署权重 best_yolo26n_assgn2_v6_50e.pt，SHA256 为 999bca4f5bf96aeed3ac0f1047e31530e4eb4584873c85dd5bd00031fc71fb3a。通过实际 tcp_server.py 加载权重，以 conf 0.399、imgsz 640、IoU 0.7、max_det 1 在 CPU 上推理，输入本地 Find lighter/test/images 的 14 张 JPEG；混合 PICF v1/v2 lighter 请求，并在新连接从 frame 1 重新开始，总计 15 次请求。没有使用推理替身。

14 张图片均返回一个 Lighter 检测，帧号/时间戳/图片尺寸/场景和有限检测坐标检查通过；与原标注多边形外接框的最佳 IoU 均超过 0.5。标签未全部复核，图片不是新采集，不能将这些数字写成独立准确率或真机验收。原始回复与文件摘要见 result.json；运行脚本为 Tools/replay_lighter_recordings.py。

## 发现的划分问题

对本地 train 的 97 张、valid 的 26 张和 test 的 14 张图片检查像素 SHA，测试图没有与 train/valid 完全相同的像素文件。9×8 灰度 dHash 检查却标记了两个跨划分近似组：

- test pico_frame_0056 与 train pico_frame_0057、0058：Hamming 距离 1。
- test pico_frame_0093 与 train pico_frame_0092：Hamming 距离 4。

逐张查看了 0056/0057 和 0093/0092：均为相同房间、火机和相邻动作视角，不能把这些帧当成独立拍摄证据。另查看了 0119 的远景，画面含多个类似火机的小物体，原始标注完整性仍应单独检查。dHash 只是筛查，不能自动证明重复文件或 v6 模型具体训练成员关系。

v6 的准备脚本引用 detect_merged_v5_freeze、audit_assgn2 等目录；当前本地缺少这些完整训练准备记录，因此无法追溯全部 v6 训练成员。137 张旧导出图统一列入 TrainingData/lighter-legacy-split-policy-v1.json 的保守历史开发组；暂不作为新独立测试集。保留原文件和历史划分，后续获得拍摄会话、物体身份与背景信息后再审查分组；新模型须用独立场次、不同目标/背景/光照的已审核数据验收。

## 资源及边界

最终运行约 11.20 秒（含文件审核、模型启动和重连），最大采样父进程与服务后代总 RSS 463.38 MiB，最低采样可用 RAM 7.61 GiB。逐请求/启动轮询采样，不代表连续峰值。RAM 下限 3 GiB、进程树 RSS 上限 3 GiB、盘余量 20 GiB、增长 64 MiB、输出 8 MiB、时长 180 秒；CPU 线程环境限制为 1。服务只绑定 localhost 5005，启动前要求端口空闲，结束后清理所属进程和端口；没有接管其他服务。

首次驱动误用当前服务不支持的 --port 参数，已改为检查既有 5005 端口；资源统计随后修正为包含 Windows 虚拟环境启动器的后代进程。本文及 result.json 仅引用修正后的最终运行。

没有修改部署配置、权重或训练数据，没有下载、训练或重新打包 APK。当前 PICO 未连接；此次回放不验证实时相机、双目定位、实例跟踪连续性、真实手势或动画闭环。所有线路的真实 YOLO/PICO 与美术完善继续按 Goal 执行。
