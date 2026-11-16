# HCI 魔法乐园：模型与动画增量 Android 验证包

本包整合已验证的宠物漫画描边、石像卡通/荷花许愿树/全身伸展、出租屋 A 水彩/赛博、B 落地窗、C 水母灯、D 糖果雨。各入口继续共用火机的 YOLO 目标入口和 A/B/C/D 手势；包内类型存在与编辑器模拟不等于真实识别通过。

## 通过范围

- Unity 6000.0.33f1 单任务 Android IL2CPP/Gradle 构建；14 个场景、ARM64、签名、ZIP CRC 和既有 40 项模型资源来源核对。
- 额外核对 4 个新增 shader、全身石像 prefab/FBX/材质；APK 元数据含共用手势与各新增效果类型。详见 increment-package-check.json。
- 构建前完整模型/交互预检查通过。修正旧检查把描边 shell 计为原猫狗网格的问题，保留猫 8646 面/40 骨骼和狗 849 面/47 骨骼约束，新增 shell 共享网格/骨骼与默认隐藏校验。房间 Stop 检查由固定数量改为准确匹配窗口、原效果与可见粒子集合。
- 1625 个 Assets/Packages/ProjectSettings 源文件与 sourceTree 对照一致。运行时代码对应此前已接受增量，本轮四个源文件修改均为 Editor 检查。

## 资源控制与中间失败

重任务串行，单个原生编译进程，RAM 可用下限 3 GiB、项目盘余量 20 GiB、每步增长预算 1 GiB、日志 16 MiB、每步 20 分钟。先准备 DAG、关闭 Unity，再离线 IL2CPP，最后启动 Unity 完整构建。遥测为五秒采样，不是连续峰值。未运行训练或下载大模型，未重启。

前两次旧检查冲突已修正。第一次完整编译虽生成 APK，但 BuildReport 缺少资源来源清单，未作为交付。仅将有界 PlayerDataCache 移入本地隔离目录后重打包，保留原生编译缓存及 Library 其余部分；最终清单绑定本次 APK SHA256。历史失败记录明确保留。

## 必须继续完成

应用标识 com.yn.picmagicmr.validation，是包含 14 场景的编译验证包，不代表所有场景都有生产导航入口。尚未完成 PICO 安装运行、真实相机 YOLO、真实手势、定位/遮挡、双眼及性能验收；没有新增合格 YOLO 权重。八用例 32 效果的缺项与美术完善、模型训练/大模型下载接入，继续按 BRANCH_SCENE_PLAN.md 的 Goal 执行，不能凭本包宣布完成。

已发布 [Android 动画增量验证包](https://github.com/JC4747-jc/magic-wand-amusement-park/releases/tag/hci-magic-effects-validation-20261116)；上传回执见 [release-receipt.json](release-receipt.json)，服务端摘要与本地一致。发布仍为预览版本，PICO 真机未验收。
