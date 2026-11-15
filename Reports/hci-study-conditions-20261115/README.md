# HCI 条件与 Park 事件记录：本地候选

六类 Park 内容使用现有 ExperimentConditionSwitcher 的 C1 Baseline、C2 Appearance、C3 Appearance+Agency、C4 All 定义。施法的生产 Dispatch 入口检查条件，按钮/键盘调用同样受控；经 Dispatch 分派的额外调试魔法只允许 C4。Reset/Undo/Stop 不作为四维魔法禁止。切换条件取消手势、清除效果并释放未完成标定与表面；需要重新确认空间。房间 Awaken 转换为 Candy Reward 的已有行为也检查 D 权限，效果语义本身仍需按 HCI 修正。

Park 中 Ctrl+1–4 切换实验条件，普通 1–4 保留虚拟调试施法。测试使用临时 Input System 配置和合成键盘，调用实际快捷键处理器及 Park 输入映射，检查两者不会同时消费；不声称真实键盘/头显输入验证。初次夹具编译/输入路由失败摘要保留。

StartStudySession(subjectId, trialId) 是显式开启入口，不自动把预览变成实验。虚拟模式受试者 ID 加 SIM_ 前缀；已有活动会话不会被覆盖。DataLogger 保留原 subject/condition/trial CSV 格式，Park 动作记录目标、场景、虚拟/物理模式、来源、接受/拒绝原因；未测量速度与状态时长记 -1，不能视作指标结果。近目标且有效的共用手势采样接入原有限频轨迹记录。

本次保存场景进入 Play Mode 后，六类内容×四条件×四维共 96 次动作尝试通过；连同扩展动作、Reset 和一次 Baseline 手势，CSV 恰好 139 条动作结果。禁止动作不改变模型状态，Baseline 仍识别 A 但不应用魔法。合成 CSV 不是受试者数据。共用手势 24 条轨迹及六场景原流程另行回归通过。

未完成：Dispatch 以外的直接动画预览 API 条件审计、实际研究操作者的会话 UI、跨场景会话生命周期、完整选择/标定/追踪事件、带目标身份的轨迹 schema、真实 YOLO/PICO 及全部 32 项效果验收。此次未重新构建 Android、未推送新运行时代码，远程仍是 cba8ffb 已验证交付基线。此前报告保留当时源文件哈希，不能作为此次源码的完整验收。隔离工程仍含猫步态 v2 候选，和主工作区猫 FBX 不同。
