# 盆栽与点云花 · 实时 PICF 验证

从保存的 BridgeStereoFusion 点击 Plant 进入 ParkPlant。原 Unity 发送器编码既有实拍彩色图，经原 TCP 组件送到运行中的 YOLOv8n。相配的左右灰度图用于原双目匹配，Editor 提供校准参数、位姿和合成手势。收到 109 条实时 PICF v2 回包，每条核对帧号、时间戳、尺寸、场景和权重 SHA256。首条用于模型预热。

照片中有两个盆栽。右侧盆栽的双目匹配深度约 2.203 米，左侧约 1.860 米。选中右侧盆栽后，默认 Plant 和 Particle flower 都走完 A/B/C/D。B 的合成语音输入带动嘴部动画，D 清除默认盆栽效果并保留配准。点云花保留 2,048 个点，指尖局部拨动影响 315 点，停手后原位恢复，范围外动作无位移。切回默认菜单仍指向同一个检测实例。停发后目标失跟，并清除语音、朝向和尺寸确认。[实时轨迹](captures/trace.txt) · [点云花拨动](captures/point-flower-brush-active.png) · [点云花变形](captures/recognized-plant-point-flower.png)

首次实时试验在点云指尖检查期间做两次 PNG 压缩，阻塞 Unity 的实时采样。第二轮暂存截图像素，在回放结束后压缩，固定流程通过。随后串行复跑出租屋实时入口，确认四个效果和失跟恢复仍通过。没有改动 Android 运行时源文件。现场相机采集、真实语音与手势及 PICO 仍待验。[验证和资源](verification.json)

复跑入口是 `CalibratedRoomMagicChainChecks.RunLivePlantFromBridge`，或在有界启动器使用 `BridgePlantLivePicf`。先启动仓库 PC 服务。彩色输入位于工程 `.codex-tmp/bridge-plant-live-picf.png`，`input.json` 及左右灰度图放在 `.codex-tmp/park-plant-chain-fixture`；哈希见验证记录。
