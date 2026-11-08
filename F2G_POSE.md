# 双目位置 + F2G-Pose 旋转实验版

## 两通路 YOLO 配置同步（2026-11-08）

定位服务与姿态服务共用 `object-detection-demo/detector_config.py`，默认权重为 `best_yolo26n_assgn2_v6_50e.pt`，conf=0.399、imgsz=640、iou=0.7。以后更换默认权重后需要重启两个服务。加载时检查文件和 `lighter` 类别，并输出实际文件 SHA-256。缺失文件会报错退出。

PC 健康回复包含姿态服务实际加载的权重指纹和检测参数。定位服务将其与自身配置比较，真实模型不一致时输出 `DETECTOR_MISMATCH` 并撤回姿态就绪通知。录制模式继续按原诊断流程工作。5007 仅用于 PC 健康检查。定位服务继续返回最高分候选，姿态服务保留多目标拒绝检查。

另一个差异来自检测图像行方向。当前 Unity 场景的 `flipV=true` 与 EncodeToJPG 的存储翻转抵消，定位 YOLO 使用原始行序。姿态 YOLO 现也使用原始行序，随后将检测框映射到正向图像供 SAM 使用，再把 mask 映射回原始深度坐标。F2G 的 RGB／深度／内参及旋转坐标转换仍由 `--native-bottom-up` 完成。此处更新了此前将姿态 YOLO 也翻转的做法。

25 项 Python 回归通过。通过 Windows 定位 TCP 服务与 Linux 完整姿态服务，同时回放 30 组历史打火机双目数据，30 次就绪通知均确认模型匹配，定位检测非空 8 帧，完整姿态有效 4 帧，有效姿态 PC 往返中位数约 437 ms。其余姿态结果为漏检 16 帧、多目标 5 帧、前景深度不足 5 帧。该结果是录制数据回放，实机旋转验收仍待完成。报告位于 `.codex-tmp/dual-detector-20261108-aligned-replay.json`，两端权重 SHA-256 均为 `999bca4f5bf96aeed3ac0f1047e31530e4eb4584873c85dd5bd00031fc71fb3a`。

本机启动定位服务可用 `python tcp_server.py --headless --host 127.0.0.1 --device cuda --half`，姿态服务使用下述 `Tools/start_f2g_wsl.sh`。回放工具 `Tools/check_detector_routes.py` 接受 `--captures` 帧目录集合与 `--output` 报告路径，使用期间应让头显断开两条数据连接。

## 1.11.2 真实模型接入（2026-11-03）

APK 已构建并安装，包名 `com.yn.picmagicmr.stereo.pose`，版本 1.11.2 / 27，SHA-256 `ACF5E45E0358210219F05CE53648DD00AD27CB16FD56E8714AEB70D89ABB0A6D`。原 Repair 版保留。构建日志 `.codex-tmp/pose-build-112.log` 包含定位／召唤与 F2G 几何检查通过；Python 共 20 项相关检查通过。1.11.1 回退 APK 位于 `.codex-tmp/BridgeStereoPoseFusion-1.11.1.apk`。

WSL／Ubuntu、CUDA 扩展及三个固定 SHA-256 权重已安装完成。`/opt/f2g/venv` 为独立 Linux 环境，原 Windows YOLO 环境未替换。真实官方样例单独 F2G 预热后 79–117 ms；完整 YOLO＋SAM＋F2G 样例预热后约 326 ms，峰值已分配显存约 4381 MiB。两路服务共存时一次整卡读数为 5248 / 8151 MiB。安装检查见 `.codex-tmp/f2g-linux-preflight.json`，实际前向检查见 `.codex-tmp/f2g-real-inference.json`。

实机已出现真实打火机 `OK` 姿态结果及原定位 `TARGET_LOCKED / StereoVisualAverage3`。有效姿态在关闭写盘后通常约 400–480 ms，尚不能据此认定旋转验收通过。用户反馈 `POSE_STALE_OR_CONFIRMING / ROTATION_HOLD`；1.11.2 将结果有效期从 600 ms 调整为 900 ms，间断判断随预算调整，避免约 800 ms 的有效连续观测反复清空确认。1200 ms 请求截止时间仍保留；`[PoseResult]` 日志记录真实帧龄、处理耗时、是否接受和稳定样本数。新增检查覆盖慢速观测、真正中断后的三帧重获和过期拒绝。

