# 当前源码整合候选（2026-11-20）

本候选以 restore-4d-lighter@4250bad 为交付父版本，生产源码来自当前 JC 工作区；原始开发基线是 codex/mr-development-snapshot-20261109@8359a0c。原分支没有改写。火机场景、模型、效果与原 YOLO 权重保持既有冻结版本，不训练火机。

## 本候选内容

- Assets/Scenes 包含 15 个保存场景，包含计划要求的 14 个场景；真实 YOLO 生产入口仍为 BridgeStereoFusion。MagicMR/合成输入不代替真实链路验收。
- XRI 示例由 3.0.5 迁至 3.0.7，保留匹配 GUID，移除 309 个旧示例路径或容器元数据。其他 14 个远端旧源码/meta 保留，其中两个非火机测试按实际 HCI 房间要求修正。
- 房间 A 为水彩/赛博，B 为落地窗，C 为水母灯，D 为糖果雨。旧测试中 A 先选糖果的前提已移除；测试要求 D 保持 A 样式、效果独立共存、Stop/Reset 回收，仍要求确认平面。依据 pico-hci-yolo-vfx@eae2686 的 HciMeetingScenario.cs 第 50 行及当前生产实现。
- 所有线路继续要求真实 YOLO 选目标，再按核实过的 HCI 实际手势要求触发对应魔法。是否共用火机手势以实际效果与验证为准。

## 在另一工作区打开

使用 Unity Hub 将完整项目作为工程打开，编辑器版本见 ProjectSettings/ProjectVersion.txt。整个仓库不是 Package Manager 可直接安装的 UPM 包。

先按 LOCAL_DEPENDENCIES.md 用 PowerShell 7.2+ 连接已有 PICO SDK 3.4.0 和 Unity MCP 10.3.0，校验完整内容指纹。SDK 本体、模型缓存和本机虚拟环境不复制进交付。PicoRuntimeRequirements.json 和 Editor 守卫会核对 SDK 版本/GUID，确保冷导入未把手部追踪与 VST 静默关闭；不修改 SDK 源码。

## 支持的本地检查与构建

- Unity Test Runner：PlayMode 下运行 MagicMR.ParkInteractionTests；修正后的 6 项已通过，覆盖贴合/定向/平面确认、虫子奖励、宠物丢失取消和房间 A/D 独立及禁用清理。
- ParkPlayModeChecks.Run：六场景真实协程、动作完成/锚点和 Reset，房间 A/B/C/D 共存/回收；修正后的入口已在有界宿主通过。其他 Run* 回放入口本轮未执行，不能沿用旧记录宣称当前通过。
- Android 使用 Tools/build_android_bounded.ps1 -Variant AllScenes -BuildJobs 1，并按对应工具检查 APK。执行前保存并关闭其他 Unity，检查 RAM/磁盘，一次只运行一个重任务。设置 SDK/NDK/JDK 路径时使用脚本参数，不修改火机。
- 对应包检查使用 Tools/verify_android_artifacts.py --unity-data <Unity的Data目录> --project-dir <当前项目目录> --variant AllScenes --require-insect-pixels --output <新的报告路径>。

现有 16 个显式交付工具覆盖本地包引导、Android、F2G 有界启动及相关核对；其余父版本 Tools/历史报告原样保留，不表示全部适配或重新验收。修改过或新增但未纳入已验证显式清单的本机工具不自动上传。

## 验收边界

原先 APK 6aa0c952... 对应添加旧源码前的 1836 文件快照，不能充当当前源码树的 Android 证据。当前树的 Android 与最终文件清单须在对应 SOURCE_VALIDATION.json 中另行记录；尚无该报告时保持待验收。

本地编译、合成输入协程、人工看图、APK 检查和 PICO 真机结果分开记录。PICO 安装/启动、真实相机同步、正例 YOLO 姿态与手势端到端、独立数据准确率以及 8 案例/32 效果的最终验收仍未完成。当前候选不能标成正式发布或完整设备通过版本。用户暂不连接头显，不自动重启或终止用户 Unity。
