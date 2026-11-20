# 像素虫当前 B：四组合原生检查候选（2026-11-20）

父版本 restore-4d-lighter@86ea6455499612bb11cede6f00d3a0eba5e8ac3d，开发基线仍 codex/mr-development-snapshot-20261109@8359a0c。仅扩展HciBugDanceChecks Editor检查和十五模式有界启动器，并附摘要、四项范围表与两QA图。生产手势/识别链路/运行时、虫模型/骨骼/贴图/场景/meta及火机未改；traeye原分支只读，历史不改写。

当前保存ParkBugs，蟑螂/蜘蛛×自然/像素四组合，每组合一次完整B和一次中途Reset：280条带真实游戏时间戳的合成画圈样本经生产ProcessHandSample，逐次匹配参照规则，八次Agency各只触发一次。357轨迹行仅有x=-.04/0/.04、y/z和识别锚点零漂；原生产配置为.7秒舞段+.3秒引导，实测舞段约.700–.717秒、每组合6次切换/54–55次保持，IK最大.0001303777小于原.0008阈值（rig局部单位，不是硬件姿态误差）。外观、完整回位/Undo、Busy中Undo拒绝和立即Reset均通过。

v3终态0、41.35秒/55次资源采样，最小空闲RAM7.35GiB、最大所属RSS1548.70MiB（采样不是连续峰值）。稳定外观下16张真实原生图及两QA已查看，pixel前置_style>1.99和Pixel网格在B中保持。Pixel前置A通过动作回调设置，不是实际A手势验收；B是合成关节入口，不是物理手/相机/YOLO输入。

保留修正记录：v1新增的12切换门槛不符合既有六段生产配置而失败，恢复原>=4观察下限，保留新增完整时长/双向/IK/八单事件条件，不改生产。v2动作检查通过但before图捕在A材质插值中；v3采用现有Pixel检查的_style>1.99完成条件再截图，并记录属性值，未改生产材质。

A像素蒙皮/首次切换与恢复，C眩晕/D虚拟金币的既有原生范围限定复用：两旧Editor哈希匹配，78虫资源与虫核心runtime匹配，差异为其他案例partial与非火机选择。完整原始报告/日志/图片仅留本地；便携摘要内保留来源哈希/范围，不宣称全工程、包配置或硬件完全一致。B旧live准备另有骨骼/Actor/虫partial变化，故本次补当前B而非原样沿用旧pass。

便携摘要 [Reports/bug-dance-current-review-20261120/delivery-summary.json](Reports/bug-dance-current-review-20261120/delivery-summary.json)；A/B/C/D范围与未完成状态 [Reports/bug-dance-current-review-20261120/delivery-case-status.json](Reports/bug-dance-current-review-20261120/delivery-case-status.json)。两QA哈希见摘要；完整原图/关节CSV/轨迹/失败记录保留本地。

沿用已核Android包 PlantPresentationValidation-20261120.apk，78064133B，SHA256 f3c8553d646f9871631dd023da4eafb45c3d3719cb4857965afaa1505f40fe31；15场景/CRC/签名/zipalign4/16个ARM64已核。仅Editor/启动器变化，玩家运行时/资产/包配置未变，不重建/上传APK；静态包检查不证明设备运行。

SOURCE_VALIDATION.sourcePayloadTree为完整候选树去掉元数据自身；payloadFiles只是已核范围，继承内容不新增验收。独立索引显式九项、JC4747-jc身份，实际提交/远端文件哈希见独立回执。真实YOLO→实际HCI手势→效果、PICO透视中真实昆虫仍可见/双眼/性能、最终美术、独立采集/非火机模型质量与完整8案例32效果仍待验；头显暂不连接，不自动定时提交。