实机原始像素与普通图片的行方向不同。启动脚本启用 `--native-bottom-up`：模型使用上下翻转后的 RGB／深度／mask 和对应主点；返回旋转用反射矩阵同时变换相机与物体基，再进入原校正坐标恢复。双目匹配保留原标定像素坐标。该修正不保证改善所有检测：同批 30 帧按置信度 0.5 检查，恰好一个打火机的帧数为原方向 15、正向 12，不能将识别不稳定全部归因于图像方向。

从 Windows 项目目录启动真实姿态服务（默认不录制图像）：

```powershell
& 'C:/Program Files/WSL/wsl.exe' -d Ubuntu-24.04 -- bash /mnt/d/Unity/Project/magic-wand-amusement-park/Tools/start_f2g_wsl.sh
```

该入口先用官方样例预热全部模型，再在 **127.0.0.1:5006 / 5007** 监听；Windows 到 WSL localhost 转发已实测可达。不要将 5007 做 ADB reverse。另一终端运行原 Windows `tcp_server.py --headless --device cuda --half`，只转发 5005 和 5006。姿态退出或失效时原位置通路继续运行。真实打火机旋转方向、稳定性和端到端帧龄仍需验收。

## 1.11.1 通信修复（2026-11-02）

实机补充：仅将 Socket 改成异步并退避，仍在服务离线测试中出现 5 次 YOLO 超时，不能单独作为修复结论。最终加入 **PC 就绪通知**：姿态进程在 PC 上另开 5007 健康端口，YOLO 的独立监控线程读取健康状态，在已有 5005 检测回复中附加 `pose_available` / `pose_model_ready`。头显只接受新鲜、同帧检测回复携带的通知，未确认可用时完全不连接 5006。5007 不做 ADB 转发。此方案避开了本机实测会干扰 5005 的“经 ADB 打开不可用姿态端口”操作；尚未通过 ADB 内部追踪确定其底层阻塞机制。

健康检查线程不等待图像或 GPU 工作；姿态服务最多两个客户端、一个计算任务，旧请求未结束时新请求回复 `POSE_BUSY`，不排队叠加推理。真正模型的运行时长、显存峰值和 YOLO 争用仍须在模型环境就绪后测量。

- 保留 `com.yn.picmagicmr.stereo.pose`，版本升级为 1.11.1 / 26；不覆盖定位 Repair 1.10.1。
- 原 YOLO、双目测深、三帧平滑与点赞召唤继续独立运行。姿态模块只改变旋转。
- `PoseTransport` 使用异步连接和收发，整次请求（含连接、压缩、发送和接收）共用 1200 ms 截止时间。取消会关闭对应 Socket；旧会话回复不能影响新会话。
- 失败按 1、2、4、8 秒退避，之后保持 8 秒。任何失败后停止发送图像，只尝试小型健康检查；连续三次失败标记断路状态。健康检查成功本身不清除连续图像传输失败次数，完整同帧回复成功才清除。
- 健康检查只有在真正模型已经加载时返回 `pose_ready=true`。录制服务始终报告 `RECORD_ONLY`，正常应用不会向它发送图像。实机传输测试可显式使用启动参数 `--ez pose_diagnostic true`，仍不会产生任何虚构姿态。
- 图像上限 2 FPS、最多一个在途请求、不设待发送队列。忙碌期间丢弃采集帧，下次只拿新的双目帧。协议 v2 在工作线程中 GZip 无损压缩原图，保留原标定和像素，不引入未经验证的 JPEG 深度误差；服务保留 v1 兼容。
- 左控制器 Y 键可切换姿态组件；暂停、复位和禁用取消旧会话。断线后模型保持最后可靠朝向，原位置继续更新。无可靠参考时仍可召唤，但需复位重新标定后才能启用旋转。
- 服务端录制默认关闭；显式录制默认最多保存 60 组。

验证入口：`python Tools/run_pose_transport_checks.py` 使用生产 C# 通信代码和真实回环 Socket，覆盖服务不回复、半包、持续慢速回复、不读取图像、取消、断开、异常长度、退避及恢复后的无损传输；`test_pose_bridge`、`test_pose_availability` 与 `test_tcp_reconnect` 共 19 项 Python 回归检查。`Tools/test_pose_isolation_device.py` 记录设备五种状态的对照结果。

