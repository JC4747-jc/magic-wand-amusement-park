# Magic Wand Amusement Park

Unity AR 互动项目：在 AR 场景中识别 3D 目标，将其「拍扁」成 2D 卡牌，再飞入背包。当前已实现**电脑端核心逻辑 Demo**，便于在脱离 AR 环境的情况下快速迭代。

## 技术栈

| 项目 | 版本 / 说明 |
|------|-------------|
| Unity | `6000.0.26f1` |
| 渲染管线 | URP（Universal Render Pipeline） |
| AR | AR Foundation `6.0.7`、ARCore、ARKit |
| XR | OpenXR、PICO SDK（PXR）、Mock HMD |
| UI | uGUI |

## 环境要求

- [Unity Hub](https://unity.com/download) + Unity **6000.0.26f1**
- 桌面端 Demo：Mac / Windows 均可
- AR / 手机打包：需配置 Android / iOS 构建支持
- MagicMR / 手势魔法：需 PICO 头显及本机 PICO SDK

> **注意**：`Packages/manifest.json` 中引用了本地 PICO SDK 路径。若本机没有该目录，打开项目时 Package Manager 可能报错，需改成本机实际路径。

## 快速开始（桌面 Demo，推荐）

1. 用 Unity 打开本项目
2. 打开场景 **`Assets/Scenes/DesktopCaptureDemo.unity`**
3. 点击 **Play**
4. 用鼠标左键点击场景中的 **Cube**

**预期效果：**

- Cube 瞬间消失
- 屏幕中央弹出带 Cube 图像的 2D 卡牌
- 停顿 0.5 秒
- 卡牌缩小并飞向右下角背包

## 在 AR 场景中测试

1. 打开 **`Assets/Scenes/SampleScene.unity`**
2. 点击 **Play**
3. 点击场景中的 **Cube**（标签 `CaptureTarget`）

编辑器 Play 时会自动启用 **`SampleSceneDesktopPreview`**，切换为 Skybox 预览，避免 AR 黑屏。

## 核心功能说明

### 3D → 2D：RenderTexture 虚拟摄影棚

1. 3D 目标放在图层 **`3D_Target`**
2. **`CaptureCamera`** 只渲染该图层，输出到 **`CardTexture`**（RenderTexture）
3. UI 上的 **`2D_Card_UI`（RawImage）** 显示这张纹理，即「拍扁」后的卡牌

### 交互流程

```
鼠标点击 Cube
    ↓
Physics.Raycast 检测命中
    ↓
隐藏 3D 物体 → 显示 2D 卡牌
    ↓
等待 0.5 秒
    ↓
卡牌 Lerp 飞向背包并缩小
    ↓
隐藏卡牌，输出埋点日志
```

## 项目结构

```
Assets/
├── CaptureTo2D.cs              # 点击检测 + 卡牌飞背包动画
├── CaptureDemoBootstrap.cs     # 自动装配 CaptureCamera / Canvas / UI
├── Editor/
│   └── CaptureDemoSetup.cs     # Unity 菜单：一键生成 / 补全场景
├── Scenes/
│   ├── DesktopCaptureDemo.unity   # 纯桌面演示（推荐入门）
│   ├── SampleScene.unity          # AR 图像追踪 + 捕捉 Demo
│   └── MagicMR.unity              # PICO MR 主场景（手势魔法）
├── Scripts/MagicMR/            # 手势检测（Pinch / Swipe / Circle / Snap）
├── SampleSceneDesktopPreview.cs # SampleScene 编辑器预览（防黑屏）
├── Cube.prefab                 # 可捕捉的 3D 目标预制体
├── CardTexture.renderTexture   # 卡牌渲染纹理
├── Backage_icon.png            # 背包图标
└── Resources/                  # 运行时备用资源
```

## 编辑器菜单

Unity 顶部菜单 **Magic Wand**：

| 菜单项 | 作用 |
|--------|------|
| **Setup Desktop Capture Demo** | 重新生成桌面演示场景 |
| **Add GameManager To Open Scene** | 给当前打开的场景补上 GameManager |

## 手动装配（可选）

1. **3D 目标**：创建 Cube，加 `BoxCollider`，图层设为 `3D_Target`，标签设为 `CaptureTarget`
2. **RenderTexture**：创建 `CardTexture`，新建 `CaptureCamera`，Culling Mask 只勾选 `3D_Target`，Target Texture 指向 `CardTexture`
3. **UI**：Canvas → `RawImage`（绑定 `CardTexture`）+ 右下角背包 `Image`
4. **脚本**：空物体 `GameManager` 挂载 `CaptureDemoBootstrap` + `CaptureTo2D`

## 后续接入 AR / 手势

电脑端 Demo 跑通后，接入 AR 时主要改输入层，动画逻辑可复用：

| 当前（桌面） | 后续（AR / 手机） |
|-------------|------------------|
| `Mouse.current`（新 Input System） | Touch / 手势信号 |
| `Camera.main.ScreenPointToRay` | AR Foundation 射线 |

`CaptureAndFlyRoutine()` 协程内的 3D→2D→背包动画无需修改。对于 AR 动态生成的 Cube，建议用 **`CaptureTarget` 标签**或 **`3D_Target` 图层**匹配。
