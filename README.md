# Magic Wand Amusement Park

基于 Unity 和 PICO 的 MR 手势交互项目。当前主线通过 **YOLO 检测真实打火机 + 双目图像估计三维位置**，将虚拟打火机叠加到实物上，再用手势触发碳化、生命化、躲闪、变花和放大效果。

当前默认场景为 `Assets/Scenes/BridgeStereoFusion.unity`，默认跟随模式为 **VisualAverage（1.11.0，最近三帧视觉位置加权平均）**。仓库同时保留桌面卡牌捕捉、AR 图像追踪和早期 MR 场景。

## 当前功能与限制

- PICO 采集双目图像，PC 端 YOLO 返回打火机检测框，Unity 使用对应帧的双目结果与拍摄姿态计算世界位置。
- 首次让直立打火机完整可见并保持静止，完成位置、底部和高度标定；右手点赞确认后显示虚拟模型。
- 默认只跟随实物的三维平移，保留首次标定尺寸与召唤朝向，**不估计实物旋转**。
- 最近三次可靠测量按 `0.2 / 0.3 / 0.5` 加权；仅保留最近 0.3 秒的数据，并拒绝过期、重复、乱序及不可靠深度结果。静止死区与跳变确认用于减少抖动。
- 视觉模式下，左手不会接管模型位置。漏检或遮挡导致测量不可靠时保持最后位置；完全遮挡时不会继续追踪实物移动。
- Inspector 中可选择 `HybridGrip` 使用历史手部接续模式；它不是当前默认模式。
- 已有编辑器合成回归检查；定位精度、延迟和遮挡恢复效果仍需 PICO 实机验证，不能把滤波参数或内部误差指标当成实测精度。

详细实现和历史记录见 [STEREO_YOLO.md](STEREO_YOLO.md)。该文件包含多个历史版本，当前行为以本分支 1.11.0 说明和代码为准。

## 环境

| 项目 | 当前配置 |
| --- | --- |
| Unity | `6000.0.26f1`，安装 Android Build Support |
| 渲染 | URP `17.0.3` |
| 手部与 XR | XR Hands `1.5.1`、OpenXR `1.13.2`、PICO SDK |
| AR 场景 | AR Foundation / ARCore / ARKit `6.0.7` |
| PC 检测 | Python、Ultralytics、OpenCV、PyTorch |
| 设备连接 | 支持所用 PICO 相机接口的头显，开启手部追踪、USB 调试及相机相关权限 |

`Packages/manifest.json` 中的 PICO SDK 已固定到官方 3.4.0 提交 `https://github.com/Pico-Developer/PICO-Unity-Integration-SDK.git#c93a59bab88d337fe0d54140499712897cedbe52`，首次打开会由 Unity Package Manager 下载，无需配置 Windows 本地路径。

## 快速运行当前 MR 场景

### 1. 启动 PC 检测服务