2026-11-03 部署进度：用户重启后已安装 WSL 3.0.3／Ubuntu 24.04，发行版位于 `D:/WSL/F2GUbuntu`。独立环境 `/opt/f2g/venv` 使用 PyTorch 2.14.1+cu130、CUDA 13.0，PointNet2 实际 GPU 算子检查通过。F2G 与 RADIO 权重经过 SHA-256 校验，官方 ROPE 茶壶样例真实推理通过：首帧 1225 ms，预热后 79–117 ms，峰值已分配显存约 1669 MiB。记录见 `.codex-tmp/f2g-real-inference.json`。这不是打火机精度验收；SAM 全链路验证仍在进行。

### 最终实机通信验收与部署进度

最终 APK SHA-256：`AE12E0C055A8E75B86171F015E7E67F3B1861547EE9A4B8596F0028029BA92F1`，已安装到 PICO `PA921AMGKA310157G`。Unity 定位、召唤与旋转几何检查通过。头显佩戴状态下，每阶段记录约 35 秒，YOLO 日志启动后的前两条性能记录不计入均值：

| 状态 | YOLO 平均 FPS | YOLO 超时 |
|---|---:|---:|
| 姿态关闭 | 6.71 | 0 |
| 姿态服务不存在 | 6.66 | 0 |
| 实际双目图像传输／录制模式、不写盘 | 6.78 | 0 |
| 中途停止姿态服务 | 6.78 | 0 |
| 重新启动姿态服务 | 6.76 | 0 |

退出时有一次预期姿态连接错误，未传导为 YOLO 超时；恢复时重新确认服务并恢复图像传输。各阶段未出现头部跟踪丢失。证据位于 `.codex-tmp/pose-isolation-20261102-pc-readiness/`；初版异步退避仍失败的对照保存在 `.codex-tmp/pose-isolation-20261102/`。本次结果限定于通信隔离，不包含真实 F2G 推理或旋转精度，不代表已完成十次实物召唤验收。

用户已授权安装 WSL2/Linux、CUDA 和模型依赖，但禁止未经通知自动重启。`Tools/install_wsl_prerequisites.ps1` 已以管理员权限成功启用 `VirtualMachinePlatform` 和 `Microsoft-Windows-Subsystem-Linux`，结果 `restartNeeded=true`；没有执行重启。记录为 `.codex-tmp/wsl-prerequisites-result.json` 和 `.codex-tmp/wsl-prerequisites.log`。

用户已完成上述重启，无需再次执行此前的重启步骤。后续检查完整模型显存和端到端延迟，并验收实物旋转。原 Windows YOLO 环境保持原样。不要将“通信修复／官方样例推理已通过”描述成“实物旋转已交付”。

以下为早期版本的设计和历史记录；旧版 5 FPS、同步等待和持续重连行为已由上述实现替代。

本版本保留 `StereoYoloLocator` 的位置估计、位置滤波与首次尺寸标定，新增 PC 姿态服务，只将 F2G-Pose 的旋转用于显示。现有 `tcp_server.py`、5005 协议和部署 YOLO 权重不变。

## 当前交付与验证边界（更新至 2026-10-29）

- 独立场景：`Assets/Scenes/BridgeStereoPoseFusion.unity`；原默认场景和 Build Settings 未切换。
- Unity 菜单：`Bridge → Configure Stereo F2G Pose Scene`、`Bridge → Build Stereo F2G Pose APK`。
- 实验包输出：`Builds/Android/BridgeStereoPoseFusion.apk`，包名 `com.yn.picmagicmr.stereo.pose`，版本 `1.11.0 / 23`；可与原应用共存。
- CPU 测试验证了协议、深度尺度、校正坐标还原、异常拒绝和录制服务；这些不是打火机上的模型精度测试。
- 使用 Unity 自带 Roslyn 编译器和项目已有引用检查了 MagicMR、Assembly-CSharp、Assembly-CSharp-Editor，均通过；输出仅在 `.codex-tmp/pose-compile/`，没有替换 Unity 缓存 DLL。
- 10 月 29 日登录 Unity 后许可证已恢复。在已打开的编辑器执行了原集成检查和 F2G 旋转检查，均通过；实验 APK 构建成功并已安装到 PICO。构建结果记录为 `.codex-tmp/build-pose.result`。
- 本机 RTX 5070 Laptop / 8151 MiB、PyTorch 2.13.0+cu130 的 CUDA 内核探针通过。Linux/WSL、F2G-Pose、PointNet++、SAM、RADIO 与相关权重尚未就绪，**尚无真实 F2G-Pose 推理或头显旋转精度结果**。PICO 真实双目采集、传输、深度生成和录制通路已经跑通。

