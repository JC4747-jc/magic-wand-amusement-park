# 出租屋 · Unity 实时 PICF 与短时失跟恢复

保存的 BridgeStereoFusion 经房间确认进入 ParkRoom。使用与已校准 Playroom 灰度图对对应的公开彩色帧，由保存场景的 `PicoYoloFrameSender` 编码 JPEG，经原 TCP 组件发送 PICF v2，再由本机 Python YOLOv8n 服务实时推理。Editor 提供校准夹具、位姿和合成手势，PICO 摄像头及真手验收仍待测。

- 收到 72 条实时回包，每条核对帧号、发送时间、图像尺寸、房间场景和权重哈希。首条用于加载模型。
- 同一椅子 ID 支撑了 4 个房间效果。完成墙面定位后，湖景展开，水母灯、染色与糖果雨共存。
- 停发约 720 ms 后，Unity 清理原特效与墙面定位。恢复后原椅子 ID 在第三条新回包重新跟踪；B 仍要求重新定位墙面。
- 原 PC 追踪保留为 0.6 秒，早于 Unity 保存场景常用的 0.65 秒失跟判定。PC 为显式非火机场景保留同类重叠框的短期匹配历史 1.5 秒。Unity 失跟和定位门槛、火机路径、发现模式各沿用原有规则。54 项 Python 测试通过，覆盖短中断后同 ID、长中断后分配新 ID、原路径及重连。

[实时轨迹](captures/trace.txt) · [四效果同屏](captures/recognized-room-effects.png) · [源码和资源核对](verification.json)

本机复跑使用已校准 Playroom 左右灰度图及对应的 `input.json`，放到工程的 `.codex-tmp/park-room-chain-fixture`；同一左目彩色 PNG 放到 `.codex-tmp/bridge-room-live-picf.png`。校验夹具哈希见验证记录。先按仓库 PC 检测说明启动本机 5005，再用 Unity 6000.0.33f1 执行 `CalibratedRoomMagicChainChecks.RunLiveFromBridge`。本机也可使用 `Tools/run_room_magic_review_bounded.ps1` 的 `BridgeRoomLivePicf` 检查。公开输入来自 [Middlebury Stereo 2014](https://vision.middlebury.edu/stereo/data/scenes2014/)。
