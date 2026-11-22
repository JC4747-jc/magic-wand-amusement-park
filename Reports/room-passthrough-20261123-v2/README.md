# 出租屋 · 实景透视

选中真实出租屋后，视野直接透出头显摄像头的房间。微缩墙面、地板、椅子、圆桌和储物柜已从实景渲染路径移除。B 魔法仍把掌心的蓝金粒子送到两点标记的墙面，展开带颗粒边缘的原创雪山天鹅湖。水母灯由 C 独立唤起。透视染色、糖果雨、传送门和降雪仍由原菜单与手势控制。

[无特效时的透明层](captures/passthrough-idle.png) · [湖景和水母灯](captures/recognized-room-effects.png) · [墙面湖景特写](captures/swan-lake-wall.png) · [传送门](../room-passthrough-20261123-v3/captures/portal-perspective.png) · [降雪](../room-passthrough-20261123-v3/captures/snow-room-perspective.png)

## Unity 验证

从保存的 BridgeStereoFusion 进入 ParkRoom，经原 TCP 回放已记录的 YOLO 回包，并注入合成姿态和手势。默认模式验证选中目标不生成虚拟房间几何、墙面位姿保持、窗口外围透明、四种效果共存与丢失恢复。传送门与降雪用同样的 1,854 项冻结输入回归，覆盖识别目标、种子投送、降雪聚散和冻结恢复。已目检虚拟层截图。PICO 实景、真实手势和双眼视图待验。[验证与哈希](verification.json)

首轮 Editor 回放因阶段图 PNG 压缩阻塞手样本，触发原有超时。截图先暂存，回放结束后再压缩，后续验证通过。

## 源码

[完整源码补丁](unity-editor-source.patch)以 JC 既有整合提交 `312fc0d5495a7cbfb5cd6c58da471078ecf78a77` 为应用基础。它包含上一版原创湖景、水母灯和本轮透视修改。以该提交建立干净工作区，执行 `git apply --check --binary /path/to/unity-editor-source.patch` 检查，再执行 `git apply --binary /path/to/unity-editor-source.patch`。构建环境沿用该提交的 LOCAL_DEPENDENCIES.md。已复核 Git 应用后的源码树与本轮 Editor 输入一致。

## Android 封包

`RoomPassthrough-20261123.apk` 已通过签名、CRC、4 字节对齐、15 场景和 16 个 ARM64 库核对。检查了包内湖景纹理、水母着色器和湖景着色器。IL2CPP 元数据包含掌心和墙面定位方法，已移除实景房间的微缩选择方法。SHA256 为 `ef15ac16f5441ceedef45df6b93e81436975e74779a6b35649dcbba447ce339f`。[封包验收](android-v2/acceptance.json)

按 1,848 项冻结生产输入封包。首轮生成 IL2CPP 中间产物后保留了临时 `--jobs`，正式封包在已恢复的原设置下复用生成产物完成。封包期间 23 次采样，最低可用内存 4.48 GiB，任务进程合计最高 3,925.91 MiB，D 盘占用最多净增 96.05 MiB。隔离工程原 1,840 个源码输入和七个默认构建文件已逐项恢复。