## 实现结构

### 2026-10-29 首次接入检查（构建前）

- ADB 已连接 PICO A9210，USB 5005/5006 转发已配置。
- 已启动 Windows YOLO 服务和 `--record-only` 姿态服务；进程记录在 `.codex-tmp/pico-services-1029.json`。
- 头显运行的是原版 `com.yn.picmagicmr.stereo`，版本 1.10.0 / 22，未安装姿态实验版。
- 原版已向 PC 发送真实相机帧：观测约 6.7–7 FPS、YOLO 推理约 11–13 ms；这不是端到端延迟或姿态推理指标。
- 当时头显日志显示 `LighterTooSmall` / `NO_LIGHTER`，尚未确认可靠定位；需让打火机完整、足够大地进入视野。
- 再次构建仍在 Unity 许可证检查阶段退出，日志为 `.codex-tmp/f2g-unity-build-1029.log`。Linux/WSL 和真实姿态推理环境仍未就绪。
- 5006 目前仅等待实验版客户端，尚未收到双目录制数据；不能把原版的 5005 图像收发视为旋转通路验证。

### 2026-10-29 登录 Unity 后的实机进展

- `STEREO_INTEGRATION_CHECKS_PASS`、`F2G_POSE_CHECKS_PASS` 已在实际 Unity 编辑器日志中确认。APK 约 59.7 MB，安装成功，启动包名为 `com.yn.picmagicmr.stereo.pose`。
- 新版向 5005 发送检测图像，同时向 5006 发送双目原始数据。已保存 226 帧到 `.codex-tmp/pose-captures/20261029-213822/`；录制服务随后停止以避免无上限写盘，检测服务继续运行。
- 抽查这段录制的首、中、末三帧均只有墙面及家具边缘，没有打火机，对应 `NO_LIGHTER`。这不是用户此前点赞时的同期证据，不能据此评估打火机检测精度。
- 修复 `tcp_server.py --headless` 客户端断开后整个服务退出的问题；现在等待重连，复用已加载模型，每次连接使用新帧缓存。两项回归测试验证会话隔离及推理线程退出时释放阻塞接收线程。
- `Assets/Editor/TabletopBuildRequest.cs` 支持 `.codex-tmp/build-pose.request`，在已打开的编辑器中构建；编辑器处于 Play、编译、构建或场景未保存状态时等待。构建结束保存恢复后的应用名称、包名及版本设置。

### 2026-10-29 超时排查与运行要求

- 关闭 5006 录制服务后，实验版继续尝试连接姿态通路。观测到 5005 检测输入降至约 0.5–1 FPS，头显反复出现 `YOLO_TIMEOUT`，而 GPU 推理仍仅约 7–16 ms。
- 恢复 5006 服务后输入恢复约 6.7–6.9 FPS，随后日志不再出现超时。此对比表明，当前 APK 的姿态通路断线仍会影响原检测链路；尚未定位到具体是线程调度还是 USB 转发层的阻塞，不能宣称两路已经完全隔离。
- 临时运行措施：保持 `pose_bridge.py --record-only`（不带 `--record-dir`）运行，仅处理双目、不保存图像、不加载姿态模型。新的 CLI 已支持此用法，避免通过停止服务来停止写盘。
- 另修复 PC 最新帧缓冲的跳帧问题：旧推理被新帧取代时，只更新已处理的 generation，确保最新帧无需等待第三帧到达即可处理。相关测试现共三项通过。
- 恢复服务后的头显状态为 `NoReliableLighterDepth`，表示已有检测框但深度匹配未通过，与超时和 `NO_LIGHTER` 是不同问题。

```text
PICO 左目 RGB + 右目灰度 + 双相机内外参 + 两侧采集时间
  ├─ 原位置路径 → YOLO :5005 → 双目点匹配 → Anchor_Lighter.position
  └─ 姿态路径 :5006
       双目校正 → SGBM + 左右一致性 → 米制深度
       校正后的 RGB → YOLO → SAM 前景 mask
       F2G-Pose（不输出补全形状）→ 原始左相机坐标旋转
       Unity 拍摄时头姿 + 相机外参 → 世界旋转 → 召唤标定 → 锚点旋转
```

