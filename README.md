# Magic Wand Amusement Park

Unity AR / MR 互动项目：在场景中识别 3D 目标，将其「拍扁」成 2D 卡牌飞入背包；在 PICO 头显上通过手部手势对魔法目标施法。

项目包含 **3 个自有场景**（`Assets/Scenes/`），分别对应桌面捕捉 Demo、AR 图像追踪、PICO MR 手势魔法三条开发线。

## 技术栈


| 项目    | 版本 / 说明                            |
| ----- | ---------------------------------- |
| Unity | `6000.0.26f1`                      |
| 渲染管线  | URP（Universal Render Pipeline）     |
| AR    | AR Foundation `6.0.7`、ARCore、ARKit |
| XR    | OpenXR、PICO SDK（PXR）、XR Hands      |
| UI    | uGUI、新 Input System                |


## 环境要求

- [Unity Hub](https://unity.com/download) + Unity **6000.0.26f1**
- 桌面端：`DesktopCaptureDemo`、`SampleScene` 编辑器预览 — Mac / Windows
- AR 真机：`SampleScene` — 支持 ARCore / ARKit 的 Android / iPhone
- MR 真机：`MagicMR` — PICO 头显 + 本机 PICO SDK + 手部追踪

> **注意**：`Packages/manifest.json` 中 `com.unity.xr.picoxr` 为本地路径，每人需改成本机 SDK 目录，否则 Package Manager 可能报错。

## 场景总览


| 场景                   | 路径                                       | 用途                     | 运行环境            | Build Settings       |
| -------------------- | ---------------------------------------- | ---------------------- | --------------- | -------------------- |
| DesktopCaptureDemo   | `Assets/Scenes/DesktopCaptureDemo.unity` | 3D → 2D 卡牌 → 飞背包（核心逻辑） | 电脑 Play，鼠标左键    | 未勾选                  |
| AR Image Tracking    | `Assets/Scenes/SampleScene.unity`        | AR 参考图追踪 + 捕捉交互        | 编辑器预览 / AR 手机真机 | 未勾选                  |
| PICO MR GestureMagic | `Assets/Scenes/MagicMR.unity`            | 手部手势驱动魔法目标             | PICO 头显         | **已勾选（Android 主场景）** |


Android 包名：`com.yn.picmagicmr`

---

## 场景一：DesktopCaptureDemo

**路径**：`Assets/Scenes/DesktopCaptureDemo.unity`

纯桌面演示，不依赖 AR 设备或 PICO 头显，用于最快验证「点击 3D → 变 2D 卡牌 → 飞背包」全流程。

### 场景内容


| 对象                  | 说明                                                                |
| ------------------- | ----------------------------------------------------------------- |
| `GameManager`       | 挂载 `CaptureDemoBootstrap` + `CaptureTo2D`，自动创建 UI 与 CaptureCamera |
| `Cube`              | 可捕捉的 3D 目标，图层 `3D_Target`，标签 `CaptureTarget`                      |
| `Main Camera`       | 场景主相机，用于射线检测                                                      |
| `Directional Light` | 场景光照                                                              |


### 操作步骤

1. 打开 `DesktopCaptureDemo.unity`
2. 点击 **Play**
3. 在 **Game 窗口**用鼠标**左键**点击 `Cube`

### 预期效果

- `Cube` 消失
- 屏幕中央弹出带 Cube 图像的 2D 卡牌
- 停顿 0.5 秒
- 卡牌缩小并飞向右下角背包

---

## 场景二：SampleScene

**路径**：`Assets/Scenes/SampleScene.unity`

AR Foundation 图像追踪场景，同时集成了与桌面 Demo 相同的捕捉逻辑。

### 场景内容


| 对象                      | 说明                                                                   |
| ----------------------- | -------------------------------------------------------------------- |
| `AR Session`            | AR Foundation 会话                                                     |
| `XR Origin`             | AR 相机 rig，含 `ARTrackedImageManager`                                  |
| `ReferenceImageLibrary` | 参考图库（标记图 `i`）                                                        |
| `GameManager`           | `CaptureDemoBootstrap` + `CaptureTo2D` + `SampleSceneDesktopPreview` |
| `CaptureCamera`         | 虚拟摄影棚，渲染 `3D_Target` 图层到 `CardTexture`                               |
| `Canvas`                | `2D_Card_UI`（卡牌）+ `Backpack_Icon`（背包）                                |
| `Cube`（预制体）             | 标签 `CaptureTarget`，图层 `3D_Target`                                    |


### 编辑器预览（无 AR 设备）

1. 打开 `SampleScene.unity`，点击 **Play**
2. `SampleSceneDesktopPreview` 自动：关闭 AR Session、禁用 AR 组件、切换 Skybox 背景，避免黑屏
3. 用鼠标左键点击 `Cube`，效果与桌面 Demo 相同

### AR 真机测试

1. Build 到支持 ARCore / ARKit 的手机
2. 将摄像头对准参考图 `i`（`Assets/ReferenceImageLibrary`）
3. 识别成功后，点击场景中的 `Cube` 触发捕捉

---

## 场景三：MagicMR

**路径**：`Assets/Scenes/MagicMR.unity`

PICO MR 主场景，通过 XR Hands 手部追踪识别手势，驱动魔法目标状态变化。当前为 **Android 打包默认场景**。

### 场景内容


| 对象                  | 说明                                      |
| ------------------- | --------------------------------------- |
| `XR Origin (VR)`    | PICO VR rig，含 `PXR_Manager`（手部追踪、MRC 等） |
| `StudySystem`       | `DataLogger` + `GestureManager`（实验会话与 CSV 记录） |
| `GestureDetectors`  | 四个手势检测器 + `MagicMRStudyBootstrap`（VST 相机配置） |
| `Lighter`           | 魔法目标，挂载 `RealityEditor`（四维现实编辑）          |
| `Directional Light` | 场景光照                                    |


### 手势与四维编辑

| 手势 | 维度 | 效果 |
|------|------|------|
| 捏合 Pinch | 外观 Appearance | 打火机材质变为烧焦 |
| 画圈 Circle | 生命 Agency | 物体「活过来」（动画或呼吸缩放） |
| 挥动 Swipe | 规则 Rule | 进入躲闪模式，手靠近时物体逃离 |
| 响指 Snap | 解构 Deconstruction | 粒子特效 → 隐藏目标 → 生成花朵 |

`GestureManager` 统一调度手势，支持实验条件（`EnabledDimensions`）与全局冷却。`DataLogger` 自动写入 CSV 到设备存储。

### 实验数据导出

实验结束后，从头显导出日志：

```bash
adb pull /storage/emulated/0/Android/data/com.yn.picmagicmr/files/StudyLogs/ ./
```

或在 Unity Log 中查看 `[DataLogger] Session started` 输出的完整路径。

### 应标技术参数（StudySpec）

手势阈值、四维编辑行为、日志采样与资源路径统一在 `Assets/Scripts/MagicMR/StudySpec.cs`。上传资源或改场景前，请与此文件对照：

| 类别 | 参数 | 值 |
|------|------|-----|
| 捏合 Pinch | 拇指食指距离阈值 | 0.02 m |
| 挥动 Swipe | 手掌速度阈值 | 1.5 m/s |
| 规则 Rule | 躲闪触发距离 / 冲量 | 0.3 m / 5 |
| 解构 | 粒子后生成花延迟 | 0.5 s |
| 日志 | 手部轨迹采样 | 10 Hz（0.1 s） |
| 手势映射 | Pinch / Circle / Swipe / Snap | 外观 / 生命 / 规则 / 解构 |

资源包路径（`Assets/MagicMR/`）：

| 用途 | 路径 |
|------|------|
| 燃烧音效 | `Audio/BurnSizzle.wav` |
| 默认打火机材质 | `Materials/LighterDefault.mat` |
| 眼睛发光材质 | `Materials/EyeGlow.mat` |
| 花朵预制体 | `Prefabs/Flower.prefab` |
| 响指粒子（引用） | XRI 样例 `Confetti.prefab` |

Unity 菜单 **Magic Wand → Apply MagicMR StudySpec Parameters** 可将场景 Inspector 数值一键同步为应标默认值。

### 运行步骤

1. 确认 `Packages/manifest.json` 中 PICO SDK 路径正确
2. 打开 `MagicMR.unity`，在 `StudySystem` 上设置 `Subject Id`、`Condition`
3. Build And Run 到 PICO 头显（需开启手部追踪）
4. 对着 `Lighter` 做对应手势；日志自动记录

---

## 核心功能：3D → 2D 捕捉

适用于 `DesktopCaptureDemo` 与 `SampleScene`。

### RenderTexture 虚拟摄影棚

1. 3D 目标放在图层 `**3D_Target`**
2. `**CaptureCamera**` 只渲染该图层，输出到 `**CardTexture**`（RenderTexture）
3. UI 上的 `**2D_Card_UI`（RawImage）** 显示纹理，即「拍扁」后的卡牌

### 交互流程

```
点击 Cube（鼠标 / 后续可换为 Touch / 手势）
    ↓
Physics.Raycast 检测命中（标签 CaptureTarget）
    ↓
隐藏 3D 物体 → 显示 2D 卡牌
    ↓
等待 0.5 秒
    ↓
卡牌 Lerp 飞向背包并缩小
    ↓
隐藏卡牌
```

`CaptureAndFlyRoutine()` 协程负责动画；接入 AR 时主要替换输入层（`Mouse.current` → Touch / 手势），动画逻辑可复用。

---

## 项目结构

```
Assets/
├── CaptureTo2D.cs                 # 点击检测 + 卡牌飞背包动画
├── CaptureDemoBootstrap.cs        # 自动装配 CaptureCamera / Canvas / UI
├── SampleSceneDesktopPreview.cs   # SampleScene 编辑器预览（防黑屏）
├── Editor/
│   └── CaptureDemoSetup.cs        # Unity 菜单：一键生成 / 补全场景
├── Scenes/
│   ├── DesktopCaptureDemo.unity   # 桌面捕捉 Demo
│   ├── SampleScene.unity          # AR 图像追踪 + 捕捉
│   └── MagicMR.unity              # PICO MR 手势魔法（Android 主场景）
├── Scripts/MagicMR/
│   ├── RealityEditor.cs           # 四维现实编辑控制器
│   ├── GestureManager.cs          # 手势调度 + 实验条件
│   ├── DataLogger.cs              # CSV 实验数据记录
│   ├── MagicMRStudyBootstrap.cs   # 场景启动装配
│   ├── StudySpec.cs               # 应标技术参数（单一来源）
│   ├── HandGestureDetectorBase.cs # XR Hands 基类
│   ├── PinchGestureDetector.cs    # 捏合 → 外观
│   ├── SwipeGestureDetector.cs    # 挥动 → 规则
│   ├── CircleGestureDetector.cs   # 画圈 → 生命
│   └── SnapGestureDetector.cs     # 响指 → 解构
├── Cube.prefab                    # 可捕捉的 3D 目标预制体
├── CardTexture.renderTexture      # 卡牌渲染纹理
├── ReferenceImageLibrary.asset    # AR 参考图库
├── Backage_icon.png               # 背包图标
└── Resources/                     # 运行时备用资源
```

## 编辑器菜单

Unity 顶部菜单 **Magic Wand**：


| 菜单项                               | 作用                           |
| --------------------------------- | ---------------------------- |
| **Setup Desktop Capture Demo**    | 重新生成 `DesktopCaptureDemo` 场景 |
| **Add GameManager To Open Scene** | 给当前打开的场景补上 GameManager       |
| **Setup MagicMR VFX Assets**    | 装配 Lighter 上 RealityEditor 资源引用 |
| **Apply MagicMR StudySpec Parameters** | 将场景手势/启动参数同步为应标默认值 |


## 手动装配捕捉系统（可选）

1. **3D 目标**：创建 Cube，加 `BoxCollider`，图层 `3D_Target`，标签 `CaptureTarget`
2. **RenderTexture**：创建 `CardTexture`，新建 `CaptureCamera`，Culling Mask 只勾选 `3D_Target`，Target Texture 指向 `CardTexture`
3. **UI**：Canvas → `RawImage`（绑定 `CardTexture`）+ 右下角背包 `Image`
4. **脚本**：空物体 `GameManager` 挂载 `CaptureDemoBootstrap` + `CaptureTo2D`

## 推荐开发顺序

1. `**DesktopCaptureDemo`** — 电脑上跑通捕捉核心逻辑
2. `**SampleScene**` — 编辑器预览或 AR 手机测图像追踪 + 捕捉
3. `**MagicMR**` — PICO 头显测手势魔法与 MR 完整流程

