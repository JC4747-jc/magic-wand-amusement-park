# 桌面端「3D 拍扁成 2D 卡牌」演示

## 最快验证（推荐）

1. 在 Unity 中打开场景：`Assets/Scenes/DesktopCaptureDemo.unity`
2. 点击 **Play**
3. 用鼠标左键点击场景中的 **Cube**

预期效果：Cube 消失 → 屏幕中央出现 2D 卡牌 → 停顿 0.5 秒 → 卡牌缩小飞向右下角背包。

## 在现有 AR 场景（SampleScene）测试

1. 打开 `Assets/Scenes/SampleScene.unity`
2. 场景中已添加 **GameManager**（含 `CaptureDemoBootstrap` + `CaptureTo2D`）
3. Play 后点击名为 **Cube** 的物体（已移到相机前方 `z=2`，并打上 `CaptureTarget` 标签）

桌面模式下会自动关闭 AR Session，并禁用图像追踪组件，保留 XR 相机。

## 菜单工具（可选）

Unity 顶部菜单：**Magic Wand**

- **Setup Desktop Capture Demo**：重新生成桌面演示场景
- **Add GameManager To Open Scene**：给当前场景补上 GameManager

## 文件说明

| 文件 | 作用 |
|------|------|
| `CaptureTo2D.cs` | 点击检测 + 卡牌飞背包动画 |
| `CaptureDemoBootstrap.cs` | 自动创建 CaptureCamera、Canvas、卡牌 UI |
| `CardTexture.renderTexture` | 虚拟摄影棚输出纹理 |
| `Resources/` | 运行时备用资源路径 |
