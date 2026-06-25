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
- 桌面端 Demo：Mac / Windows 均可，**无需真机、无需 PICO 4**
- AR / 手机打包：需配置 Android / iOS 构建支持
- MagicMR / 手势魔法：需 **PICO 4**（或同类 PICO 头显）及本机 PICO SDK

> **注意**：`Packages/manifest.json` 中引用了本地 PICO SDK 路径（`file:/Users/shensiqi/Downloads/...`）。若你本机没有该目录，打开项目时 Package Manager 可能报错，需删除或改成本机实际路径。

## 使用要求：是否需要 PICO 4？

**不是所有功能都需要 PICO 4。** 按你要测的内容选择场景和设备：

| 你要做什么 | 是否需要 PICO 4 | 推荐场景 | 操作方式 |
|-----------|----------------|---------|---------|
| 3D → 2D 卡牌 → 飞背包（核心逻辑） | **不需要** | `DesktopCaptureDemo.unity` | 电脑 Play，**鼠标左键**点 Cube |
| AR 场景结构 + 捕捉逻辑预览 | **不需要** | `SampleScene.unity` | 电脑 Play（自动切换 Skybox 预览，避免 AR 黑屏） |
| AR 图像追踪（认参考图 `i.jpg`） | **不需要 PICO 4** | `SampleScene.unity` | 支持 ARCore / ARKit 的 **Android / iPhone** 真机 |
| 手势施法（捏合 / 滑动 / 画圈 / 响指） | **需要** | `MagicMR.unity` | PICO 4 头显 + 手部追踪 |
| 完整 MR / VR 体验（当前打包主场景） | **需要** | `MagicMR.unity` | PICO 4 + PICO SDK |

**建议开发顺序：**

1. 没有 PICO 4 → 先用 **`DesktopCaptureDemo`** 在电脑上跑通「拍扁 + 飞背包」
2. 有 AR 手机 → 测 **`SampleScene`** 的图像追踪
3. 有 PICO 4 → 再测 **`MagicMR`** 的手势与 MR 完整流程

## 识别方案说明

本项目**未使用 YOLO**，也**未接入 MediaPipe**（教程中为后续计划）。当前识别方式：

| 识别目标 | 方案 |
|---------|------|
| 平面标记图 | AR Foundation 图像追踪 + `ReferenceImageLibrary` |
| 手部手势 | PICO 手部追踪 → Unity XR Hands → 自研关节规则（`Assets/Scripts/MagicMR/`） |
| 点击 / 捕捉 3D 物体 | 相机射线 + `Physics.Raycast` + `CaptureTarget` 标签 |

## 快速开始（桌面 Demo，推荐）

这是最快验证「点击 3D → 变 2D 卡牌 → 飞背包」全流程的方式。

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
3. 点击场景中的 **Cube**（位于相机前方，标签 `CaptureTarget`）

编辑器 Play 时会自动启用 **`SampleSceneDesktopPreview`**：关闭 AR 黑屏背景，切换为 Skybox，方便没有 AR 设备时在电脑上预览。

真机 AR 图像追踪需将 App 打到支持 ARCore / ARKit 的手机上测试。

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
│   ├── DesktopCaptureDemo.unity   # 纯桌面演示（推荐入门，无需 PICO 4）
│   ├── SampleScene.unity          # AR 图像追踪 + 捕捉 Demo
│   └── MagicMR.unity              # PICO MR 主场景（手势魔法，需 PICO 4）
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

若需从零搭建，可按以下步骤：

1. **3D 目标**：创建 Cube，加 `BoxCollider`，图层设为 `3D_Target`，标签设为 `CaptureTarget`
2. **RenderTexture**：创建 `CardTexture`，新建 `CaptureCamera`，Culling Mask 只勾选 `3D_Target`，Target Texture 指向 `CardTexture`
3. **UI**：Canvas → `RawImage`（绑定 `CardTexture`）+ 右下角背包 `Image`
4. **脚本**：空物体 `GameManager` 挂载 `CaptureDemoBootstrap` + `CaptureTo2D`，或在 Inspector 中手动拖引用

## 后续接入 AR / 手势