`Anchor_Lighter` 已处在原位置参考点，直接作为旋转中心；`Lighter` 保留标定后的局部偏移、缩放和现有手势效果。无需额外重设模型层级，避免原 binder 把模型重新绑定到锚点造成冲突。花朵为世界空间对象，新增 `RotateWorldEffects` 处理旋转，原 `SyncWorldAnchor` 处理平移，两者不重复补偿根节点位移。

F2G 预测的位置、尺寸和形状不驱动对象。原检测框中心加表面深度不等于恒定的物体几何中心，大幅转动、薄侧面、反光和遮挡仍可能引起位置偏移。此版本不会消除该定位局限。

## Windows 上先录制输入

在项目根目录、两个终端分别运行：

```powershell
object-detection-demo/.venv-gpu/Scripts/python.exe object-detection-demo/tcp_server.py --headless
```

```powershell
object-detection-demo/.venv-gpu/Scripts/python.exe object-detection-demo/pose_bridge.py --record-only --record-dir .codex-tmp/pose-captures
```

若没有现有 `.venv-gpu`，使用已安装 numpy、opencv-python 的 Python 运行录制服务即可。姿态服务的录制模式不加载 torch、YOLO、SAM 或 F2G，不返回模拟旋转。

暂不需要保存图像时，使用 `python object-detection-demo/pose_bridge.py --record-only`，不传 `--record-dir`；当前实验 APK 测试期间保持此服务在线。

连接 PICO 后，在可使用 adb 的终端执行：

```powershell
adb reverse tcp:5005 tcp:5005
adb reverse tcp:5006 tcp:5006
```

打开新场景，执行 `MagicMR → Checks → F2G Pose`，再用 **Build Stereo F2G Pose APK** 构建。该构建入口也运行原有集成检查。菜单会从 BridgeTest 重新生成实验场景，先保存手工修改。旧版 Build Stereo YOLO APK 仍构建原版本。

```powershell
adb install -r Builds/Android/BridgeStereoPoseFusion.apk
```

录制直立静止、缓慢转动、倾斜、单纯转头、部分遮挡等序列。每帧目录包含：

| 文件 | 含义 |
| --- | --- |
| `header.json` | 相机参数、左右时间戳、会话和帧 ID |
| `left.png` / `right.png` | 原始左目 RGB / 右目灰度，顶行在前 |
| `rectified_rgb.png` / `rectified_right.png` | 双目校正图像 |
| `depth_m.npy` | 米制深度；0 表示无效，不填补虚构几何 |
| `K.txt` | 校正图像对应的内参 |

若校正失败，原始图像和 header 仍保留，服务回复原因。录制持续写入大文件，采集到足够样本后用 Ctrl+C 停止。

## Linux GPU 推理环境

已核对官方源码版本 `6141733a61d9a6e764bb0a72d2c5ecde06adc1db` 的 `PoseEstimator.predict` 接口。官方使用 Linux/Python 3.10、PyTorch 2.4/CUDA 12.4；本机 GPU 为 SM 12.0，不能未经验证直接采用旧工具链。

1. 准备 Linux/WSL2、专用 Python 虚拟环境、支持本机 GPU 的 CUDA PyTorch/torchvision 和 CUDA 开发工具包（包含 nvcc），先验证 CUDA 内核可运行。
2. 在该环境运行下面的安装脚本。它固定 F2G/RADIO 源码版本并校验三个权重 SHA256，编译 CUDA 扩展；保留已安装的 torch/torchvision，遇到依赖冲突会停止。该较新 CUDA 环境下的安装流程尚未实测。

```bash
python Tools/setup_f2g_runtime.py
source .codex-tmp/f2g-runtime/environment.sh
python object-detection-demo/pose_preflight.py \
  --checkpoint .codex-tmp/f2g-runtime/weights/f2g-pose.pt \
  --sam .codex-tmp/f2g-runtime/weights/sam_vit_b_01ec64.pth
```

官方 F2G 包元数据仍声明旧 torch/torchvision 固定版本，安装器对该包使用 `--no-deps`。不能把安装结束或 preflight 就绪等同于模型可用；必须用实际样例验证 CUDA 扩展及模型前向。若新工具链编译失败，应记录错误并适配，而不是降级到不支持本机 GPU 的运行时。

先用官方随源码附带的 ROPE 样例验证真正的模型推理：

