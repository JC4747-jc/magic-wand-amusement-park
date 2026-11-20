# 青铜兽当前完整动作：补充检查候选交付（2026-11-20）

父版本 restore-4d-lighter@001cdd6abd36e2bdb6fbab762e9277805acde18b，开发基线仍 codex/mr-development-snapshot-20261109@8359a0c。仅给有界启动器增加第十四个BronzePlayback入口，并附B补充摘要/状态、更新石像四项状态表及一幅已查看动作QA。现有Editor检查、18骨骼模型、动画/生产协程、识别链路/手势/资源/meta、火机均未改；traeye原分支只读，历史不改写。

在当前保存ParkStatue中实际Play Mode执行HciBronzePlaybackChecks.Run，49.85秒终态0、66次资源采样（最小空闲RAM7.69GiB、最大所属RSS1471.34MiB，仅采样范围）。真实生产B协程观察1331帧、334轨迹行，首周期5.717s、头位移约.071m、四脚最大漂移.000000179m；完整伸展/回位、根/拟合稳定、保留已有A/D、Undo、途中Stop/Reset/丢失、恢复和变体清理通过。193合成关节样本逐项匹配参照规则，7事件为A1/D1/B5，D四拳实际.16664s；生产规则未改。

六张原生图与动作QA已查看，能见身体伸展/抬头和回位。相机只框青铜主体，边缘花园/树被裁，不能证明花园完整构图；D另用上一轮当前StatueGarden证据。较早青铜真实播放证据仍有其限定价值，本次针对旧fixture中ParkActor/Bridge等差异补当前B，不把已有模型当成缺失资产。人体结果不单独证明青铜动作，本次是独立青铜当前场景证据。

便携摘要 [Reports/bronze-current-review-20261120/delivery-summary.json](Reports/bronze-current-review-20261120/delivery-summary.json)；B补充状态 [Reports/bronze-current-review-20261120/delivery-case-status.json](Reports/bronze-current-review-20261120/delivery-case-status.json)；更新的石像四项状态 [Reports/statue-current-review-20261120/delivery-case-status.json](Reports/statue-current-review-20261120/delivery-case-status.json)。B本地人体/青铜均有证据，所有项真实输入/PICO/最终美术仍未完成。完整原图/轨迹/关节CSV/资源日志留本地，QA和哈希随摘要交付。

沿用已核Android包 PlantPresentationValidation-20261120.apk，78064133B，SHA256 f3c8553d646f9871631dd023da4eafb45c3d3719cb4857965afaa1505f40fe31；15场景/CRC/签名/zipalign4/16个ARM64已核。仅启动器输入改变，玩家运行时/资产/包配置不变，未重复构建/上传APK，静态包检查不证明PICO运行。

SOURCE_VALIDATION.sourcePayloadTree为完整候选树去掉元数据自身；payloadFiles为已核范围，其余继承内容不新增验收。独立索引显式八项、JC4747-jc身份，实际远端提交/文件哈希由单独回执记录。

尚未完成：真实YOLO→实际HCI手势→效果、PICO双眼/读距/性能/长期生命周期、最终美术、独立采集/非火机模型质量与完整8案例32效果。头显暂不连接，不自动定时提交。后续先测柳树已有金币的可见性/遮挡再决定是否优化树专属几何，火机继续冻结。