电脑端 Demo 跑通后，接入 AR 时**主要改输入层**，动画逻辑可复用：

| 当前（桌面） | 后续（AR / 手机） |
|-------------|------------------|
| `Mouse.current`（新 Input System） | MediaPipe 手势信号 / Touch |
| `Camera.main.ScreenPointToRay` | AR Foundation 射线 |

`CaptureAndFlyRoutine()` 协程内的 3D→2D→背包动画**无需修改**。

对于 AR 动态生成的 Cube，建议用 **`CaptureTarget` 标签**或 **`3D_Target` 图层**匹配，而不是硬编码单个 GameObject 引用。

## 常见问题

**Play 后点击 Cube 没反应？**

- 确认打开的是 `DesktopCaptureDemo.unity`，或场景中存在 `GameManager`
- 确认 Cube 有 `BoxCollider`，且标签为 `CaptureTarget`
- 在 **Game 窗口**用**左键**点击（不是 Scene 视图）
- 查看 Console 是否有 `[CaptureTo2D]` 或 Input System 相关报错
- 等待 Unity 脚本编译完成后再 Play

**SampleScene 一打开就黑屏？**

- AR 场景默认背景为黑色（等待摄像头画面），编辑器里没有摄像头会全黑
- 点 **Play** 后 `SampleSceneDesktopPreview` 会自动切换为 Skybox 预览
- 若仍黑屏，优先用 **`DesktopCaptureDemo`** 测核心逻辑

**卡牌是空白 / 没有 Cube 图像？**

- 检查 `CaptureCamera` 是否对准 Cube
- 检查 `CaptureCamera` 的 Culling Mask 是否包含 `3D_Target`
- 检查 `2D_Card_UI` 的 Texture 是否绑定了 `CardTexture`

**第二次捕捉卡牌不显示？**

- 已在 `CaptureTo2D` 中处理：动画结束后会恢复卡牌缩放；若 3D 物体已被隐藏，需自行实现「重置关卡」逻辑

**PICO SDK 报错？**

- 修改 `Packages/manifest.json` 中 `com.unity.xr.picoxr` 的路径，或暂时移除该依赖

## 团队协作（GitHub）

项目已配置 `.gitignore`，**不要**把 `Library/`、`Temp/`、`Logs/` 等提交到仓库。

### 第一次：负责人创建远程仓库

1. 在 [GitHub](https://github.com/new) 新建 **Private** 仓库（例如 `magic-wand-amusement-park`）
2. **不要**勾选 "Add a README"（本地已有）
3. 在本项目根目录执行：

```bash
cd "/Users/shensiqi/Magic Wand Amusement Park"

git init
git add .
git commit -m "Initial commit: Magic Wand Amusement Park Unity project"
git branch -M main
git remote add origin https://github.com/你的用户名/仓库名.git
git push -u origin main
```

### 队友加入

```bash
git clone https://github.com/你的用户名/仓库名.git
```

Unity Hub → **Add** → 选择克隆下来的文件夹 → 用 **Unity 6000.0.26f1** 打开。

### 日常协作

```bash
# 开工前
git pull

# 改完后
git add .
git commit -m "描述你改了什么"
git push
```

### 协作约定

| 事项 | 说明 |
|------|------|
| Unity 版本 | 全员统一 **6000.0.26f1** |
| PICO SDK | `Packages/manifest.json` 里是本地路径，每人需改成自己机器上的 SDK 路径 |
| 场景分工 | 尽量避免多人同时改同一个 `.unity` 文件（易冲突） |
| 无 PICO 同学 | 负责 `DesktopCaptureDemo` / 脚本；有 PICO 的同学负责 `MagicMR` 真机 |
| 大文件 | 单文件 >100MB 需用 [Git LFS](https://git-lfs.github.com/)，或放网盘并在 README 注明 |

### 可选：Unity 编辑器内查看 Git

安装 **GitHub for Unity** 或使用 Cursor / VS Code 的 Git 面板，效果与命令行相同。

## 相关文档

- 桌面 Demo 补充说明：`Assets/DESKTOP_CAPTURE_DEMO.md`

## License

内部项目，暂未指定开源协议。