在项目根目录执行：

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r object-detection-demo/requirements.txt
.\.venv\Scripts\python.exe object-detection-demo/tcp_server.py
```

无需预览窗口时，可给最后一条命令加 `--headless`。服务接收的是 **PICO 相机图像**，不是电脑摄像头。

当前 `tcp_server.py` 实际加载的权重为：

```text
object-detection-demo/weights/best_yolo26n_quad_v4_50e.pt
```

本地 v5 / v6 / v7 训练结果不会自动替换部署模型。启动时检查控制台输出的权重名称；训练资料与检测工具另见 [object-detection-demo](object-detection-demo/README.md)，服务的实际配置以 `tcp_server.py` 为准。

### 2. 连接头显

通过 USB 连接并授权调试，在已安装 Android Platform Tools 的终端执行：

```powershell
adb devices
adb reverse tcp:5005 tcp:5005
```

当前场景的连接地址为 `127.0.0.1:5005`，通过 USB 反向转发访问 PC 服务。重新连接设备后如无法连接，请重新执行转发命令。

### 3. 构建与安装

在 Unity 中确认 PICO SDK 和 Android 环境可用，然后选择：

**Bridge → Build Stereo YOLO APK**

该菜单会生成花朵资源、执行集成检查，并从 `BridgeTest` 重新配置和保存双目场景；它会覆盖生成场景，请先保存需要保留的手工场景调整。

| 项目 | 菜单构建结果 |
| --- | --- |
| APK | `Builds/Android/BridgeStereoFusion.apk` |
| 应用名 | `Magic MR Stereo YOLO` |
| Android 包名 | `com.yn.picmagicmr.stereo` |
| 版本 / versionCode | `1.11.0` / `24` |
| 构建类型 | Development，关闭自定义签名 |

菜单结束后恢复原来的应用标识、名称、版本和签名选项，因此 Project Settings 中保存的版本号可能与上述 APK 版本不同。菜单也会关闭 Spatial Mesh，当前双目定位不依赖场景网格。

```powershell
adb install -r Builds/Android/BridgeStereoFusion.apk
```

先启动 PC 服务，再在头显中打开应用。

### 4. 标定、召唤与施法

1. 让直立打火机完整进入视野，保持静止，等待定位和标定就绪。
2. 右手在目标附近做点赞并停稳约 0.3 秒，确认召唤。
3. 按提示张开手掌约 0.15 秒，再开始施法。
4. 缓慢移动实物，观察模型平移跟随；短暂遮挡后重新露出机身，检查定位恢复。

| 操作 | 效果 |
| --- | --- |
| 右手快速捏合后松开 | 外观：烧焦材质与烟雾 |
| 右手伸食指画闭合小圈 | 生命：眼睛、呼吸等生命化效果 |
| 右手张掌快速横挥 | 规则：短暂侧向躲闪，再回到实物锚点 |
| 右手握拳后张开 | 解构：隐藏打火机并生成花朵 |
| 双手张开并向两侧拉开 | 放大：逐次增大，达到 3 倍上限后再次触发展示内部示意结构 |
| 左手靠近 Reset 按钮并捏合保持约 0.3 秒 | 清除效果，重新定位和召唤 |

右手施法需要先进入目标附近约 20 cm 的范围；外圈容差用于衔接动作，不代表可从远处开始施法。统一手势冷却为 0.35 秒，复位和双手放大另有各自保护条件。当前变花使用“握拳后张开”，不是旧 README 中的响指。

### 手部施法特效（当前工作区新增）

`RightHandSpellVfx` 为召唤和五种变换提供手部反馈，由手势识别与调度代码自动创建，无需手工挂载到场景。当前工作区包含以下效果：

| 动作 | 手部与目标反馈 |
| --- | --- |
| 点赞召唤 | 拇指处金色进度光环，召唤成功后播放传递光脉冲和目标光环 |
| 捏合施法 | 指尖橙红火星，接受施法后播放传递光效与目标光环 |
| 食指画圈 | 青绿色指尖光轨，接受施法后释放生命化光效 |
| 张掌横挥 | 淡蓝风痕，接受施法后沿挥动方向释放笔画 |
| 握拳后张开 | 握拳时金色蓄力火星，接受变花后释放花瓣轮廓与传递光效 |
| 双手拉开放大 | 金色光丝连接到目标；左手关节可用时补充左手光丝 |

预览只反馈手型和运动，不触发物体变化，也不表示施法已被接受。完整施法特效在 `GestureManager` 通过状态、距离和冷却检查，并调用物体效果后播放；召唤光效则在召唤成功后播放。

特效采用世界坐标，最多复用 80 条效果笔画，另有一条指尖轨迹。超过约 0.15 秒没有有效手部采样时清除画圈轨迹，已发射笔画按各自寿命消退；复位或禁用特效组件时清除全部笔画。

当前花瓣是发光轮廓。物体变化流程与传递光效在同次施法中启动，**尚未实现光点抵达目标后才触发变化**。PICO 上的亮度、真实物体遮挡、双眼一致性和帧率仍需实机验证。

编辑器检查入口：**MagicMR → Checks → Right Hand Spell VFX**。检查内容包括 shader 编译、无有效手部输入时不释放、五种效果、笔画池上限、复位清理、召唤和组件禁用，并调用手势回归检查。检查通过不代表已完成实机画质或性能验收。

## 状态与排查

| 状态或现象 | 检查方向 |
| --- | --- |
| `PC_DISCONNECTED` | PC 服务、USB 授权及 `adb reverse`；客户端会自动重连 |
| `NO_LIGHTER` | 目标是否完整可见、尺寸是否足够、当前模型是否能识别 |
| `LighterTooSmall` | 检测框太小，无法可靠进行双目纹理采样 |
| `NoReliableLighterDepth` | 当前双目匹配未通过，检查纹理、遮挡和图像质量 |
| 遮挡时模型停住 | 默认视觉模式保持最后位置；露出实物以恢复测量 |
| 手势被阻止 | 查看面板反馈，检查召唤状态、手部追踪、距离和动作冷却 |

日志标签包括 `[StereoCapture]`、`[StereoYOLO]`、`[Registration]`、`[FollowMetrics]` 和 `[MagicMR]`。`Distance` 表示拍摄时头部中心到测量点的直线距离，`Z` 表示左相机光轴深度，两者含义不同。

启用实验会话并生成日志后，可导出：

```powershell
adb pull /storage/emulated/0/Android/data/com.yn.picmagicmr.stereo/files/StudyLogs/ ./StudyLogs/
```

实际日志位置也可从 `[DataLogger] Session started` 输出中确认。

## 场景入口

| 场景 | 用途 | 当前构建状态 |
| --- | --- | --- |
| `BridgeStereoFusion` | YOLO + 双目定位、视觉跟随与手势魔法 | 默认启用 |
| `BridgeTest` | 原始识别桥接场景，也是双目场景生成源 | 未启用 |
| `MagicMR` | 早期 MR 手势场景 | 未启用 |
| `VstTest` | 透视测试场景 | 未启用 |
| `SampleScene` | AR 参考图追踪与卡牌捕捉 | 未启用 |
| `DesktopCaptureDemo` | 电脑点击 Cube，转换为卡牌并飞入背包 | 不在当前构建列表 |

以上场景均位于 `Assets/Scenes/`。桌面 Demo 可直接在编辑器 Play；AR 与 MR 功能需要对应设备和配置。恢复旧桥接版本时需切换构建场景，并按需恢复 Spatial Mesh 设置。

## 代码与资源

| 目录 / 文件 | 内容 |
| --- | --- |
| `Assets/Scripts/Perception/Stereo/` | 双目采集、几何投影、视觉位置平均、历史手部跟随 |
| `Assets/Scripts/Perception/` | TCP 通信、识别结果处理、锚点与目标绑定 |
| `Assets/Scripts/MagicMR/` | 手势规则、召唤、效果、复位和实验日志 |
| `Assets/Editor/` | 场景构建、模型生成与编辑器回归检查（含 `RightHandSpellVfxChecks`） |
| `Assets/MagicMR/` | 花朵 prefab、模型网格、材质、shader 和音效 |
| `Assets/Resources/MagicMR/HandSpell.shader` | 手部施法特效使用的 URP 加法混合 shader |
| `object-detection-demo/` | PC 检测服务与部署权重 |
| `Tools/` | 数据审查、标注整理、模型训练与报告脚本 |
| `STEREO_YOLO.md` | 双目主线实现说明与历史验证记录 |

`VisualAverageChecks`、`StereoIntegrationChecks`、`TabletopGestureChecks` 等提供合成检查入口。它们用于验证算法和集成行为，不替代头显上的实际叠加测试。

## Git 提交范围

提交源代码、场景、所需模型和材质、对应 `.meta`、项目配置、文档及 `.gitignore`。生成的模型网格只要被场景或 prefab 引用，也属于需要保留的项目资源。

本次手部特效需配套提交 `RightHandSpellVfx.cs`、`HandSpell.shader`、`RightHandSpellVfxChecks.cs` 及各自 `.meta`，同时保留手势识别、调度和复位代码的接入改动。只提交 README 不会让已有版本获得这些效果。

`.gitignore` 已排除：

- Unity 缓存、构建目录、APK、Python 虚拟环境和缓存。
- `.codex-tmp/`、`tmp/` 和 `Assets/XR/Temp.meta`。
- `Find lighter/` 根目录的 ZIP、列出的新增审查及标注副本、v5 / v6 / v7 合并数据集。
- `Find lighter/runs/` 下未跟踪的训练输出、检查点、图表和结果归档。

忽略规则保留本地文件，但这些文件不会随克隆下载；复现实验需要另外准备原始数据与相关产物。已被 Git 跟踪的历史训练文件不会因为新增规则而自动移除。不要整体忽略 `Assets/` 或所有 `.meta`，也不要将训练输出目录与实际部署权重目录混淆。

## restore-4d-lighter 1.11.0：以 main 为基础的交互版本

本分支使用 main 的双目视觉跟随场景和统一右手手势规则，保留增强施法光效。不包含旧分支的额外叙事情境。修改和发布只发生在 restore-4d-lighter，main 不受影响。

首次标定要求完整检测框在画面内部、YOLO 置信度至少 0.6、可靠的双目深度和不超过 0.3 秒的图像。连续至少五次测量、持续至少 0.6 秒，并检查中心、真实高度、宽度和底部偏移的稳定性；未满足时继续提示保持直立和静止，不显示模型。站稳后右手在目标附近点赞约 0.3 秒召唤，再张开手掌至少 0.15 秒解锁施法。

右手快速捏合后松开产生烧焦材质与烟雾；伸食指画闭合小圈产生眼睛和呼吸；张掌快速横挥使模型短暂侧向躲闪并返回移动中的实物锚点；握拳后张开隐藏打火机并在底部生成花朵。双手全部张开，在目标附近分别向左、右拉开，单次放大 1.35 倍，累计上限为 3 倍，到达上限后再做一次展示内部示意结构。双手动作期间独占识别，避免同时触发横挥；手回到起始间距或闭合后才能重复。左手在 Reset 按钮附近真实捏合并保持 0.3 秒，会清除效果、尺寸变化和注册信息，然后重新定位和召唤。

施法光效采用 2.5 倍尺寸、1.8 倍持续时间和 1.6 倍亮度；可在 BridgeTest 源场景的 GestureDetectors 上添加 RightHandSpellVfx 后在 Inspector 调整。池容量限制为 80，Reset、禁用和暂停会清除残留光效。

视觉跟随保持首次尺寸与朝向，使用最近三帧加权位置。按直立物体的真实高度检查跟随结果，避免视角俯仰使检测框看起来变短。短暂遮挡时保持最后可靠位置；露出机身后，过期或不可靠测量不移动锚点，较长失踪或大位移需连续三次可靠测量恢复。完全遮挡时无法观察真实移动，不估计旋转。

### 替换虚拟模型

1. 将 FBX、GLB 转换后的 Unity 模型或 prefab 导入 Assets，在 Project 窗口选择模型。
2. 点击 **Bridge → Use Selected Lighter Model**，编辑生成的 `Assets/Resources/MagicMR/LighterModelSettings.asset`。
3. 设置 `rotationDegrees`，让模型直立方向为 +Y、正面为 +Z。设置 `metersPerUnit`：米为 1，厘米为 0.01，毫米为 0.001；Unity 导入器已换算成米时保持 1。
4. 使用 **Bridge → Build Stereo YOLO APK**。构建替换视觉网格，保留 Lighter 根对象、RealityEditor、定位和手势组件；首次标定自动按真实高度缩放并对齐底部。导入 prefab 的脚本和物理碰撞不会接管定位。
5. 清空配置中的 `modelPrefab` 可恢复默认打火机。不要删除或整体替换场景中的 Lighter 根对象。

这里的模型替换只改变虚拟显示，不改变 YOLO 的 Lighter 类。更换真实识别对象或 YOLO 权重需要另外训练/验证检测与尺寸假设。

### 验证

构建运行 RightHandSpellVfxChecks、StereoIntegrationChecks 和 RequestedInteractionChecks，覆盖手势、光效池、首次完整标定、视觉位置/遮挡恢复、模型底部注册、3 倍放大和内部结构复位。这些是合成回归，不是实测精度报告。实机请测试不同距离与视角，缓慢移动、遮挡后恢复，并观察调试面板状态及 StudyLogs；不可靠深度会保持锚点而非伪造位置。