```bash
f2g-pose predict \
  --checkpoint .codex-tmp/f2g-runtime/weights/f2g-pose.pt \
  --rgb .codex-tmp/f2g-runtime/F2G-Pose/examples/rope/rgb.png \
  --depth .codex-tmp/f2g-runtime/F2G-Pose/examples/rope/depth.npy \
  --mask .codex-tmp/f2g-runtime/F2G-Pose/examples/rope/mask.png \
  --intrinsics .codex-tmp/f2g-runtime/F2G-Pose/examples/rope/K.txt \
  --depth-scale 1 --pose-only --output .codex-tmp/rope-pose
```

为一个录制帧的 **rectified_rgb.png** 画二值 mask，保存为独立的 `manual_mask.png`，进行离线测试（将 FRAME_DIR 换成该帧目录）：

```bash
python object-detection-demo/pose_bridge.py \
  --offline FRAME_DIR --mask FRAME_DIR/manual_mask.png \
  --checkpoint .codex-tmp/f2g-runtime/weights/f2g-pose.pt
```

离线推理不加载 SAM/YOLO，以便区分深度问题与自动分割问题。输出 `pose.json` 的旋转已经由校正相机还原到原左相机坐标。

离线通过后启动实时服务：

```bash
python object-detection-demo/pose_bridge.py \
  --host 127.0.0.1 --port 5006 \
  --checkpoint .codex-tmp/f2g-runtime/weights/f2g-pose.pt \
  --sam .codex-tmp/f2g-runtime/weights/sam_vit_b_01ec64.pth
```

Windows 上的 5005 检测服务仍须运行。WSL 部署时须验证 Windows 的 localhost:5006 能访问到 WSL 服务，再使用 adb reverse；若本机转发配置不同，先解决端口可达性。本机 8 GB 显存下 YOLO、SAM、RADIO 与 F2G 共存尚未验证，需测峰值显存和整条链路耗时。

## 召唤、恢复与关闭

- 物体保持直立、静止，等待位置就绪和 `POSE_REFERENCE 3/3`，再点赞召唤。固定偏移让模型以原召唤朝向开始，根据之后的相对旋转变化跟随。
- 若召唤时没有新鲜且稳定的姿态参考，位置模式照常运行，旋转不自动在任意后续姿态上标定；出现 `RESET_AND_SUMMON_FOR_ROTATION` 时复位再召唤。
- 默认请求上限 2 Hz、结果最大年龄 0.9 秒，只有一个姿态请求在途。网络 I/O 在工作线程，位置服务不等待它；GPU 争用对原 YOLO 延迟的影响仍要实测。
- 结果必须匹配会话、帧 ID、采集时间。复位、暂停、禁用后，旧结果不参与新标定。
- 旋转大跳变或长时间丢失后的恢复需要连续三帧相近观测。普通旋转使用四元数平滑，陈旧或无效结果保持最后朝向。此策略不保证解决打火机正反面对称导致的轴歧义。
- 在 Inspector 关闭 `F2GPoseClient` 即停止姿态请求并恢复初始锚点朝向，位置通路继续运行。实验旋转目前仅支持 `VisualAverage` 模式。
- PICO 相机使用已有 PINHOLE 采集模式；新增深度路径假定 SDK 输出已对应这些虚拟针孔内参。实机仍需检查双目校正后的垂直视差，不能仅凭合成测试认定标定正确。

## 回归与实机验收

```powershell
object-detection-demo/.venv-gpu/Scripts/python.exe -m unittest discover -s object-detection-demo -p test_pose_bridge.py -v
object-detection-demo/.venv-gpu/Scripts/python.exe object-detection-demo/pose_preflight.py --output .codex-tmp/pose-preflight.json
```

Unity 检查菜单覆盖旋转左右手系、头姿补偿、旋转异常过滤、重获确认、复位和旋转中心；10 月 29 日已在实际编辑器执行通过。服务重连检查可用 `python -m unittest discover -s object-detection-demo -p test_tcp_reconnect.py -v` 单独运行。

头显验收至少包括：静止物体只转头时不跟转；三轴缓慢旋转方向正确；姿态服务退出后平移仍工作；遮挡保持与恢复；复位不回灌旧结果；放大不被模型尺寸覆盖；变花后位置及旋转仍跟随。记录端到端帧龄、姿态有效率、静止角度抖动和峰值显存，再决定是否提高请求频率。

官方资料：[F2G-Pose](https://github.com/jrryzh/F2G-Pose)、[输入与坐标](https://github.com/jrryzh/F2G-Pose/blob/main/docs/USAGE.md)、[安装](https://github.com/jrryzh/F2G-Pose/blob/main/docs/INSTALL.md)。
