# 原入口启动与捕获流程：有范围的源码候选（2026-11-20）

父版本 restore-4d-lighter@943e87faecda9f6f0fc33e2ae1a75c69ea89bf67；开发基线仍 codex/mr-development-snapshot-20261109@8359a0c。仅修改 CaptureDemoTimelineChecks 编辑器检查及有界启动器，并交付三个证据摘要、缺口表 v3 与三张实际 Unity 图。原场景、生产运行时、识别/手势、模型、meta 和火机完全不改。traeye 分支只读，旧提交不改写；本次提交使用 JC4747-jc。

八个原入口各自原相机启动记录为独立的既有当前输入：8 场景零错误，图已逐张查看；Bridge 四入口各有 PC 连接超时，Sample 有四项 Windows XR/AR provider 警告，全部没有真实相机帧。未执行维护、升级或效果注入。黑背景/UI 不证明 YOLO 或 PICO 成功。合成图仅把实际捕获并排展示，不替代运行模型。

两个捕获场景在同一新版输入分别原生 Play：SampleScene 及 DesktopCaptureDemo 终态均为 0。绑定、实际非空截图和完整目标边界、重复拦截、中途复位/重用、timeScale=0 下完成、禁用/启用与换目标清理通过，复位后背包仍显示。Desktop 调用原场景真实复位按钮回调；Sample 没有该按钮，调用既有 ResetCapture API，不主张按钮/键盘/AR 验收。

修正的是检查脚本的场景假设：Sample 卡片直接位于共享 Canvas 下，不能把父 Canvas 当卡片并要求其隐藏。现在检查实际 RawImage.activeInHierarchy。保留 v1 按钮缺失、v2 Canvas 假设及 v3 一处漏引用造成的 CS0103 失败；v4 两场景通过。没有为测试添加生产 UI 或更换入口。两张卡片图已查看，仍只是灰立方体，不是最终美术。

- [原八入口摘要](Reports/scene-evidence-audit-20261120/delivery-summary.json)：原始输入版本及范围保持，不把本次新 Editor 哈希套用到旧执行。
- [两捕获场景摘要](Reports/sample-capture-review-20261120/delivery-summary.json)：各自来源输入/资源采样/截图 SHA 和 API 与按钮的区别。
- [Android 复核摘要](Reports/android-close-resume-20261120/delivery-summary.json)：与构建输入相比只有六个 Editor 检查和启动器变化，玩家输入不变。
- [场景及 32 效果缺口表](Reports/delivery-gap-index-20261120/CASE_SCENE_STATUS-v3.md) 与 [结构化 v3](Reports/delivery-gap-index-20261120/inventory-v3.json)。v1/v2 原记录仍在本地保留；未发布的原始日志路径仅为本地交接引用。

Android 沿用 PlantPresentationValidation-20261120.apk（78064133 字节，SHA256 f3c8553d646f9871631dd023da4eafb45c3d3719cb4857965afaa1505f40fe31）；签名、CRC、zipalign4、15 场景、16 个 ARM64 ELF、min29/target35 再核通过。没有重复重建或上传 APK。打包 15 场景不等于把森林研究资产扩为正式项目；ForestBlockout/ForestStudy 继续按计划仅供选择性复用。

所有本地范围不能替代真实 YOLO→实际 HCI 手势→魔法、PICO VST/双眼/遮挡/性能、独立采集/模型质量或最终美术验收。火机四效果仍不因启动成功而判完整通过。用户暂不接设备；无定时四小时提交。完整 Goal 未完成。

SOURCE_VALIDATION.sourcePayloadTree 是完整候选树排除元数据自身；payloadFiles 只描述已核范围，继承内容不会自动获得新增验收。显式清单、来源/产物 SHA、身份与远端文件字节核验见独立发布回执；不动主索引或当前分支。
