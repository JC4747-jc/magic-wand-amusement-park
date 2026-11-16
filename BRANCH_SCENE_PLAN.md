# 全分支整合与场景交付计划

## 语音与火机扑翼 Android 交付（2026-11-16）

植物离线语音/嘴部包络和火机扑翼/定时咳嗽烟已进入新 Android 包，1635 个项目源文件核对一致，14 场景、签名/CRC/ARM64、资源及类型检查通过。见 [构建证据](Reports/hci-android-voice-wings-20261116/README.md)。旧章节中的未进入 APK 保留为当时状态。真实 YOLO/PICO、声音听感、美术精修及其他 HCI 缺项仍必须完成，Goal 继续 active。

## HCI 火机 B 翅膀与咳嗽烟增量（2026-11-16）

B 画圈沿用现有 Agency 入口，加入原创折面扑翼、定时小烟团和缓存的合成双段咳嗽声，修正非等比缩放及眼睛贴合/前后侧位置。实际火机网格的旋转/缩放、重复 B、A/C/D 切换、复位、暂停、隐藏清理通过；共用火机规则和原八场景动作回归通过。见 [扑翼与咳嗽报告](Reports/hci-lighter-agency-20261116/README.md)。本轮未生成新 APK，翼膜细节、真实 YOLO/手势闭环、双眼/遮挡与听感仍待完善验收；不能视为全部 HCI 效果完成。Goal 继续 active。

## 火机录制图像回放与独立划分缺口（2026-11-16）

当前 v6 权重对 14 张历史 PICO 命名 JPEG 的真实 TCP 推理、PICF v1/v2 与重连共 15 请求通过。审核发现 test 0056/0093 与训练相邻帧高度相似，逐图检查确认同一场景/动作近似；原始训练来源也未完全恢复，因此不能将 14/14 诊断结果视为独立准确率。137 张旧导出图列入保守历史开发组，保留旧文件，补充拍摄场次与标签审核后再定义新模型的独立测试。见 [回放与数据划分报告](Reports/hci-lighter-recording-replay-20261116/README.md)。本轮无下载/训练/部署改变，真实 PICO 全线路和美术目标继续 active。

## HCI 桌面植物 B 离线语音增量（2026-11-16）

植物 B 已在现有拟人字幕与表情上加入约 2.57 秒离线英文示范语音，复用一个空间 AudioSource；嘴部按 PCM 响度包络开合，播放结束闭嘴。保存场景中的重播、撤销、D 清除、复位、暂停、追踪丢失/恢复和禁用清理通过，植物完整流程、24 条共用手势及 96 次条件回归通过。见 [语音播放与渲染证据](Reports/hci-plant-voice-20261116/README.md)。本轮为编辑器增量，未更新 APK，PICO 扬声器听感/真实 YOLO/真手势尚未验收；英文声音与表情仍需美术完善。全部 Goal 保持 active。

## YOLO 预训练诊断与数据缺口（2026-11-16）

已对现有 yolov8n/yolo26n 各执行 41 张公开候选图的离线推理，并逐图查看四个失败/歧义例。两者类别覆盖项目 7 类，仍缺 Lighter/Tree/Cockroach/Spider/Statue/BronzeBeast 六类；有重叠猫合并、家具分类混淆与强裁切盆栽遗漏，不能仅更换权重宣布全线路完成。标注未全部批准，诊断匹配数不作 mAP/真实 PICO 准确率。见 [对比与审核记录](Reports/hci-pretrained-comparison-20261116/README.md)。本轮无下载/训练/部署改动；后续继续标签审核、独立划分、必要训练和 PICO 实测，美术完善仍须完成。

## Android 模型动画增量已打包（2026-11-16）

新版 `HciMagicEffectsValidation-20261116.apk` 含此前已接受的宠物描边、石像卡通/全身伸展/荷花许愿树和房间 A/B/C/D 修正。14 场景、签名/CRC/ARM64、40 项既有资源与 7 项新增资源来源核对通过；构建前模型和交互回归通过，修正四个旧检查对描边层及窗口退出集合的过时假设。证据见 [Android 增量报告](Reports/hci-android-effects-20261116/README.md)。没有训练新权重或完成真机验收。下面旧增量中的“未进入 APK”是当时状态，以本次打包范围为准；真实 YOLO/PICO、美术、全部 32 效果及模型任务继续 active。

## Goal 必需完成条件：真实 YOLO/PICO 与美术完善（2026-11-16 用户补充）

### 模型训练与大模型下载（用户追加授权）

将训练模型、下载和接入适合项目的大模型纳入本 Goal 的执行范围，覆盖 YOLO 实物识别以及有明确缺口的三维资产/动画生成；不再把历史目标中的“默认不训练”作为拒绝执行的理由。先核对现有权重与效果，再按魔法乐园实际任务选择模型、固定版本与来源，完成所需下载、独立环境配置、训练或推理、质量评估与 Unity 接入。不能仅下载权重就标记为完成，也不以参数量越大越好代替效果比较。

每次任务先记录所需 RAM/VRAM、下载与解压/缓存/检查点预计占用和实际盘余量；一次只运行一个训练或构建重任务，复用已有文件，启用有限并发、有限日志与检查点保留策略，保护已验证的最佳权重与恢复点。资源不够时选择有依据的较小/量化配置或记录外部算力缺口，不填满硬盘、不降低既有保护下限、不保证硬件绝对不会损坏。第三方付费服务仍须有可用账户和明确预算，不因本条自动开通收费服务。当前 Android 编译完成后，再推进模型任务。

真实 YOLO/PICO 闭环和八用例的美术完善都是本 Goal 的必做项，不能仅列为交接建议后宣布完成。继续完成已授权的 YOLO 数据审核、必要训练、独立验证与部署；逐项记录目标类别、权重版本、置信度与漏检/误检、定位稳定性、手势触发、恢复和撤销。全部正式线路必须在 PICO 相机识别真实物体后，用真实手部输入触发对应魔法；模拟目标、合成轨迹和 APK 打包只能证明各自范围。

手势沿用当前 README「统一交互线路」及火机操作表：A 捏合后松开，B 指尖画圈，C 张掌横挥，D 握拳后张开。复用既有 `TabletopHandSample`、`TabletopGestureRules`、`GestureReachWindow` 和 HCI 条件/日志调度，不另造对象专用规则。历史 HCI 分支 `eae26868` README 仍写 D 为 Snap、旧捏合/挥动阈值；这与用户本次明确的 D 和当前火机实现不同，保留为历史资料，不回退生产输入。HCI 的 A/B/C/D 维度与八用例效果要求继续保留。

- 真机完成证据：各用例真实目标与 A/B/C/D 的录像或逐项记录、目标丢失与恢复、误触/重复触发、UI/标定输入仲裁、双眼位置/遮挡、帧率和内存数据。没有设备结果的项保持未验收；需要安装运行时通知用户连接 PICO。
- 美术完成证据：八用例 32 个效果逐项对照 HCI 需求，完善模型比例、原始材质、动作衔接、穿插/漂浮、风格辨识和反馈；保存实际 Unity 前后对比及 PICO 观察。参考图与工具服务魔法乐园，不能用概念图或通用森林替代；目前青铜伸展、讲史/语音及其他未补效果仍须完成。
- 资源约束：构建与训练串行，启动前检查余量；保留内存/磁盘/日志守卫，限制训练缓存和检查点，保留已验证权重与必要诊断。GitHub 仅交付通过对应检查的源码和产物，明确编辑器、Android 静态检查与真机验收的不同范围。

本节追加完成标准，不缩减全分支整合、Unity/PICO/F2G、YOLO 训练及全部场景目标；Goal 继续 active。

## HCI 出租屋四维映射与组合动画增量（2026-11-16）

按仓库 HciMeetingScenario 的 Bedroom 要求修正房间映射：A 水彩/赛博，B 展开虚拟落地窗，C 水母灯，D 确认天花板后糖果雨。移除 A 选择水母/糖果、B 根据 A 转为 D 的旧流程；四维独立共存且沿用火机识别规则。保存 ParkRoom 的合成关节播放、窗口展开、水母/糖果持续、撤销/停止/复位/追踪丢失恢复、原材质恢复和资源复用通过，共用手势/实验条件回归通过。见 [房间验证报告](Reports/hci-room-magic-20261116/README.md)。这只是虚拟房间模型：真实家具 YOLO、房间几何与遮挡、PICO 双眼/帧率仍需设备，水彩/窗景美术也需完善；未更新 APK。以下旧映射描述是历史状态。全部八用例 32 效果 Goal 继续 active。

## HCI 人形石像 B 完整播放增量（2026-11-16）

全身人形石像已接入生产共用 B 手势：约 5.067 秒伸展并回位，保留 A 卡通与 D 荷花许愿树。保存场景的 Play Mode 实际播放、脚/骨盆稳定、锚点/标定不变、撤销、中途停止/复位/追踪丢失、动画图释放与恢复、变体切换均通过。六条 Park 入口的 24 条共用手势、外观/许愿树回归和 96 次实验条件检查通过。所有线路继续 YOLO 识别实物 → 定位/选择 → 火机同一套手部采样与 A/B/C/D 规则 → 对象魔法；尺寸和平面确认只是辅助，不替代该链路。见 [完整播放报告](Reports/hci-statue-playback-20261116/README.md)。以上为合成输入的编辑器验证，人形 B 尚待 Android/PICO；青铜 B、C 历史/语音、其他用例差异和真实 YOLO 闭环仍待完成，Goal 保持 active。现有 APK 未更新。下方候选及旧未完成描述是历史状态，以本增量为准。

## HCI 石像 B 全身伸展资源候选（2026-11-16）

为补足半身像无法伸懒腰的结构缺口，新增 CC0 全身骨架候选与自有披布、5.067 秒伸展动作。Blender 和 Unity 153 姿态检查、脚/骨盆稳定、回位及正反面蒙皮渲染通过；修正检测脚本重复缩放、缓存截图以及披布背面穿透问题。资源位于 ArtSource，未替换正式场景、未进入 APK。仍需艺术完善、共用 B 手势接入、播放/中断/撤销/复位及 PICO 验证，不能将 B 标为完成。详见 [候选验证报告](Reports/hci-statue-stretch-20261116/README.md)。所有线路继续使用 YOLO → 定位/选择 → 火机共用 A/B/C/D 手势 → 对象魔法，Goal 保持 active。

## HCI 历史石像 D 荷花许愿树增量（2026-11-16）

石像 D 已替换扁球占位，加入六朵分层荷花、展开动画与七签许愿树；沿用火机握拳后张开的共用规则，只在确认水平锚点后显示。大理石/青铜变体的合成生产 A/D 手势、展开与停止、撤销/复位、平面失效/追踪丢失清除、锚点/标定稳定、网格材质复用和渲染检查通过；24 条共用轨迹与 96 次条件回归通过。见 [荷花许愿树报告](Reports/hci-statue-garden-20261116/README.md)。这是编辑器验证，尚未进入 APK；真实 YOLO/PICO、环境遮挡与帧率仍待头显。B 完整伸懒腰和 C 历史/语音待完善，八用例 32 效果 Goal 继续 active。以下历史记录中的 D 未完成是当时状态，以本段增量为准。

## HCI 历史石像卡通外观增量（2026-11-16）

石像 A 已从故事章节选择改为原材质/卡通材质切换，B/C 保持 A 外观独立，C 保留故事和来源标签；使用与火机相同的识别规则。大理石/青铜变体通过生产合成手势、材质恢复、同帧渲染、Undo/Reset、锚点与输入保护检查；共用线路及实验条件回归通过。见 [石像外观报告](Reports/hci-statue-style-20261116/README.md)。B 完整伸懒腰、C 历史/语音内容、D 荷花许愿树仍未补齐；该增量尚未进入 APK，真实 YOLO/PICO 未验收。Goal 保持 active，继续完成完整用例而非只以外观通过代替。

## HCI 漫画萌宠描边增量（2026-11-16）

宠物 A 已从改变梦境内容修正为可撤销的漫画轮廓，沿用火机共用手部采样和 A/B/C/D 手势规则；B 梦境、C 戳破、D 好感保留独立状态。Cat/Dog 的生产识别合成轨迹、同帧像素对比、骨骼跟随、丢失/距离保护、撤销/复位和实际渲染通过；六条 Park 线路及实验条件回归通过。证据见 [描边验证报告](Reports/hci-pet-outline-20261116/README.md)。本次仅为编辑器源文件交付，已发布 APK 未包含该增量，PICO 双眼与真实 YOLO 端到端未验收。Goal 继续 active，不将此项通过扩展为全部 32 个效果完成。

## 用户再次明确：全部线路使用火机同一套 YOLO + 手势魔法（2026-11-16）

此项覆盖历史上任何对象独立手势方案。八个 HCI 用例统一为：YOLO 识别现实目标 → 校验定位与实例 → 选中目标 → 火机共用手部采样和手势规则 → 分派该目标的 A/B/C/D 效果 → 反馈、撤销/复位和实验记录。六个 Park 内容入口和火机入口属于同一交互体系；打人柳与四季树共用 Tree 识别类别，但保留各自效果语义。

- A 捏合后松开；B 指尖画圈；C 张掌横挥；D 握拳后张开。所有对象共用 `TabletopHandSample`、`TabletopGestureRules`、`GestureReachWindow` 的判定，不另设植物、宠物或房间的施法手势。
- 尺寸标定、朝向和平面确认只是定位辅助步骤，不能代替 YOLO + 手势的正式线路；UI/标定占用输入期间不得触发魔法。房间由家具识别提供入口，确认空间几何后再执行相应魔法，不能把检测框当作三维房间。
- 无可靠目标、追踪失效、过期关节输入或距离不合格时取消施法；切换目标和退出 UI 后先张手再开始。实验条件继续控制允许的魔法维度。
- 当前共用识别已实现，编辑器合成轨迹已有证据。所有线路的真实 YOLO 权重、PICO 相机/手势与动画端到端仍必须逐项实测；Virtual test、按钮触发和成功编译均不能作为真机完成依据。
- 本轮整合检查同时修正旧配置报告仍导出停用的 Park 独立手势参数的问题，改为直接引用共用规则常量。保留候选模型与森林试作，但仅发布与此 HCI 主线有关且通过检查的文件。

本批统一线路交付证据见 [2026-11-16 整合验证](Reports/hci-unified-release-20261116/README.md)。源码快照 `c901a0c8aa21c63f8269b255b415baff1018dc50` 的 1581 个项目源文件核对一致；24 条共用手势轨迹、96 次条件检查/139 条记录及 14 场景启动通过。Android IL2CPP 被 3 GiB 可用内存下限中止（最低采样 2.828 GiB），未发布新 APK，不降低守卫重试。后续构建必须使用 PowerShell 7，并先解决内存预算；旧包与历史报告不能代替新版验收。此前段落中的“未发布”按当时源哈希保留，本批交付范围以最新报告为准。Goal 继续 active。

2026-11-16 Android 后续验证：统一火机手势源码已完成 IL2CPP/Gradle 打包，签名、ZIP CRC、ARM64、14 场景和 40 项模型/材质/贴图来源核对通过。新包为 `HciUnifiedAllScenesValidation-20261116.apk`，应用标识 `com.yn.picmagicmr.validation`，见 [GitHub 验证包](https://github.com/JC4747-jc/magic-wand-amusement-park/releases/tag/hci-unified-validation-20261116) 与 [构建证据](Reports/hci-android-recovery-20261116/README.md)。它不包含新的已接受 YOLO 权重，不代表真实 YOLO→定位→手势→动画、PICO 双眼/性能或全部 32 项效果验收完成。此前 Android 失败报告保留作为历史。

本次通过分离 Unity 与离线 IL2CPP 转换降低重叠内存；转换最低采样可用 RAM 3.827 GiB，随后完整构建最低 4.279 GiB。3 GiB 下限未降低，单编译进程、日志与磁盘增长预算继续生效，保留 Library，无训练或自动重启。后续需要连接 PICO 验证真实输入；Goal 保持 active。

## Goal 当前执行目标：HCI「魔法乐园」需求优先（2026-11-15，覆盖下方旧执行顺序）

**目标：在现有 Magic Wand Amusement Park 的 Unity/PICO MR 工程中，完成以现实物体为对象、以四维手势为输入的魔法乐园体验。以仓库 HCI 八个用例为需求基线，逐项对照现有主线的识别、定位、动画、反馈、撤销/复位与实验记录，补足差异并验证。图片和工具只用于改善这些用例的表现与制作效率，不将项目改成森林漫游、宫殿游戏或通用 AI 建模演示。继续完成已授权的 YOLO 训练、F2G 独立实验、分支整合、GitHub 交付和资源控制。**

### 需求来源与可信边界

- 2026-11-15 通过 GitHub 远程 `ls-remote` 核实 HCI 分支仍为 `eae26868f857a7607a9fa2973fe9bc88c0260740`。需求对应表依据该提交的 [HciMeetingScenario.cs](https://github.com/JC4747-jc/magic-wand-amusement-park/blob/eae26868f857a7607a9fa2973fe9bc88c0260740/Assets/Scripts/MagicMR/HciMeetingScenario.cs) 的枚举、名称和提示，并结合 [HciMeetingDirector.cs](https://github.com/JC4747-jc/magic-wand-amusement-park/blob/eae26868f857a7607a9fa2973fe9bc88c0260740/Assets/Scripts/MagicMR/HciMeetingDirector.cs) 与 [README](https://github.com/JC4747-jc/magic-wand-amusement-park/blob/eae26868f857a7607a9fa2973fe9bc88c0260740/README.md)。
- 代码注释引用 `HCI meeting 4.pdf`；该分支追踪文件中未找到该 PDF 原件。本表是仓库可核实的 HCI 需求基线，不宣称已读到课程任务书、评分细则或会议原文。后续如获得原件，逐项复核并记录差异，不自行编造要求。
- 最新主线 README、`StudySpec`、`GestureManager`、`ExperimentConditionSwitcher`、`DataLogger` 和 Park 代码用于判断实现现状，不能反过来把尚未实现的 HCI 要求删掉。第三方截图不具有项目需求优先级。
- 八个体验用例可复用六个 Park 内容入口和既有打火机主线；不要求机械创建八份重复场景。14 场景启动回归是兼容性证据，不等于八用例各项交互通过。

### 八用例需求追踪表

以下 A/B/C/D 沿用 HCI 分支提示：捏合/画圈/横挥/握拳后张开，对应 Appearance、Agency、Rule、Deconstruction。现有 Park 的按钮名、快捷键和动作语义可能不同，必须建立逐项映射；发现冲突先记录并修正，不能把按钮回调成功当作手势映射一致。

| HCI 用例 | 原分支 A / B / C / D 效果要求 | 现有承载入口与必须补验内容 |
| --- | --- | --- |
| 打火机·焦炭小恶魔 | 焦炭火 / 翅膀咳嗽烟 / 躲闪 / 爆炸净化开花 | BridgeStereoFusion + RealityEditor；逐项核对现有生命化与小恶魔表现，验证锚点、净化开花和复位；双手放大为既有扩展，不能代替四维流程 |
| 打人柳 | 黑暗风 / 枝条抽打 / 树疤呼吸 / 封印宝箱 | ParkTree 的 Fantasy 变体；核对枝条、树疤、安抚/封印前置条件与宝箱反馈；普通树风摆或背景森林不能算完成 |
| 像素虫 | 8bit / 卡顿舞 / 眩晕 / 爆金币 | ParkBugs 的 Cockroach/Spider；保留识别对应实物及虚拟覆盖边界，补验像素风切换、离散舞步、眩晕、奖励和恢复；自然爬行动画只是基础 |
| 历史石像 | 卡通 / 伸懒腰 / 讲史 / 荷花许愿树 | ParkStatue 的石雕/青铜变体；逐项核对造型变换、关节动作、故事反馈、荷花/许愿树；现有苏醒/扫描与牛首不能直接视为完整用例，故事须区分有来源史实与虚构演示 |
| 出租屋 | 水彩/赛博 / 落地窗 / 水母灯 / 糖果雨 | ParkRoom；在已确认现实空间上验证房间风格、窗户、灯与糖果雨，核对空间确认/天花板约束；阅读角美术只能作为局部素材，不能替代房间交互 |
| 桌面植物 | 选中 / 拟人说话 / 长大或幼苗 / 去掉效果 | ParkPlant；核对选择反馈、拟人表达、生长回退及清除；开花为扩展，不代替原需求；演示湿度不得标成真实传感数据 |
| 漫画萌宠 | 漫画描边 / 梦境泡 / 戳破 / 好感 +1 | ParkPet 的 Cat/Dog；优先核对描边、气泡内容与弹出/戳破、奖励和状态恢复；自然步态用于表现优化，睡眠/进食为图片启发的可选扩展，不抢占必需交互 |
| 四季树 | 动漫季 / 拟人 / 春夏秋冬 / 风吹落叶 | ParkTree 的 Natural/季节变体；核对风格与生命化、四季明确切换、对应花叶效果；当前 Style/Grow 等动作与 A/B/C/D 的映射须单独复核，不能仅有树叶变色 |

**美术适配原则：**采用自然比例、清晰材质、克制灯光与成熟构图；但 HCI 明确要求的像素、漫画、卡通、水彩等风格转换必须保留并可辨认。不能以“写实优化”为由删除研究中的风格维度。动画服务于动作因果与状态可理解性，避免只增加装饰摆动。

### 图片和工具在魔法乐园中的具体用途

- **ProBuilder / Polybrush**：用于出租屋的空间比例、窗洞/边界和局部树木展示区的白模、地面与植被；先检查交互距离、实物尺度与遮挡，再做材质。不会强制给 MR 用户增加 WASD 漫游或大面积虚拟地形。
- **itch.io 与其他许可明确的素材库**：按八用例的具体缺口找树、虫、石像/青铜兽、植物、宠物和房间资产；先复用已有资源，再检查下载量、绑定、动作和 GitHub 再分发许可，不批量搬运教程素材。
- **World Labs / fal / image-blaster**：仅作为有明确缺口时的候选工具。静态空间可研究用于出租屋窗外或受控背景；单物体网格可用于缺失道具。生成物须独立通过尺度、拓扑、绑定、材质、碰撞、双眼与性能验收；不替代真实房间定位或对象动画。账户、凭据和预算就绪前不启动付费请求，截图价格不作为当前报价。
- **ChatGPT 图像生成 / Codex / Unity MCP**：图像只作为对应场景的概念、贴图或道具参考；开发代理通过现有 Editor 工具或经验证的 MCP 实现 Unity 内可交互成果。安装工具、换模型、生成图片均不等于 HCI 用例完成。
- **现有森林试作降为候选研究资产**：ForestBlockout 的通路验证只说明其自身移动/碰撞逻辑；ForestStudy 的树、蕨类、地表与风动只按 ParkTree 需要择取。ForestStudy 场景仍在隔离工程，本轮候选资源/脚本尚未作为正式 Park 内容整合；不升级为主线、不加入正式 APK、不扩张为独立森林项目。保留已有文件和证据，不删除用户工作。

### 新执行顺序与完成条件

**用户追加的统一主线（2026-11-15，优先于所有历史输入方案）：所有用例采用与火机一致的 YOLO → 定位/选中 → 手势识别 → 对象魔法链路。** 共用火机的手型判定、时序状态机、阈值、冷却、追踪失效处理与输入仲裁；仅按目标类型分派 A/B/C/D 的效果。不得给植物或其他对象另建样例手型/距离阈值体系，也不能把直接调用动作 API 的测试称为共用识别完成。以火机实际启用的 TabletopGestureRecognizer / TabletopGestureRules 为核对基线，先抽取共享采样/判定层，再接入各目标；避免双识别器重复消费。

- 房间以 YOLO 家具检测提供目标线索，结合确认后的房间平面/天花板施法；YOLO 框不冒充三维房间几何。手动放置、Virtual test、键盘/按钮仅作调试与辅助标定，不替代真实入口验收。
- 必须补测：同一关节轨迹在火机及各对象上产生相同手势结果、一次动作仅触发一次、UI/标定抢占、追踪丢失取消、距离与冷却门控、条件开关与实验记录。随后才进行 PICO YOLO+真手势端到端验收。
- 当前火机与 Park 已共用 TabletopHandSample、TabletopGestureRules 和 GestureReachWindow。Park 实际 XR 回调接入同一采样与四维仲裁，移除生产入口中的植物样例手型、独立捏合施法、静止掌/上下挥/拍手等分流；长握拳后张开统一为 D，不再抢占成 Undo。旧辅助 API 的存在不代表它仍在生产手势入口执行。
- 六类内容的 24 条 A/B/C/D 合成轨迹已在 Play Mode 通过生产 ProcessHandSample 入口，与火机规则结果一致且动作被接受；还覆盖 UI/标定抢占、丢失/过期/远距输入与持续捏合不能重复标定。真实 XR 关节、YOLO 识别到魔法、条件与日志闭环仍未完成，不能把合成测试当成整条线路完成。
- GitHub 上传采用已验证整合基线；本轮共用识别运行时代码仍为本地候选，待整合构建后发布。失败的 YOLO 候选只保留诊断身份，不部署为正式权重。

2026-11-15 实施更新：已完成 [32 项代码语义审计](Reports/hci-requirements-20261115/README.md)，共用识别修复了 Bugs 的 A/D 手势路由；具体效果表现、实验条件和日志仍有差异。Pet B/C、Plant A/B/C/D 有本地动作层修复证据，六类内容有共用火机合成手势证据。该分类不是完成率：32 项均未完整通过 HCI 全链路验收。最新结果见 [共用识别报告](Reports/hci-shared-gestures-20261115/README.md)，较早植物动作层报告保留历史范围。这些新增运行时代码未发布。

2026-11-15 GitHub 同步：`restore-4d-lighter` 已快进到 `cba8ffbb5cbe8a7725bd675d338248f1296209f8`，采用已验证整合基线 `f0e6183`，新增最新 HCI Goal 和发布边界说明，分支顶端排除未达标 gap-candidate。Assets/Packages/ProjectSettings 与已验证基线一致；本地后续候选仍保留。主工作区分支和 Git index 不变。

2026-11-15 HCI 条件接入：Park 的生产 Dispatch 已遵守原有四种实验条件，经 Dispatch 分派的额外调试动作限制在 C4，Ctrl+1–4 与普通虚拟施法数字键分离。增加显式 StartStudySession 和动作接受/拒绝 CSV、共用采样的限频轨迹记录。条件矩阵在六类内容上检查 96 次动作尝试，含控制动作共 139 条结果；数据明确标注为合成测试，不是受试者实验。最新范围见 [条件与日志报告](Reports/hci-study-conditions-20261115/README.md)。会话 UI、跨场景生命周期、完整交互事件和真实 YOLO/PICO 仍待贯通；此前语义审计保留当时源文件哈希，本段不将任何用例升级为完整验收。

2026-11-15 跨线路会话修复：DataLogger 独立持久化，火机手势管理器随场景释放；新场景沿用已有身份、条件、试次和日志文件。实际 ParkPlant → BridgeStereoFusion → MagicMR → ParkPet 及独立 MagicMR → ParkPlant 的合成切换检查通过，旧活动手势组件没有残留。CSV 连续会话防覆盖、特殊字符转义及限频/非有限轨迹检查通过；移除标定的逐帧重复日志。条件矩阵 96 次动作/139 条结果回归与 14 入口启动（0 错误）通过，详见 [跨线路会话报告](Reports/hci-study-session-20261115/README.md)。该结果不替代 YOLO/PICO 真机闭环或 Android 验证；新增运行时代码仍未发布。下一步优先推进按 HCI 的对象效果差异、真实识别入口与整体构建；操作者会话 UI、完整交互事件及 IO/容量保护继续列为缺口。

2026-11-15 四季树效果修复：Natural 变体按 HCI 改为 A 可逆树冠风格、B 树干拟人/眨眼、C 四季循环、D 有界阵风落叶；不再由 A 改季节或 D 弹出幻想树宝箱。实际 ParkTree 的持续合成供样经生产 ProcessHandSample 驱动，逐次匹配火机规则并验证动画、撤销/复位和锚点不变；保留选择/UI 后张手准备及追踪过期取消保护。保存 9 张实际渲染，另有网格、六类 24 条手势和四条件 96 次动作/139 条结果回归，见 [四季树报告](Reports/hci-season-tree-20261115/README.md)。拟人面部远距可读性、最终美术质量、打人柳独立效果及真实 YOLO/PICO 仍未验收；本轮无 Android 构建，候选代码未发布。资源守卫采样最低可用 RAM 6.372 GiB，最大单任务磁盘增长 1.47 MiB，证据归档约 1.48 MB；没有启动训练或大型下载。

2026-11-16 打人柳效果修复：Fantasy 变体改为 A 深色/自然切换（不改季节）、B 有界枝条抽打/收回、C 树疤亮度呼吸、D 树疤揭示后的封印开箱。宝箱新增底板、四壁、金属带、锁扣、金币和铰链盖；移除整树呼吸缩放，保持现实锚点与拟合树干稳定。实际 ParkTree 持续合成手势通过生产入口比对火机规则，验证封印前置、Undo/Reset、动画并保存七张渲染；四季树、原叶片网格、六类共用手势和四条件矩阵回归通过，见 [打人柳报告](Reports/hci-willow-20261116/README.md)。当前普通 SmallTree 仍是树种占位，垂枝/柳叶模型、黑暗树冠可读性、真机与 Android/发布仍未完成，不能将本轮视为打人柳完整交付。五项验证最低采样可用 RAM 6.385 GiB、最大单任务磁盘增长 1.96 MiB，证据约 2.70 MB；无训练或大型下载。主 Git index 未改动。

1. **先做需求差异审计**：给八用例各 A/B/C/D 建立状态清单，逐项标记“代码存在 / 编辑器实测 / Android 构建 / PICO 实测 / 缺失”；关联源文件和证据。特别检查当前 Style/季节映射、石像讲史/许愿、房间风格/窗户、植物说话及宠物描边，不能延续“六入口已有即八用例完成”的假设。
2. **补交互闭环再精修美术**：现实目标识别/选择→标定与召唤→手势门控→动画和音画反馈→停止/撤销/复位；检查丢失追踪、切对象、重复手势和中断。共享同一输入仲裁，不能同时挂载旧 HciMeetingDirector 和新 Park 控制器而重复消费手势。
3. **保留 HCI 可比较性**：核对既有 Baseline、Appearance、Appearance+Agency、All 条件，以及 subject/condition/trial、事件、手部/目标轨迹记录。动画替换不得悄悄改变条件定义；新增场景是否正确记录应实际验证。完成率、误触、完成时间等作为建议评估项另标，不冒称课程评分指标；模拟数据与真人实验数据分开，不伪造受试者结果。
4. **按用例优化资产/动画**：从已验证素材逐项替换，保持现实锚点和可识别的四维效果；猫步态候选复验属于 Pet，树风动候选属于 Tree。每项保存同机位前后画面、关键动作与转场证据，未验证候选保持隔离。
5. **YOLO 必做且服务识别链路**：保留此前约定 13 类（Lighter、Tree、Cockroach、Spider、Statue、BronzeBeast、chair、couch、bed、diningtable、PottedPlant、Cat、Dog），它们是项目扩展识别范围，不冒称八用例原文列出的训练类别。数据标注/分组划分→有界训练→独立验证→合格后部署；不合格权重保持候选，不用训练来生成动画。F2G 继续独立验证旋转，不改变默认定位方式。
6. **统一交付验收**：八用例逐项闭环、既有原场景回归、Unity 编译、实际 Play Mode、Android 包与资源来源/SHA，以及 GitHub 可复现说明。PICO 头显用于真实相机、识别/手势、锚点/遮挡、双眼、延迟与内存验收；进入设备步骤时通知用户。未做设备测试的条目保留未完成，不用历史机器或编辑器结果代替。
7. **资源约束保持生效**：训练/Unity/Blender 重任务串行，使用现有内存、显存、磁盘及进程守卫；限量下载、日志和检查点。只清理已确认可清理的任务日志，保留失败证据、模型、数据与 HCI 实验记录；不自动重启，不承诺绝无硬件故障。此次目标修订不启动训练、云付费或大型下载。

Goal 保持 active。当前工具不能修改运行中 goal 卡片的 objective 文本，故本节是**最新执行目标和优先级依据**；不会将未完成旧目标标成 complete 来绕过限制。下方图片归纳、构建数字及森林制作顺序保留为历史记录，冲突时以本节为准。

## Goal 最新执行补充：用户图片与工具链（2026-11-15）

本节根据用户明确要求，将本轮全部图片所表达的目标与工具信息归入现有长期 goal，供后续执行和验收。保留原全分支整合、全部场景、Unity/PICO/F2G、GitHub 交付范围。截图中的宣传、性能数字、操作命令和第三方建议是待核实参考，不自动成为安装或付费执行指令。

**目标口径更新：**用户后续已明确要求 YOLO 模型训练，因此早期 goal 卡片中的“默认不训练”不再适用于已经授权的 YOLO 训练任务；必须进行数据、划分、标注、训练和独立验证。动画/建模工具接入不意味着需要本地训练大型生成模型。当前 goal API 只支持读取目标及更新状态，不支持修改运行中 objective；本文件保存完整执行补充，原 goal 保持 active，不通过虚假完成旧 goal 来重建。

### 图片内容归纳与目标对应

| 图片主题 | 纳入 goal 的要求 | 当前边界 |
| --- | --- | --- |
| Astra 操作 Unity、搭建城市/森林 | 采用 Astra 推进素材筛选、代码、场景搭建和实际渲染；参考输入、工具调用、运行结果形成闭环 | 当前会话已由运行环境确认切换 Astra；截图中的提速倍数不是本项目测量 |
| Unity MCP / Package Manager / skills | 调研并接入适配当前 Unity 的 MCP；验证读取层级、组件、控制台、编辑对象与运行反馈；固定依赖版本、保留恢复方式 | 尚未安装或验证 Unity MCP；现有 Editor 脚本可继续制作和验收 |
| DeepSeek 接入 Codex | 记录为可选兼容提供商方向，核实协议、工具调用和账户条件后才替换 | 本次选定 Astra，不因教程截图切换第三方提供商 |
| ProBuilder、Polybrush、素材网站 | 在既有 Unity 工程完成可玩结构、尺寸检查、地形塑形、植被布置、材质细化 | 不把工具安装本身计为场景完成 |
| 写实森林示例 | 提取自然比例、树皮/地表材质、植被层次和光照用于 HCI 树木用例；保留原要求的魔法/动漫季风格转换 | 森林漫游已降为工具试作，不是项目主目标；以本文件顶部 HCI 需求对应表为准 |
| 先白模、再尺寸、最后角色画面 | 固定 Unity；先完成通路、交互、碰撞；放人物尺度参照检查路宽、坡度、台阶、门洞、相机净空；通过后再替换美术 | 森林白模已保存并通过路线/碰撞/交互回归；写实美术仍待制作，不因参考宫殿图片将项目改成宫殿游戏 |
| 图像转世界 / image-blaster | 建立参考图→静态环境→独立道具网格→Unity 导入→材质、碰撞、交互与性能验证的候选流程 | Gaussian splat、静态网格、绑定动画是不同交付物；需分别验证 |
| AR 猫、Animator 提示词教程 | 明确场景对象/骨骼/已有动作及目标；猫咪待机、走路、嗅闻、睡眠、进食候选逐项制作与接入，检查转场、落脚和停止复位 | 当前已接入待机/嗅闻/行走；睡眠/进食尚未验收，嗅闻不能计作完整进食 |

### 工具实施清单

1. **ProBuilder**：用于小径、平台、障碍、交互区与碰撞白模。固定 Unity 6000.0 兼容版本，先在隔离工程编译验证；明确人物尺寸和路线后保存可编辑场景。官方说明：https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.probuilder.html 。
2. **Polybrush**：用于网格地形塑形、平滑、顶点色/纹理混合及植被实例布置。验证 URP 材质支持，控制植被密度、碰撞体与透明叶片开销；不无界刷入高面数资产。官方说明：https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.polybrush.html 。
3. **itch.io**：筛选真实风格且有明确许可的树木、蕨类、岩石、倒木和带动作动物。每项记录作者、原链接、许可、修改、面数、贴图尺寸、骨骼/动作、下载体积。免费不等于可在公开 GitHub 再分发原文件；优先可满足本项目发布要求的来源。低多边形卡通资源不能仅因体积小就替代用户写实目标。
4. **World Labs**：评估参考图生成静态空间的能力、输出格式、Unity 渲染支持、遮挡/碰撞、双眼显示与 PICO 性能。先一个小样；云生成需有可用账户/凭据及明确预算，日志不记录密钥。不将生成的静态世界当作可动画角色。官方工作流参考：https://worldlabs.ai/labs/showcase/image-blaster 。
5. **fal**：评估 Hunyuan3D 等单物体生成端点及 PBR 输出，用于缺失道具候选。选择具体端点后核实输入、计费、面数与下载限制；进入 Unity 前检查尺度、拓扑、UV、材质、减面/LOD，需运动的对象另做绑定/动画。官方候选：https://fal.ai/models/fal-ai/hunyuan3d-v3/image-to-3d 。
6. **image-blaster**：审查官方仓库 https://github.com/neilsonnn/image-blaster 的脚本、依赖、许可和服务调用；将必要步骤适配本项目的 Codex 工作流，记录固定提交与输出来源。其原工作流面向 Claude skills，接入 Codex 必须验证，不能以截图宣称已适配。避免为了一个候选资产另装整套重复查看器；真实需要时才安装有界依赖。
7. **ChatGPT 图像生成**：用于风格探索、可追踪的贴图/材质参考与独立物体参考图；需要生成时使用图像生成工具。所有最终模型和动画必须在 Unity 中运行并渲染交付。

### 历史图片驱动执行顺序（已由顶部 HCI 执行顺序替代）

- 第一阶段：完成当前猫步态候选复验并保留旧版；建立森林白模、人物尺度、可通行路线及交互点。随后验证 ProBuilder/Polybrush 依赖和 Unity MCP 的实际可用性。
- 第二阶段：筛选少量高质量、许可适用的自然资产，优先复用已有资源；制作森林地形、植被层次、材质和光照，输出同机位白模/美术对比及可行走预览。
- 第三阶段：对确有缺口的物体或静态环境试用 World Labs/fal/image-blaster。每次任务先确定输入、产物、请求/下载上限；保留候选和来源记录，审核后才纳入正式场景。
- 第四阶段：完善所有场景模型、动画和输入交互。猫狗检查脚掌接触与速度、重心/头尾运动和转场；昆虫检查分段步态；植物、水母、雕像与青铜检查节奏、形变和复位。当前牛首模型不能替代完整青铜兽的验收。
- 第五阶段：回归既有全部场景，检查编译、缺失脚本/材质、碰撞/尺寸、真实 Play Mode 交互、同机位动画渲染、Android 构建。PICO 连接后测相机/识别/手势/空间锚点、双眼观感、帧率与内存；设备缺席时明确保留缺口。
- YOLO 训练与上述开发并行规划、重计算串行运行；覆盖已约定的 13 类，拒绝数据泄漏和把公开数据结果当作头显实测。训练前检查 RAM/显存/磁盘，限制缓存、epoch、检查点数量与日志；保留最佳权重、必要恢复点和验证证据。
- GitHub 交付包括源码、可再分发资源及署名、动画、训练配置/合格权重与验证报告。大型二进制按体积选择发布资产或 LFS，密钥/缓存/原始受限素材不进入提交；保留原有工作和分支历史。
- 单次构建/Blender/训练重任务串行，使用已有内存与磁盘守卫；限制下载、生成次数、日志和检查点，避免磁盘写满及持续换页。不自动重启。资源控制旨在降低压力，不承诺硬盘绝不会发生硬件故障。

### 本轮实际进度证据

- 猫脚掌轨迹改为支撑/摆动交界速度与加速度连续的曲线，两个数值回归通过；Blender 与 Unity 均完成 193 姿态蒙皮采样，Unity 支撑误差最大约 0.155mm、循环接缝为零。该证据证明采样几何稳定性，不代表动画自然度或头显性能已最终验收。
- 新 FBX 位于 `.codex-tmp/cat-gait-v2`，已在隔离工程试装；主工作区正式猫 FBX 尚未替换。Unity 新版预览 48 帧/24fps/640×640，解码与颜色/运动检查通过，文件 `.codex-tmp/pet-motion-v2/CatWalk.mp4`，SHA256 `05bdaec05c00b54cf7f3d25c79d4178aaa403dd96ef1a1845738e968b3d8412b`。
- 本轮 Unity 检查最低采样空闲 RAM 6.178GiB，最大采样磁盘增量 6.73MiB，资源守卫未触发；检查报告 `.codex-tmp/github-delivery-20261115/cat-gait-v2-unity-resources.json`。
- ProBuilder 6.0.9、Polybrush 1.2.1 已在 Unity 6000.0.33f1 隔离工程导入并编译通过；附带 settings-manager 2.0.1，其余既有锁定依赖语义未变化。通过后已同步主工作区 manifest/lock。Polybrush 尚未完成实际笔刷与 URP 顶点色混合验收；Unity MCP、World Labs/fal/image-blaster 仍待接入。
- 森林白模 `Assets/Scenes/ForestBlockout.unity` 已保存并同步主工作区：24×32m 地形、3.2m 设计路线、1.8m 人物/1.65m 眼高、16组树干/树冠占位、三个交互站点、边界碰撞。ProBuilder 可编辑网格已使用，未改变既有 Android 场景清单。
- 159 个地面/全身胶囊通路探测通过，最大采样地面坡度 3.94°；真实 Play Mode 中以同一运行时移动方法执行 735 步，三个交互站点、阻挡碰撞、出生位姿及状态复位通过，运行时错误 0。最大采样脚底到设计曲面差约 7.1cm（含出生高度与胶囊皮肤），不是足部动画验收。物理键鼠、PICO 输入尚未实测。
- 首轮 Play 检查因测试将首帧重力后的坐标当出生点而失败，改为使用 Awake 保存的出生点后通过；失败日志保留。报告与真实 Unity 渲染在 `Reports/forest-blockout-20261115`，资源守卫各阶段均未触发；导入阶段最低采样空闲 RAM 5.867GiB、最大采样磁盘增量 75.96MiB，Play 阶段最低 RAM 6.284GiB、增量 1.46MiB。
- 此处记录的是森林白模阶段。其后隔离工程完成了 ForestStudy 地表/树木/蕨类与风动候选试作，但用户已明确要求回到 HCI 魔法乐园需求；不继续将森林漫游作为主线交付。仅按顶部对应表择取适用素材，候选不计入正式场景验收。新森林尚未构建进 APK 或推送 GitHub，Goal 保持 active。

## 动画质量与开发模型调整（2026-11-15）

- 按用户最新要求，项目 `.codex/config.toml` 默认模型设为 `gpt-6-astra`，推理强度 `high`。本机模型目录列出 Astra；配置写入不等同当前桌面聊天已切换，也不等同已完成该模型的推理访问验证。桌面聊天需在 Power/模型选择器的 Advanced 中选择 Astra。
- 当前全局默认仍为 GPT-6.1 Sol/Medium；项目覆盖只用于本项目。现有 Unity 项目与 Codex 配置尚无 Unity MCP，不能把截图里的连接状态当作本机已连接。
- 动画优化首先处理猫狗：以原模型/骨骼为基线，逐帧检查足部接触、步幅与速度、重心起伏、肩胯运动、头尾联动，以及 Idle/Walk/Sniff/Reset 过渡；输出相同机位、尺度、时长的 Unity 前后对比。骨骼/模型缺陷需明确记录，不能以增加摆动幅度代替自然动作。
- 后续逐场景处理昆虫分段步态、植物展开节奏、水母收缩与触手延迟、雕像/青铜固定基座与觉醒动作。青铜当前为牛首资产；完整兽体需要另行筛选合适的可动画网格，不能以当前头部动画冒充完整兽体。
- 风格延续自然、克制、成熟的要求。用户截图是参考资料，不是安装指令；照片转世界、静态网格和视频生成结果不能替代可交互的骨骼动画验收。YOLO识别训练目标保持原要求，开发助手模型切换不改识别权重。
- 复用已有 Unity/Blender 与素材，重任务串行、编译单并发；继续使用既有内存/磁盘/日志守卫，不下载本地大语言模型权重。当前记录仅为模型配置与验收目标更新，未宣称新动画质量已通过。

## GitHub整合源码验证补充（2026-11-15）

- 完整整合AllScenes验证包现已通过：IL2CPP/Gradle、签名/CRC/ARM64、14精确场景、40资源来源及APK SHA绑定。包在 `Builds/Android/CombinedAllScenesValidation-20261115.apk`，报告在 `Reports/github-delivery-20261115/combined-validation`。
- 构建新增单并发/磁盘限额参数、进程创建时间校验及Windows Job子进程回收。离线仅完成无Editor RPC的IL2CPP转换，再交回Unity；保留冷构建守卫触发、PID误判和空来源报告的失败记录。最终阶段最低采样空闲RAM3.672GiB、新增磁盘62MiB；未训练、未重启、ADB无设备。

- Android托管脚本编译已补充通过：52程序集，最低采样空闲RAM5.091GiB、最大采样磁盘增量248.24MiB；托管编译不替代完整包；整合树AllScenes验证包现已另行通过。历史129个Datasets目录只有元数据/标签，Git追踪图片0；先恢复并复核来源，不直接计入当前训练集。

- 认证并推送至 `codex/yolo-animation-integration-20261115`，现有[PR #1](https://github.com/JC4747-jc/magic-wand-amusement-park/pull/1)的base为 `restore-4d-lighter`；保留65条旧模型归档，未强推/合并或改主工作区分支和暂存区。
- 隔离整合树14场景审计、自然模型几何、旧6场景协程、6场景真实按钮/渲染流程通过；30张截图、0运行时错误。旧NUnit断言适配后6/6通过，产品代码未因测试调整。
- 首次导入新增磁盘限额和首次图形NUnit内存限额曾触发，均停止并保留记录；后续复用独立缓存，CPU回归无图形/禁Burst，界面回归保留实际图形。最后界面回归最低采样空闲RAM6.57GiB、最大采样新增磁盘0.98MiB。
- 报告、6张动作图、逐场景JSON及NUnit结果在 `Reports/github-delivery-20261115/combined-validation`。现有四份APK来自主工作区，不冒充本整合树重新构建；PICO实机、合格识别权重和完整训练覆盖仍未完成，Goal继续。


Goal 状态：执行中。此文件是验收清单，不表示模型、动画或设备验收已经完成。

## 用户修订后的完整目标（2026-11-15，包含YOLO训练）

审查所有远程分支并保留当前有效修复，继续完成全部项目场景的模型、动画与交互接入；采用成熟、自然比例和真实材质的视觉方向，以实际Unity渲染、生命周期回归、Android构建验收。完成Unity/PICO SDK/F2G环境及本地交接，并明确区分编辑器模拟与真实设备结果。

**YOLO模型训练、独立验证和识别服务接入现为必做项，用户已授权训练，原“默认不训练”要求由本次修订取代。** 训练覆盖实际场景全部13个识别类别：Lighter、Tree、Cockroach、Spider、Statue、BronzeBeast、chair、couch、bed、dining table、PottedPlant、Cat、Dog；模型可以共享，但类别映射和各场景的验证结果必须分别记录。先审查现有标注和可用数据，补齐缺失类别；不得把仅训练打火机、仅完成一次试跑或公共COCO推理当作全部场景训练完成。

训练执行与验收顺序：

1. 审核数据来源、许可、图像/标注完整性、类别及坐标；按采集组划分train/val/test并查重复/泄漏，冻结独立测试集。修正旧机器绝对路径，保留原始数据和既有可用权重。
2. 配置隔离的GPU训练环境，不覆盖F2G或现有推理环境；先进行有界短训确认前向/反向、显存和输出，再进行正式训练、验证与测试。记录固定初始权重、依赖版本、随机种子、参数和输出哈希。
3. 使用独立测试集记录逐类别precision/recall、mAP、混淆矩阵、误检/漏检和推理资源/耗时，与现有模型比较。数据不足、失衡或验证不合格的类别保持未通过，不能仅凭训练完成就部署。
4. 将通过检查的新权重接入scene_profiles与类别映射，重新验证PC协议/缓存/模型身份及F2G共享检测器，必要时重建Android产物；真实PICO采集、手势、定位/旋转、双眼与设备性能仍须头显验收。

资源约束继续有效：Unity构建、Blender、F2G推理和YOLO训练串行；训练从nano模型、batch=1、workers=0、较小输入开始，先测量后调整；关闭RAM/磁盘图像缓存，不进行无界下载/搜索/自动重试。训练前检查RAM/VRAM和宿主C/D盘余量，为每次运行设定epoch、输出磁盘和检查点上限，仅保留必要的best/last及精简指标。OOM或资源越界时停止当前任务、释放资源并记录，不自动重启电脑，不丢弃用户修改。

当前训练进度：独立GPU环境与2epoch打火机试跑已通过；另已完成Cockroach/BronzeBeast首版候选20epoch真实训练（340批次/334次首卷积优化器更新）及冻结分组测试、Windows同SHA重载。候选只用了23张已审框的公开照片，测试仅3张；conf0.25/IoU0.5下3个目标全部漏检，精度不合格，不部署，仍须补足数据和其余11类训练。原服务/初始权重保留，旧batch16/workers4脚本不直接沿用。现有Goal工具无法改运行中卡片正文，本文件与用户最新指令作为修订后的执行要求，Goal保持active。

初次只读数据审查已完成：`Tools/audit_yolo_training_data.py` 输出 `.codex-tmp/yolo-training/data-audit.json`。5组本地数据合计扫描1,380个图像条目/170,019,992字节（不同数据版本有重叠，不能当作1,380张独立照片）；全部可解码、未发现缺标注或非法行，各组内部未发现精确像素跨split重复。当前本地既有标注只有Lighter；仍须来源/许可、近重复、采集组及语义标注复核。原始两组使用多边形标注，三组detect版本为5列检测框；merged_v3配置仍含旧D盘根路径。该初审阶段未训练；后续真实短训结果见开发接续记录顶部。

缺失类别数据准备：官方Open Images元数据约38.5MiB已建立367张/10个候选类别索引；仅下载26张审核样图，共7,564,761字节，保留作者、原网页、许可、图像SHA及原框。`Tools/fetch_openimages_review_sample.py` 单线程，单图2MiB/图片总128MiB/15分钟上限；Windows采用系统Schannel证书链，保持TLS验证。没有创建训练标签或项目split。前6张模糊语义样图实际查看后，花卉/仙人掌特写及地栽大黄均不能直接映射PottedPlant，雕像脚部特写也暂拒；两个人形雕像仅保留有条件候选，不能作为BronzeBeast。结论 `.codex-tmp/yolo-training/openimages-review/semantic-review-first6.json`，6张均尚未接受训练；其余20张仍待审核。Cockroach/BronzeBeast须另补有许可和标注的真实数据，不能把候选索引当成训练集已齐全。

上述26张为初次审核阶段，现已扩展：Open Images共189张/51,460,437字节；iNaturalist的两种明确蟑螂物种元数据40条、Commons青铜器候选60条，仅下载16张蟑螂和13张青铜器，共12,264,924字节。29张逐一原图及框图审核后，14张蟑螂/9张青铜兽共23个手工检测框保存在 `TrainingData/public-gap-annotations-v1.json`，另6张隔离且不作为负样本；来源、许可、作者、SHA、观察者/日期和相同藏品/复制品家族分组保留。Open Images的60张Sculpture/Houseplant候选全部语义初筛，20张保留语义候选、40张隔离，仍须框完整性复核；切花、地栽、建筑部件及玩偶等不能静默映射为目标。记录 `TrainingData/openimages-semantic-review-v1.json`。另15张精确类别抽样发现人工圣诞树和漏标的大椅子，不能把所有未框目标当可靠背景。

`Tools/audit_yolo_candidate_groups.py` 已扫描355张候选/98,941,565字节，0精确像素重复、66个双感知哈希候选对；按来源组和候选对保守并组，算法不证明不同实物。**原Find lighter/detect的137张全是pico_frame编号系列（含一张带导出后缀），缺少原采集session标识，按同一未解决来源组处理；该组横跨原train97/valid26/test14，不能作为独立精度验证。** 新的蟑螂/青铜兽23张对应19个保守组，冻结划分在 `TrainingData/gap-candidate-split-v1.json`：train17、valid3、test3，各split都含两类且组不交叉；test在训练前冻结，不据这3张调参。打火机正式验证需新采集/独立来源；补真实头显数据时需要连接PICO并开启USB调试。

`gap-candidate-20261115-a` 20epoch/416/batch1/workers0/cacheFalse/AMP/AdamW/nbs1候选训练通过技术检查；报告 `.codex-tmp/yolo-training/artifacts/gap-candidate-20261115-a/report.json`，best SHA `ba8c4259fafd94d218925f62197898997c3f5c87743d1209466781b4c844baeb`，best/last各5,422,270字节。原生AP评估conf0.001：两类mAP50分别0.28917/0.14214，仅2/1个测试实例；另在实际候选conf0.25下0命中/3漏检，不能采用。已有bugs/statue服务没有合格自定义基线，不伪造基线精度0作改进比较。Windows同SHA CPU重载/实图有限输出通过。PyTorch峰值分配/保留169.39/210MiB、最低采样宿主空闲3.498GiB、最大采样RSS2350.08MiB，Linux运行输出20.05MiB；45个回调/五秒样本不是完整设备峰值。固定128MiB输出/15分钟及RAM/VRAM/磁盘门槛未提高，F2G五个关键依赖文件哈希不变；没有替换服务权重。结束空闲RAM9.12GiB、显存5069MiB、C/D盘208.11/415.99GiB，WSL交换空间当时用量0，未重启，ADB无设备。

模型接续缺口已在本地补齐：`BuildNaturalStatue()` 现按Statue/BronzeBeast选择石雕/青铜兽模型，虚拟预览可切换；独立蒙皮、苏醒/扫描/Stop/Undo、三场景变体与六场景/14场景回归、四包构建已通过。真实识别权重和PICO验收仍未通过，不能将模型完成等同识别通过。

## 当前整合与验证（2026-11-15）

- 最新青铜兽版四包全部构建/核验通过：`.codex-tmp/android-{main,pose,repair,allscenes}-bronze-final.log`、同名`.resources.json`、`apk-validation-bronze-final.log`。CRC/签名/ARM64、精确7/1/1/14场景、40项源资产与APK SHA绑定均通过，并独立重算四个文件SHA。下表为本版。最低采样空闲RAM3.280/5.523/5.369/3.233GiB，原生编译进程2/0/0/2（0为缓存复用），仍是五秒离散采样；没有提高Java/Gradle配额。全模型/时间序列、六场景29按钮流程、14原场景启动0运行时错误通过：`bronze-final-{model-timeline,six-scene-flow,all14-startup}.log`。三变体实际场景共13张回调截图另见 `bronze-beast-three-variant-flow-fixed.log`；不是物理键盘/手势或头显通过。
- 青铜兽采用Poly Haven Tina的CC0 Bull Head，原始17,148面/1K PBR与UV保留，新编两骨固定底座苏醒；铜材质通道转换逐字节核对。35cm拟合/37°朝向的129个独立蒙皮采样底部误差约0.00003mm、最大动作7.47mm、循环接缝0；512次更新0托管分配。扫描环按真实模型范围运动，Statue Stop门控已修复，石雕/青铜兽不再整体拉伸。96帧/24fps离线预览逐帧解码通过，MP4为276,044字节；通用牛首表现，不宣称某一博物馆藏品复原。
- YOLO数据继续审核：现有照片未新增下载，剩余114张已全部做RGB框图初筛，78张记录需复核问题，6张另查原图；`TrainingData/openimages-exact-review-progress-v1.json` 含图像/来源清单SHA、逐图作者许可/原框和审核范围。原图确认同一扶手椅的chair/couch重复类别、明显沙发/猫漏标、厨房台面误框、蜘蛛腿出框及野生猫科域问题；缩略图“玩偶猫”怀疑经原图撤回。114张仍未获训练标注接纳，未标问题不等同干净负样本。
- 按用户新增要求，两个真实训练入口均接入 `Tools/yolo_training_logs.py`：每次最多写1MiB开头+64KiB结尾，中间超额仅在内存滚动，不持续轮转写盘；成功日志最多保留最近2次，失败/未完成日志及元数据不清理，总目录8MiB预算满则拒绝新训练。清理只操作本工具SUCCESS回执绑定的prefix/tail，模型、原始数据、训练指标和历史未登记日志不动。7项无模型回归在Windows和真实Linux训练解释器均通过，覆盖大Unicode日志截断/异常尾部/旧成功清理/失败保留/路径越界/不覆盖。现有20份训练相关历史日志合计99,825字节，无大日志可回收；本次清理0字节，不声称释放大量空间。
- 本轮构建结束检查：宿主空闲RAM9.02GiB、显存5070MiB、C/D盘208.28/415.99GiB，任务Editor/Java/clang/Blender/Python已退出，仅保留既有Hub后台服务；Ubuntu-24.04训练环境无模型加载、交换空间当时0。ADB无设备、PICO三必需开关仍为1；未重启。全部五个远程分支只读复核提交未变。训练扩充/正式精度、服务部署与头显验收继续未完成，Goal保持active。以下旧“当前/未训练”条目均为历史阶段记录。
- 最新为桌面退出引用修复/共享唤醒按钮版四包，全部构建与核验通过：`.codex-tmp/android-{main,pose,repair,allscenes}-desktop-fix-final.log`、同名资源JSON、`apk-validation-desktop-fix-final.log`。CRC/签名/ARM64、精确7/1/1/14场景、35项源资源及APK SHA绑定、猫狗步态名均通过；下表与 `apk-validation.json` 已更新。Main/Pose/Repair/AllScenes最低采样空闲内存3.391/5.249/5.162/3.510GiB，原生编译进程2/0/0/2（0为复用缓存），采样不是连续峰值；本轮无OOM，Java/Gradle配额未提高。全模型/14场景结构/六场景时间序列与14原场景实际启动0错误通过，日志 `desktop-fix-model-timeline.log` / `desktop-fix-all14-startup.log`。构建结束空闲RAM9.03GiB、显存5068MiB、C/D盘208.22/416.10GiB；任务Editor/Java/clang/Python退出，保留Hub后台及既有WSL shell，未自动重启。ADB无设备，PICO三必需开关仍为1。YOLO已真实短训但尚未正式精度/部署验收；真实头显验证仍未完成，goal保持执行。下方其他包与旧“未训练”条目是历史记录。
- 当前为虫/树虚拟变体及树皮标记版，四包全部构建/核验通过：`.codex-tmp/android-{main,pose,repair,allscenes}-variant-bark-final.log` 与资源JSON、`apk-validation-variant-bark-final.log`。签名/CRC/ARM64、精确7/1/1/14场景路径、35项BuildReport资源清单与APK SHA绑定、猫狗步态名均通过。Main/Pose/Repair/AllScenes最低采样空闲内存3.278/5.254/5.315/3.354GiB；Main/AllScenes实测原生编译最多2进程，Pose/Repair复用缓存；五秒离散采样不代表完整峰值。本轮无OOM，保留Java堆2GiB/元空间512MiB/CPU2/公共池1/Gradleworker1，独立签名核验另限Java512MiB。结束空闲内存9.06GiB、C/D空闲208.86/416.28GiB，任务Editor/Java/clang/Blender/WSL退出，仅既有Hub服务保留。ADB无设备，PICO三必需开关仍为1；未训练/未重启。远程五分支再次复核提交未变。当前SHA见计划表与 `apk-validation.json`，下方旧包为历史记录。goal保持执行，真实手势/识别/双眼/性能及缺失自定义权重保持未完成。
- 虫类Cockroach/Spider/Test defeat与树Natural/Fantasy/Test awaken已补虚拟入口，Reset可直接重生；切换门控、树动作中断保留手动拟合/朝向、旧表面与历史清理、贴合树皮标记/安抚/Undo已通过真实场景回调检查。树反馈改为至多六个暖色小光点，风动季节反馈保留。详见开发接续记录顶部及 `variant-bark-motes-final.log`。
- 全模型/14场景结构/六场景18帧、六个真实Park场景27按钮/30帧及全部14原场景启动0运行时错误通过；日志 `variant-bark-{model-timeline,six-scene,all14}-final.log`。合成动作/回调与独立渲染仅作本地证据，真实指针/手势/双眼/性能仍需PICO。

## 原生编译限流与狗资源接续（2026-11-15）

- 阅读角细化版四包已全部构建/核验：`.codex-tmp/android-{main,pose,repair,allscenes}-room-detail-final.log` 与资源JSON、`apk-validation-room-detail-final.log`。四包CRC/签名/ARM64、精确7/1/1/14场景、32项真实BuildReport资源/SHA绑定、猫狗步态名、家具作者/CC0与材质转换说明均通过；未接受宠物、错误UV2法线和未使用原始ARM未入包。最低采样空闲内存3.202/5.349/5.565/3.648GiB，Main/AllScenes实测最多2个原生编译进程，Pose/Repair复用缓存；五秒离散采样不代表完整峰值。本轮无OOM，保持Java堆2GiB/元空间512MiB/CPU2/公共池1/Gradleworker1。结束可用内存9.26GiB，C/D空闲209.12/416.28GiB，任务Editor/Java/clang/Blender/WSL退出，仅既有Hub后台服务保留；PICO三个必需开关仍为1，ADB无设备。未训练、未重启。远程五个分支再次只读复核、提交未变。goal保持执行，后续补虫类/树模式的虚拟变体入口并继续剩余验收；实机部分仍需连接PICO。
- ParkRoom 已细化为自然材质阅读角：官方CC0 Modern Arm Chair 01（Vibrant Nordic，原始8,916面）与Ceramic Vase 01（James Ray Cock，实际glTF10,296面），1K原图/UV/网格完整保留；两组官方清单合计3,116,489字节，逐文件大小/MD5/SHA校验，无高分辨率大包。新增分板地面、真实窗洞/窗框/窗台、薄地毯、书本封面/页块、圆边台面与金属底座；模型坐标下小倒角、连续灰泥UV及按尺寸投影木纹。全部静态部分24,204三角面/9个renderer，缓存共享且不增加动态光/刚体。ARM数值转为URP R金属/G遮蔽/A光滑度，同一张图供metal/AO使用，线性导入；原始ARM不作错误通道绑定，转换通道误差小于1.2e-7。来源、原SHA和修改说明随资源保存。
- `.codex-tmp/room-detail-lifecycle-validation.log` 通过模型/切线/非退化三角面、支撑底部与名义尺寸、实际窗洞射线、材质映射、14场景结构和六场景动画18帧；房间真实Play Mode10帧中家具固定，水母渐显/脉动/收起/Undo/模式切换、确认天花板下持续24颗糖果/移动锚点/Stop/Reset/丢失恢复/退出/音源均通过。`room-detail-six-scene-flow.log` 六个真实场景菜单/30帧通过，`room-detail-all14-startup.log` 全14原场景启动0运行时错误。首次导入材质映射丢失已分离贴图重导入与材质保存阶段；实际渲染还发现跨场景原生Mesh已卸载而托管字典仍有项，现按Unity对象有效性重建缓存，并在渲染前要求可见Mesh有效。失败和诊断日志保留，不把最初缺建筑的画面作为完成证据。
- 完整阅读角水母相位预览 `.codex-tmp/room-motion/ReadingCornerPulse.mp4` 已生成，640×640/30fps/84帧/2.8s；帧序列17,465,414字节，小于24MiB固定上限。本机单线程编码，与独立水母片段共同通过解码、PNG颜色/中帧变化检查，报告 `room-motion/movies-validation.json`。这是固定家具+离线shader相位，不含实际输入/世界漂浮或头显录像，实际Play Mode另有上述证据。四份APK及32项资产核验已通过（见最新条目）。完整阅读角视频189,547字节，平均首帧颜色误差1.622/255、中帧变化1.042/255，解码84帧。
- 接续可操作性审查发现：当前蜘蛛已有模型/步态与代码/识别选择路径，但虚拟界面仍默认蟑螂；树的自然/魔法模式目前只在桌面T键切换。后续补齐头显可操作的虚拟变体入口，并检查模式中断/复位，避免把测试脚本可调用等同于完整用户入口。房间空间细节的本地模型/效果工作已完成，实机空间、手势、透明排序及帧率仍未验收。
- 猫狗步态/虚拟入口版四包已完成构建与核验：日志 `.codex-tmp/android-{main,pose,repair,allscenes}-pet-walk-final.log`、资源JSON、`apk-validation-pet-walk-final.log`。四包均验证CRC、签名、ARM64、精确7/1/1/14场景路径、18项源资产/SHA绑定、猫犬署名及新增步态修改说明；还直接在player数据检出 `BicolorCat_Walk` 与 `Wolfhound_WalkSource` 两个动画名。最低采样空闲3.369/5.471/5.435/3.466GiB；Main/AllScenes最多2个原生编译进程，Pose/Repair复用缓存（样本0不算冷编译），本轮无OOM，Java配额不变。当前四包数据见下表/JSON；猫狗步态及入口本地验收已完成，完整14场景原始启动复验通过，0运行时错误，日志 `pet-walk-all14-startup-final.log`；保留原相机和输入组件，桌面缺少AR provider及PC服务的警告按原说明记录。空间细节及实机输入/双眼/性能等验收仍未完成，goal保持执行。
- 本轮结束空闲内存9.19GiB，C/D空闲209.26/416.28GiB；无任务D盘Editor/Java/clang/Blender/WSL重进程，Unity后台服务保留，PICO必需三开关仍为1，ADB无设备。未训练、未重启。复现：单线程Blender运行 `Tools/prepare_bicolor_cat.py`（需固定SHA原始GLB），Unity执行 `NaturalPetWalkPlayChecks.Run`；该Play Mode入口自行退出，批处理不要附加 `-quit`。四包用 `Tools/build_android_bounded.ps1` 顺序构建，再运行 `Tools/verify_android_artifacts.py --unity-data D:/Unity/Hub/Editor/6000.0.26f1/Editor/Data --include-all-scenes`。猫狗原地步态/入口本地工作完成；接续推进昆虫与空间细节、剩余场景验收及实机输入/头部/双眼/帧率，缺失识别权重继续显式未配置且默认不训练。
- 猫已补2s四拍原地慢走（原网格8,646面/40骨、毛色/眼睛/UV0法线不变）：`Tools/author_cat_gait.py` 在源骨架上做解析腿链求解、按骨骼权重选取真实脚掌表面并校正接触，不增加骨骼、不改权重、不训练。源Blender193个采样支撑误差0.166mm、循环顶点差0；Unity在0.35m手动拟合/37°转向下193采样最大支撑误差0.176mm、四足抬起约19.18mm、首尾差0，定位根固定。实际ParkPet的Preview Cat/Preview Dog/Walk in place按钮、Animator步态、回待机、喂食中断/忙碌拒绝切种、Undo/Stop/Reset、丢失/恢复/物理目标及live门控、退出/音源清理均通过；最终日志 `.codex-tmp/pet-walk-final-ui-idempotent.log`，5张原相机场景图。旧梦境/喂食生命周期另通过 `pet-walk-pet-lifecycle.log`。重复生成曾使犬控制器累积旧行走状态，已修复生成器状态复用和清理；初次回归失败日志保留。新按钮仅在虚拟Pet模式显示，其他场景菜单保持原尺寸；步态是原地预览，不推断真实宠物行走。14场景结构/自然模型与六场景实际动画18帧通过 `pet-walk-all-model-timeline.log`；六个实际场景界面/30帧通过 `pet-walk-six-scene-flow.log`。进一步的实际Animator启停过渡100次离散采样未见穿地，`pet-walk-actual-transition-ground.log`；喂食打断从已经播放的Walk状态验证。本轮四包更新/动画入包核验已通过（见最新条目）。
- 四份小型动作预览已更新：`.codex-tmp/pet-motion/CatIdle.mp4`、`CatSniff.mp4`、`CatWalk.mp4`、`DogWalk.mp4`；640×640/24fps，分别60/60/48/26帧（2.5/2.5/2/约1.083s），59,793/64,606/77,849/81,782字节。单线程本机编码、逐帧解码及PNG颜色/中帧变化通过 `pet-motion/movies-validation.json`；四序列合计约21.10MiB。离线直接clip采样/显式蒙皮渲染，不是实时头显录像，实际Play Mode证据另列。四拍时序参考成年猫的同侧后足先于前足的行走序列（https://journals.physiology.org/doi/full/10.1152/jn.00240.2026），不是实验数据或运动捕捉复原。重新只读复核全部远程分支，仍为已审查的五个refs且提交未变，未重复下载。
- 猎狼犬接入后的四包已完整构建/核验通过：`.codex-tmp/android-{main,pose,repair,allscenes}-natural-dog-final.log`、同名资源JSON、`apk-validation-natural-dog-final.log`。CRC/签名/ARM64、精确7/1/1/14场景路径、猫犬许可署名、18项真实BuildReport源资产与APK SHA绑定均通过；未接受獒犬/Innerscene候选和错误UV2法线未入包。原生编译最多2个（Main/AllScenes实测；Pose/Repair复用缓存，样本0不算冷编译证明），最低采样空闲3.206/5.743/5.751/3.447GiB；仍是5秒离散采样，不称完整峰值。实际Gradle守卫核对CPU2/公共池1/2GiB堆/512MiB元空间。本轮无JVM OOM；首次批量调用在主包实际成功后误读残留LASTEXITCODE而停止，已修正调用层，只续建后三包，没有重复重建成功主包。结束时空闲内存9.17GiB、C/D空闲209.28/416.28GiB；任务Editor/Java/clang/Blender/WSL均退出，仅既有Hub服务保留，PICO三开关仍为1，ADB无设备。未训练、未重启；猫步态/产品行走入口、空间细节、缺失识别权重和实机头部/双眼/性能验收仍待完成，goal继续执行。
- 猎狼犬已替换正式 Dog 的临时球体模型：Wildfire Games / 0 A.D. CC BY-SA 3.0，原始849面/47骨/256px毛色，源待机/观察/原地行走及新编2.5s嗅闻；新增 Generic 导入器、URP 材质、控制器和 Resources prefab，幻想气泡绑定真实 Head 骨。仅修改狗资产适用同许可，来源、固定提交、逐文件SHA与修改说明/包内署名保留。Unity 初次实际渲染发现相反前向的 FromToRotation 选轴会倒置模型，改为仅绕Y转向并新增头高于身体断言；不是更改跟踪根来遮蔽错误。`.codex-tmp/natural-dog-review-final.log` 验证真实Play Mode 15个动作帧/3视角、嗅闻自动回待机/Reset、原地源步态；正式猫狗生命周期、两个头部气泡、两种嗅闻及门控/Undo/丢失恢复/退出通过 `natural-dog-integration-final.log`，10帧。五个独立蒙皮拟合姿态脚部位移0、最大皮肤位移12.717mm、头骨旋转8.292°，37°转向和0.35m拟合底部通过；静态拟合直接采样源clip，实际Animator另由Play Mode检验。原始头骨轴心不平移，不以轴心位移代替动作证据。最初失败日志均保留。14场景结构/全部自然模型与六场景真实动画18帧通过 `natural-dog-model-timeline-final.log`，六个实际场景界面流程/30帧通过 `natural-dog-six-scene-flow-final.log`；四份APK已完成构建与18项资产核验（见最新条目）。猫步态/实机定位与性能仍未完成，goal保持执行中。
- 针对已观察到的16个clang子进程，安装官方记录Bee后台并发修复的固定补丁 `6000.0.33f1 / 433b0a79340b`，使用独立 `D:/Unity/Hub/Editor/6000.0.33f1` 目录并保留旧编辑器。独立小项目冷构建通过后，真实项目已由补丁版打开并更新ProjectVersion，旧版本文本保存在 `.codex-tmp/project-version-before-bee-patch.txt`。真实项目四包与场景回归已通过，细项如下；不把小项目当作PICO实机证据。
- `Tools/fetch_unity_patch.py` 只读取官方固定版本清单，并顺序分块下载编辑器/Android支持。总下载上限5GiB、单块1MiB、D盘先保留30GiB，验证官方MD5并记录SHA-256。官方清单大小按KiB取整，两个HTTP精确大小各比清单多680/640字节；首次严格长度拒绝保留原因，现要求HEAD实际大小与清单误差小于1MiB且最终完整MD5一致，没有放宽总量/证书检查。不重复下载SDK/NDK/JDK。
- `Tools/install_unity_patch.ps1` 执行前再次核对SHA-256及有效Unity发布者签名；安装成功，临时解压也在D盘，没有重启。结果 `unity-patch-install.json`。编辑器/Android模块实际下载共4,505,863,464字节；SHA分别为`f84ff4ec163aeb91ac1feb45a22c15a78b8d2d6d450d512058304112f07ba5db`、`28fea2c59c1e72cb2e35aa9b8fbacd1a0d8fc3a7116cfc8bd2d9d4b9c5e42d83`。
- `build_android_bounded.ps1` 新增作用域内 `BEE_BUILD_THREADS=2` 与旧版本提示，finally恢复原环境；保留原2GiB堆/512MiB元空间/单Gradleworker。每5秒记录本次Unity后代中的原生编译进程、Bee参数和空闲内存，最多720条驻留RAM，结束时一次写入 `<LogPath>.resources.json`。这是离散采样，不称连续峰值测量。`-Variant Probe` 只运行固定独立小项目；可通过 `-AndroidToolchainRoot` 指向已有包含SDK/NDK/OpenJDK的目录，构建回调作用域内切换并恢复路径；内置模式通过空值恢复，避免把未安装的内置路径当作有效自定义路径而抛异常。
- 对齐项目现有Activity/关闭优化帧同步配置的独立冷构建已完整通过：`bee-cold-native-project-config-probe.log` 与资源JSON，21次离散采样最多2个clang、最低可用内存7.488GiB，ARM64 APK 14,698,992字节、SHA`30904292c25464dc3528b40b737e6bcf5aea5632ad4d494ed06231a30ec0248b`。调用者BEE/Java/工具链环境恢复检查通过。前两次默认GameActivity/优化帧同步的额外CMake路径失败，错误只返回JDK选项提示，细分根因未完全隔离；日志 `bee-cold-native-probe.log`、`bee-cold-native-activity-probe.log` 保留，不算通过。这些额外配置在当前受限启动方式下尚未验收，本项目原有配置不使用这些目标。
- 6000.0.33f1的Bee本体直接读取线程环境变量，命令行未额外出现`-j`并不表示未生效；与旧本体不含该变量不同，独立冷构建和真实项目冷编译/14场景Release编译均实际观察最多2个clang。
- 真实项目第一次补丁版完整构建通过脚本/模型/共享交互与原生编译，但在 `packageDebug` 的Zipflinger缓冲分配处再次发生2GiB Java堆不足。`android-main-bee-patch-compatibility.log` 及资源JSON保留：126次采样，最多2个clang，最低可用内存2.172GiB。没有提升堆或元空间，也不再把只移除旧APK输出当作已解决OOM的证明。
- 读取本机AGP8.3.0库的字节码确认：`ApkFlinger` 将压缩任务提交到 `ForkJoinPool.commonPool()`，不受Gradle单worker设置直接控制。独立JVM探针默认CPU16/公共池并行度15；新增 `-XX:ActiveProcessorCount=2` 和 `-Djava.util.concurrent.ForkJoinPool.common.parallelism=1` 后实测CPU2/公共池1，仍为2GiB堆/512MiB元空间。参数依据 [Java17 ActiveProcessorCount](https://docs.oracle.com/en/java/javase/17/docs/specs/man/java.html) 和 [ForkJoinPool公共池配置](https://docs.oracle.com/en/java/javase/17/docs/api/java.base/java/util/concurrent/ForkJoinPool.html)。这是发现并限制了额外并发来源，尚不宣称唯一根因已完全隔离。
- Unity退出后的独立实际主线打包16s通过，`android-main-java-pool-diagnostic.log` 内实际Gradle JVM输出 `cpus=2 common_pool=1 heap=2147483648 metaspace=536870912`。完整Unity主线复验也通过：`android-main-bee-java-pools-final.log`，对应Gradle daemon-21068日志的实际配额行与上述一致（Unity成功构建默认不回显全部Gradle stdout）。构建回调在受限流程生成的Gradle脚本中增加运行时配额检查，避免只打印配置值；未增加Java内存。该完整复验采样最低空闲5.416GiB，C++复用了前轮已由2线程编译的缓存，不能把本轮0个新clang样本当作又一次冷编译证明。
- 工具默认编辑器已更新为独立6000.0.33f1，仍复用旧6000.0.26f1目录下SDK/NDK/JDK，旧编辑器也保留。补丁版 `bee-patch-model-timeline.log`（全部自然模型/14场景结构/六场景18帧）、`bee-patch-all14-startup.log`（14个原场景启动，0运行时错误）、`bee-patch-six-scene-flow.log`（六场景30帧实际界面流程）、`bee-patch-legacy-animation.log`（五个打火机35帧、两捕捉场景/VST配置）全部通过。输入隔离与合成点/真实设备边界保持原说明。
- 四包全部成功，日志 `android-{main,pose,repair,allscenes}-bee-java-pools-final.log` 和同名 `.resources.json`。最低采样空闲内存分别5.416/5.496/5.425/2.633GiB；前三包复用原生缓存，新clang样本为0，14场景Release重新编译实际最多2个。`apk-validation-bee-java-pools-final.log` / `apk-validation.json` 核对四包CRC/签名/ARM64、精确7/1/1/14个场景路径、署名及BuildReport中16项源资产清单与APK SHA绑定均通过，当前数据见下表。这是前一轮补丁/JVM限流构建记录，当时尚未含犬候选；当前猎狼犬整合见本文件顶部。
- 完整主线与运行回归通过后，仅删除本任务两个已验证安装器叶文件，回收4,505,863,464字节；签名/校验记录保留，旧编辑器、共享工具链与既有Blender/WSL安装缓存未动。清理后D盘约416.28GiB可用，不做递归清理用户目录。
- 在下载期间审查 Wildfire Games 官方獒犬/猎狼犬资源，固定0ad提交 `61a3b9507d974084e6badb88a0826bd89a6d5b8b`，来源和逐文件SHA保存于候选。两组网格、动画、256px原始贴图与许可共约5MiB，不克隆大型仓库、不训练。资产遵循[官方CC BY-SA 3.0署名及修改许可](https://raw.githubusercontent.com/0ad/0ad/master/binaries/data/mods/public/art/LICENSE.txt)，与项目代码分开记录。
- `Tools/prepare_natural_dog.py` 单线程导入/渲染，完整变形骨的绑定姿态一致性检查通过；猎狼犬额外的 `prop-collar` 经核实没有顶点权重，且未导入单独项圈。獒犬880面/48骨含战斗刺项圈，暂不接受作普通宠物；猎狼犬849面/47骨，原始待机20s、观察9.625s、行走约1.083s，三视角Blender图位于 `.codex-tmp/natural-dog-review`。新增2.5s头部/上颈小幅嗅闻，保留原始旋转通道模式；五个实际变形姿态验证56个足部顶点最大位移0，头骨尾端最大位移约3.19mm（不是鼻尖位移），模型根不动，结果 `wolfhound-sniff-candidate.log` / 来源JSON。当时仅为Blender候选证据；当前猎狼犬已完成正式接入及本地Unity/Android验收，见顶部最新记录。
- 收尾时空闲内存9.23GiB，C/D空闲209.56/416.28GiB；任务Editor/Java/clang/Blender/WSL均已退出，仅既有Hub服务保留。PICO三个必需开关仍为1，ADB仍无设备；未训练、未重启。该轮结束时计划接入猎狼犬；当前已接入，后续推进猫步态、产品行走入口、空间细节与设备缺口，goal继续执行。

## 六个实际 Park 场景的界面流程（2026-11-15）

- 新增 `ParkSceneFlowChecks.Run`：逐一打开六个保存的 Park 场景并进入真实 Editor Play Mode，使用场景原相机/组件，不关闭输入或替换目标。通过实际生成的 Button 回调进入虚拟预览、Fit/Plane、植物 Test bloom、Stop/Reset、内容菜单与回放候选；其余模型动作使用公开 controller API。Fit/Plane 点和动作位置是明确的合成输入，按钮中心射线检查不等同真实鼠标/手势验收。
- 复现虚拟模式点击内容菜单后只清除模型、等待真实定位的空白预览故障，保留 `.codex-tmp/park-actual-scene-flow-baseline.log`。现在同一模式切换会生成所选内容；真实模式仍等待定位。Virtual test 从回放切入时明确退出回放，清理旧图像/候选/识别锁、恢复接收器；再次点击回到真实模式，避免 REPLAY 标记与 simulation 状态冲突。
- 原虚拟对象偏移太靠右，实际原相机截图可见树冠裁切；只将虚拟预览横向偏移从0.85m改为0.5m，保持相机和真实定位路径。合成虫体尺寸测试采用2cm高，其他场景30cm高，不能把30cm高的虫体测试当自然预览。
- 校准后的模型退出原先调用 ResetEffects，导致缩小时突然恢复默认大小/底部；ExitAndDestroy 现在保留原表示层的拟合位置/尺度直到退出结束。Controller 清理手动表面时不再提前 Reset 同一个模型；仍在退出/销毁流程统一清理动作、音源、锚点和特效。验证包含 Fit 后重新确认表面的情况。
- 最终 `.codex-tmp/park-actual-scene-flow-surface-final.log` 通过全部六场景（0条运行时错误）。每场景5张原相机截图：虚拟进入、动作、保持拟合的退出、切换内容、回放选择，共30张/1,996,610字节，保存在 `park-scene-flow`。六份JSON绑定同一个运行ID，开始/失败会覆盖总结果状态，拒绝借用旧的通过报告。覆盖忙碌时拒绝切换、植物按钮开花、两类幻想Stop、Reset中断、回放候选实际回调、回放→虚拟→真实状态/旧目标/音源/手动表面清理。早期候选选择诊断误把识别框也当候选按钮，已限定候选面板；该诊断失败保留，不算产品失败或通过证据。
- 四份 Android 包顺序构建/核验通过：`.codex-tmp/android-{main,pose,repair,allscenes}-actual-scene-flow-final.log`，报告 `.codex-tmp/apk-validation.json`、`apk-validation-actual-scene-flow-final.log`。CRC、签名、ARM64、精确7/1/1/14个场景路径、署名和16项SHA绑定源资产全部通过；大小/哈希见下表。本轮未出现JVM OOM，仍是2GiB堆/512MiB元空间/单Gradleworker，没有训练/重启。
- **纠正原生编译并发表述**：`-job-worker-count 2` 和 IL2CPP `--jobs=2` 没有把当前6000.0.26f1的Bee/C++子进程限制为两个。Main阶段实际观察16个clang进程，约5.43GiB可用内存；AllScenes阶段另一次16进程合计工作集约2.535GiB，可用4.30GiB，不是完整峰值记录。不能把既有参数记录当成有效原生限流证明。[Unity 6000.0.33f1官方更新说明](https://unity.com/releases/editor/whats-new/6000.0.33f1)才列出向Bee后台扩展`BEE_BUILD_THREADS`的修复；当前未升级编辑器、未替换安装二进制。原生编译限流/持续资源采样仍是后续配置缺口。
- 构建结束任务Editor/Java/clang/Blender/WSL均已退出，只保留原有Hub服务；当时可用内存9.20GiB，C/D空闲211.19/425.56GiB。ADB仍无设备，PICO真实相机/手势/双眼/帧率未验收。狗模型、猫自然步态、房间细节仍未完成，不以本流程代替这些要求。

## 旧入口实际启动、核心动作和卡牌修复（2026-11-15）

- 修正 `ProjectSceneAudit` 只接受 `Assembly-CSharp` 的筛选错误，改为按 MonoScript 的项目资源路径收集；独立 `MagicMR` 程序集的 RealityEditor、引导与手势组件不再遗漏。缺失脚本/空材质仍由实际场景检查，不能以清单漏项推断脚本不存在。
- 第一轮八个实际旧/桥接场景未经隔离启动共记录五条错误：可选测试图像为空却自动启动，以及编辑器不支持设备相机却报告异常。原始证据 `.codex-tmp/legacy-startup-baseline.log`、`legacy-startup-baseline` 保留。现在无图像时发送器明确停在 `NOT_CONFIGURED`；手动 Begin 的缺失配置仍报告错误。非 Android 设备相机状态为 `UnsupportedPlatform`、`HasFrame=false`，没有生成替代相机帧。
- `LegacySceneMaintenance.Run` 将 BridgeTest / MagicMR 的基本打火机换为已有圆角壳体、金属风罩/滚轮与细节，保留根位置、旋转和 `.025/.08/.012` 名义尺寸。仅把继承的旧机器默认地址 `192.168.1.5` 改为 `127.0.0.1`，不覆盖其他自定义地址。VstTest 实际挂接已有的 `VstTestRunner`，停用重复的一次性开关脚本；验证相机 alpha=0、关闭后处理/不透明 Volume，不等同实际透视成功。
- 五个**实际保存场景中的原目标**完成碳化材质/复位、生命化脉动/眼部、0.2m 躲闪与返回、三倍放大/内部结构显示、支撑点变花、立即取消、跨维度取消及复位材质/音源/花朵清理，共35张状态图。日志 `.codex-tmp/legacy-model-animation-startup-validation.log`，图和各场景 JSON 在 `legacy-animation`。检查明确暂时关闭定位/输入组件，并打开未召唤渲染遮罩用于模型审查；没有注入识别结果，也不将其计作真实跟踪/手势/锚点验收。未经隔离的启动截图另存。
- 修复 `RealityEditor.ResetTarget` 未停止动作音源，以及碎解特效/花朵在取消、换维度或销毁目标时的归属清理；碎解爆发只保留当前目标的一份，正常自动寿命仍保留。复位和中途换维度后等待超过原延迟，不产生迟到花朵。
- SampleScene 的截图相机原本依赖首次捕捉才查找，且一直渲染；现在启动时解析专用相机并关闭连续渲染，捕捉时主动 Render，保留主观察相机。更重要的是旧 `2D_Card_UI` 直接位于共享 Canvas 下，原动画会隐藏/缩放/移动整个 Canvas 和背包；`CaptureTo2D` 现仅动画独立卡牌容器，遇共享 Canvas 或包含背包的父容器则使用卡牌自身。
- 两个实际捕捉场景通过非空纹理、重复请求拒绝、中途复位/复用、`timeScale=0` 下真实卡牌移动与完成、禁用清理；背包在整个过程始终可见且位置固定，专用相机平时禁用。日志 `.codex-tmp/legacy-card-layout-final-validation.log`，两张卡牌纹理在 `legacy-animation`。这不是网络上传功能；SampleScene 的 AR 图像/输入/相机 provider 在桌面缺失仍有4条预期警告，实际 AR 尚未验证。
- 旧模型审查最初遇到空图，是未召唤目标的 `forceRenderingOff` 门控所致；保留 `.codex-tmp/legacy-actual-animation.log`。只调整诊断审查的隔离显示，没有放宽产品跟踪门控。卡牌专用相机/共享Canvas的问题随后由实际场景检查发现并修正，先前失败不算验收通过。
- 最终扩展为**全部14个实际场景**逐一进入Play Mode，未注入跟踪、未关闭入口组件、未重定位原相机，保存14张启动截图与逐场景组件/输入状态JSON，0条Error/Exception。日志 `.codex-tmp/all14-actual-startup-final.log`，结果 `legacy-startup/result.txt`。真实定位尚未召唤时模型继续受门控，Park入口等待操作时`parkActors=0`，不能据启动图宣称已完成真实场景交互；模型/动作证据另列。
- 四份当前APK均通过CRC、签名、ARM64、包名、署名、16项SHA绑定的源资产清单，并直接解析APK `globalgamemanagers` 核对精确的7/1/1/14个场景路径，不只数level文件。常用三包日志 `.codex-tmp/android-{main,pose,repair}-legacy-validation-final.log`；独立 `AllScenesValidation.apk` 完整包含14场景，包名`com.yn.picmagicmr.validation`，日志 `android-allscenes-fresh-release-final.log`。它是编译/打包验证产物，没有新增14场景导航产品流程，也不代表实机通过。
- `Tools/build_android_bounded.ps1 -Variant AllScenes` 可复现14场景验证包（Release模式、独立应用ID）；该模式仍复用2GiB堆/512MiB元空间/单Gradleworker与两Unity/IL2CPP任务。生成目录校验后只移除已知的debug/release APK叶文件，保留编译缓存与正式Builds产物；Release全新打包已验证。`Tools/verify_android_artifacts.py --include-all-scenes --unity-data <Unity Data>` 验证四包，默认仍验证原三包。当前检查内存空闲约9.29GiB，C/D空闲211.2/425.6GiB，无任务Editor/Java/WSL/Blender残留，未训练/重启；ADB仍无头显。

## 视觉方向修订

用户已否定此前生成的六格卡通图，要求成熟、自然比例与真实材质。该图仅在聊天中预览，没有作为项目贴图或最终模型使用。后续不能以大头、夸张眼睛、笑脸树、玩具比例替代写实目标。

### 房间水母灯与稳定家具（2026-11-15）

- 用原创共享网格替换球体/六根直杆水母：5,730 顶点、9,248 非退化三角面，一个 renderer/材质，无贴图、骨骼或动态灯。造型参考 [Monterey Bay Aquarium 的月水母资料](https://www.montereybayaquarium.org/animals-the-ocean/animals-a-to-z/moon-jelly)：薄伞体、短缘触手与四条褶皱口腕分开建模，内侧四个低饱和马蹄形结构；不是科学物种还原或天然发光的陈述，没有复制外部照片或模型。
- `NaturalJellyfishAssetBuilder` 生成约 738KiB 的共享 mesh；shader 使用固定幅度的 GPU 伞体收缩/口腕波动与半透明薄膜，兼有双眼宏，尚未据此宣称 PICO 双眼透明排序/过绘制/帧率已通过。`NaturalJellyfish.Step` 只写缓存的材质参数：512 次更新 0 托管分配，不上传网格、不移动跟踪根，非法时间/Reset 可恢复。
- 家具不再随唤醒整体呼吸、拉伸和摇摆；水母独立渐显、漂移、收起和重启。水母唤醒的 16 个黄色星星改成六个约 9mm 的冷白小光点，复用原有 32 项池。幻想糖果玩法保留，修正粒子起点到手动确认的水平天花板点下 2cm，最多下落约 31cm；不是房间扫描或物理碰撞模拟。
- 最终 `.codex-tmp/natural-room-loop-warm-final-validation.log`：非退化网格/法线/包围盒、四阶段 shader 渲染及循环边界、14 场景结构和六场景模型/共享界面检查通过。真实 Play Mode 保存 10 帧，覆盖固定家具、水母渐显/脉动/Stop 渐隐/Undo/重启/模式切换、天花板门控、24 项糖果池持续超过 3s 且始终在确认点下方、移动确认点后跟随、Reset/跟踪丢失/恢复/退出与音源清理。随后六场景 Play Mode 时间序列（18 帧）通过。
- 首次 `.codex-tmp/natural-room-integration.log` 在场景加载的约 1.7s 长帧上发起手势，错过渐显中段；该次失败保留。后续截图预热也曾造成 386ms 长帧，失败日志为 `natural-room-loop-final-validation.log`。现先完成预热截图，再观察五个新的真实稳定帧（dt<50ms）才触发动作，仍在固定0.38s检查部分透明度，并检查Stop中间帧渐隐；没有把加载/截图停顿当连续输入证据。最终全部通过。
- 复查发现初版触手用0.9/0.75倍频率而相位每2π回绕，会在边界跳变；现改用整数时间频率，并以实际GPU渲染检查周期前后±0.1ms，平均RGB差约0.0105/255。结果 `natural-jellyfish-review/loop-validation.txt`，包含前后两张图；不是仅由参数数值推断循环连续。
- 预览 `.codex-tmp/room-motion/JellyfishPulse.mp4` 为修正后同一运行时 shader 的离线采样，640×640、30fps、84 帧/2.8s、237,014 字节。帧序列约 4.81MiB，固定上限 24MiB；单线程本地编码、逐帧解码帧数及首帧颜色/中帧变化检查通过。实际 Play Mode 证据另列，视频不是头显录像。
- 最终水母循环版三份 APK 全部构建/核验通过，日志 `.codex-tmp/android-{main,pose,repair}-natural-room-loop-final.log`；CRC、签名、ARM64、7/1/1 场景、署名及16项SHA绑定的实际源资产清单通过，当前大小/哈希见下表。本轮初次Repair在单worker下仍出现 `packageDebug/IncrementalSplitterRunnable` OOM，失败日志 `android-repair-natural-room.log` 保留。相同2GiB堆下退出Unity后的独立Gradle诊断及完整Unity全新打包均成功，具体细分根因未完全隔离。
- `ResourceBoundedAndroidBuild` 现在在已核对的项目 `Library/Bee/Android/Prj/IL2CPP/Gradle` 内，仅移除可再生成的 `launcher/build/outputs/apk/debug/launcher-debug.apk`，避免沿用上一个app ID的增量输出；保留IL2CPP/Gradle缓存和正式Builds产物。生成目录不匹配时拒绝清理。增加 [Gradle 官方说明的错误栈配置](https://docs.gradle.org/current/userguide/logging.html)，未提高堆、元空间或并发；最终三包连续全新打包成功。本轮结束可用内存约9.34GiB、C/D空闲212.3/425.6GiB，任务Editor/Java/Blender/WSL已退出，未训练、未重启，ADB仍无头显。
- 重新只读查询 GitHub 全部分支，仍为原来的五个 refs，提交均与审查清单一致；没有重复拉取大仓库。狗模型、自然猫步态、进一步空间细节、剩余场景运行/手势与真实设备验收继续保留未完成。

### 自然猫、法线 UV 修复与宠物交互（2026-11-15）

- 以真实短毛猫的眼窝、口鼻和四肢比例作对照，包括 [PetRescue 的 Chico 实拍](https://www.petrescue.com.au/listings/337442)。图片只作造型参考，没有复制成贴图。小红书搜索未取得可稳定访问的具体作品，因此不列作已采用来源。
- 找到此前双色猫预览突眼、假凹凸的一个确定原因：源 glTF 的 albedo 使用 UV0，normal 使用 UV2；URP/Lit 却把两者都当 UV0 采样。直接缩小眼部不能修复这个错误。恢复原始眼睛和网格，用单线程 Blender 将原法线的着色结果重投影到 1K UV0 切线空间，保留原 albedo、8,646 三角面及 40 骨骼。原始下载未改写，修改后的法线图约 772KiB；没有训练或新增高分辨率下载。
- 采用 kenchoo / guillaume bolis 的 CC BY4.0 双色猫作为正式 Cat 表示，`Assets/Resources/MagicMR/NaturalModels/BicolorCat.prefab`；原地待机与短暂低头嗅闻为本项目新编动作，原始约 8.7s 片段不作为当前待机。静态胡须绑定原头骨，避免头动而胡须停留。三视角、五帧待机和五帧低头/恢复实际渲染已通过，见 `.codex-tmp/bicolor-cat-review` 与 `natural-pet-final-review.log`。这是低面数自然比例模型，仍不是照片级毛发或真实动物运动捕捉。
- `NaturalPetRig` 的 Animator 只驱动表示内部；虚拟梦境跟随猫头骨，真实目标仍必须有独立、有效的头部定位。修复可靠标记为真但锚点为空仍能喂食的漏洞；跟踪丢失时清除旧的头部定位证明，同时保留梦境选择与好感。
- 新增独立绑定矩阵蒙皮检查：五个待机姿态经平移、37° 转向及 0.35m 名义高度拟合，最大底部偏差约 0.000313mm，跟踪根保持固定。猫/狗实际 Play Mode 通过食物/星星/爱心、合成头部门控、喂食与猫低头、撤销/戳破/奖励/停止/复位、跟踪丢失/恢复/退出与音源清理，保存十帧；头部测试锚点是明确的合成输入。
- 14 场景无缺失脚本/空材质；六场景模型检查通过。日志 `.codex-tmp/natural-pet-final-review.log`。Innerscene 猫仍不接受，Grab3D 候选在 Windows 和 WSL 保留证书校验的下载均失败，未导入。WSL 已停止，不与 Unity 重任务并行。
- Android 构建源资产清单新增猫 FBX、原始 albedo、重投影法线；继续排除 Innerscene 候选及错用的 UV2 原法线。新增 APK 内 `MagicMRAssetCredits.txt`，记录 CC BY 作者、来源、许可和修改。
- 猫版 Main/Pose/Repair 全部在单 Gradle worker、2GiB 堆、512MiB 元空间下构建成功；签名/CRC/ARM64、7/1/1 场景、14 项 SHA 绑定实际源资产及 APK 内署名均通过。日志 `.codex-tmp/android-{main,pose,repair}-natural-cat.log`；最终哈希见下表。
- 两份可播放猫预览位于 `.codex-tmp/pet-motion/{CatIdle,CatSniff}.mp4`，640×640、24fps、60帧/2.5s，分别60,437/65,382字节。离线显式蒙皮采样同时更新顶点、法线及切线，单线程编码；逐帧解码、首帧颜色与中帧变化验证通过。最终预览日志 `.codex-tmp/natural-pet-motion-frames-final.log`。它们不是头显录像，实际 Play Mode 动作证据单列。
- 狗仍是临时程序模型，猫尚无自然步态；房间效果与细节、完整手势和实机双眼/性能验收仍待完成，goal 保持执行中。

### 原创昆虫与足部动画（2026-11-15）

- ParkBugs 改用原创六足蟑螂与八足蜘蛛的骨骼模型；分别为 8,652/8,744 三角面、27/25 骨骼，每种只有一个主体 renderer/材质。保留扁平甲壳、翅脉、触角、足刺、跗节与爪，蜘蛛补充细毛和斑纹；没有夸大人脸或人类虹膜。模型是近似解剖重建，**不是扫描或照片级物种识别资产**，仍可继续打磨表面细节。
- `Tools/build_natural_insects.py` 单线程生成全部几何与顶点色，固定几何预算，不下载受限模型或训练。形态参考 [UF/IFAS 实拍图库](https://entnemdept.ufl.edu/projex/gallery/dl/cockroaches/text/american_cockroach.htm) 和 [Animal Diversity Web](https://animaldiversity.org/accounts/Periplaneta_americana/)，没有把原照片复制成贴图。生成源、FBX 和 provenance 在 `Assets/MagicMR/NaturalInsects`，本机该目录约 645KiB；正式 prefab 在 `Assets/Resources/MagicMR/NaturalModels`。
- 曾核查 Blendkit 的免费蟑螂步态候选，但它属于 Royalty Free 而非 CC0，官方匿名下载返回 403；未绕过认证、未下载或导入该模型，元数据仅留本机候选目录。
- `NaturalInsectRig` 缓存腿部链和姿态，使用两段式 IK、交替支撑/摆动及触角微动。独立骨骼绑定矩阵参考检查完整步态，蟑螂/蜘蛛最大脚踝误差约 0.041/0.065mm，地面最小偏差约 0.00003/0.057mm；连续 256 步均为 0 托管分配，平移/转向后的根锚点保持不变。手动确认底部/高度与独立蒙皮顶点的世界范围在 0.5mm 内一致。
- 修正受击缩放：网格与骨架一起缩放，结束后关闭 renderer；Undo 立即恢复分数、可见性及原尺寸。名义尺寸拟合改用绑定网格的包围盒，避免保守剔除箱影响摆放。初版检测曾把 `BakeMesh` 中已包含的 FBX 缩放再次应用而得到错误的悬空判断；尺度对照日志确认渲染器、原始网格与骨骼矩阵一致后，已修正检测方法，不将错误采样计作证据。
- `.codex-tmp/natural-insect-final-placement-timeline.log`：14 场景无缺失脚本/空材质，模型/步态/摆放回归通过；两种昆虫在真实 Play Mode 完成进入、步态、受击、重复奖励拒绝、立即 Undo、Reset、跟踪丢失/恢复、退出与音源清理，保存 14 帧。随后六场景 Play Mode 也通过并保存 18 帧。
- 动作取样明确包含 0.3s 施法飞行阶段，六场景统一在命中后的 0.36/0.55/0.95s 采样；以实际 LateUpdate 累计时间为准，不把离屏相机推进的编辑器时钟当演员执行时间。初版受击检查漏算飞行阶段，已修正。图片与采样记录在 `.codex-tmp/natural-insect-play`、`park-animation-timeline`；静态细节图在 `natural-insect-review`。
- 实物昆虫识别仍没有已验收权重，PICO 真实相机/双眼与手势、设备帧率尚未通过；上述检查只证明本地虚拟表示及其动作，不将近似模型或技术检查表述为科学还原或最终美术验收。
- 新增可直接播放的短预览：`.codex-tmp/insect-motion/Cockroach.mp4`、`Spider.mp4`，均为 640×640、24fps、48 帧/2s，约 53/82KiB。`NaturalInsectMotionFrames.Run` 用运行时相同 IK 与显式蒙皮快照离线采样，避免同一编辑器帧复用 GPU 蒙皮缓存；`Tools/encode_insect_motion.py` 用已安装 Blender 的内置编码器单线程编码，不另行下载工具。两份视频均解码为 48 帧，首帧与 PNG 的平均颜色误差约 1/255，首帧/中帧有实际变化。它们是离线步态预览，真实 Play Mode 生命周期证据另列，不是头显录像。
- 三个最终昆虫版 APK 均已通过签名、CRC、ARM64、7/1/1 场景及 SHA 绑定的 11 项源资产清单检查。主线/姿态日志为 `.codex-tmp/android-main-natural-insects.log`、`android-pose-natural-insects.log`；修复版首次以两 Gradle worker 打包发生 JVM OOM，保留失败日志，没有增大堆上限。Unity 退出后的单 worker 诊断打包通过，随后 Unity 内单 worker 整包通过；移除 Library 内单个可再生成输出后，全新打包也通过：`.codex-tmp/android-repair-natural-insects-fresh-package.log`。并发已默认改为 Gradle worker=1，堆仍 2048MiB、元空间512MiB，Unity/IL2CPP 并发仍为2。首次 OOM 的细分根因未完全隔离，不作永久内存安全保证。
- 结束时可用内存约 9.39GiB、C/D 盘剩余约 212.4/425.6GiB；没有训练、没有自动重启。ADB 仍无已连接设备，PICO 三个必需开关保持开启。

### 自然树木与季节/风动（2026-11-15）

- ParkTree 改为 Rico Cilliers 的 [Tree Small 02 / Poly Haven](https://polyhaven.com/a/tree_small_02)，CC0。原始 glTF 约 206 万三角面；受限候选下载共 101,219,226 字节，包含 1K 贴图与官方叶片遮罩，使用分块写入、固定总量、文件大小/MD5/SHA-256 校验和剩余磁盘检查，不下载 8K 贴图包。
- 运行时 LOD 为 74,999 三角面、三个主体 renderer、六骨骼（固定根骨 + 五个树冠风动区）。30,250 个原始叶片 UV 连通组拟合为卡片，保留原始分布并使用官方遮罩裁切轮廓。直接全树减面曾将叶片压为细点，该版已替换；转换同时清除孤立顶点，不将无效顶点送入蒙皮缓冲。
- 保留原始树皮、枝条、叶纹颜色与法线；枝条 UV 读取 Blender 导入节点的实际映射，不重复使用 glTF V 翻转前的偏移。叶材质支持自然、季节渐变、连通叶片组疏落，以及相同遮罩的深度/阴影 pass；保留显式选择的墨色/像素风格转换，默认仍为自然材质。
- 风动只改变树冠骨骼，树干和定位根节点固定；生长从模型底部缩放，Undo/Reset 立即恢复。`NaturalTreeChecks` 实际 BakeMesh 验证 121,000/121,000 个叶片顶点发生有限形变、无效风向恢复原姿、固定树干/锚点、生长底部及季节复位。此前仲秋断言采在渐变开端，已改用仲秋时间点，渐变行为保留。
- `Tools/prepare_natural_tree.py` 使用单线程 Blender 与单线程数值库。资源来源、原始文件 SHA-256 和修改说明保存在 `Assets/ThirdParty/NaturalTrees/SmallTree`；正式 prefab 在 `Assets/Resources/MagicMR/NaturalModels/SmallTree.prefab`。`.codex-tmp/natural-tree-review` 的三季图及 `.codex-tmp/park-natural-models` 的整合图来自 Unity 实际渲染，丢弃初始化帧并关闭异步着色器编译，避免首帧占位/未稳定蒙皮光照误判。
- 透明卡片的实际双眼表现、远距离闪烁、过绘制和 PICO 帧率仍需头显验证；桌面截图与几何预算不代表设备性能已经通过。
- 最终统一验证日志 `.codex-tmp/natural-tree-final-validation.log`：树木导入、14 场景、盆栽实际蒙皮、树木实际蒙皮/底部/季节/Undo、共享模型回归、六场景 Play Mode 生命周期全部通过。三季预览为实际模型材质采样，完整手势组合仍保留待验收。
- Android 资源验收新增真实 `BuildReport.packedAssets` 清单：核对树 FBX、季节 shader、原始叶片遮罩、盆栽/石雕 FBX 及三种房间材质八项源文件，排除未接受的宠物资源；清单绑定最终 APK SHA-256，不能只凭包内出现名字字符串判定模型已打包。
- 三个自然树版 APK 均已构建并通过签名、CRC、ARM64、7/1/1 场景及 SHA 绑定资源清单检查；最终日志 `.codex-tmp/android-main-natural-tree.log`、`android-pose-natural-tree.log`、`android-repair-natural-tree.log`，最终数据 `.codex-tmp/apk-validation.json` 与 `android-assets-*.json`。构建继续使用 2GiB JVM 堆、512MiB 元空间、两工作线程；没有训练模型。结束时可用内存约 9.42GiB、C/D 盘余量约 212.9/425.6GiB，均为当时读数。ADB 仍无设备。

### 真实盆栽与房间材质（2026-11-15）

- 正式 ParkPlant 改用 James Ray Cock 的 [Poly Haven Potted Plant 04](https://polyhaven.com/a/potted_plant_04)，CC0。原始 UV、形状和 1K 贴图保留，盆/土/石子合并为固定容器；40 片叶子各绑一根刚性骨骼，生长和微摆只作用于独立 Growth 节点。8,929 三角面、两个主体 renderer；六朵小型象牙白管状虚拟花取代原来的大粉色五瓣图形。下载总量 2,122,625 字节，不训练。
- 骨骼连接初版因 FBX 将蒙皮与骨架导入为同级对象而失败；已解包可编辑 prefab 并把两者一并放入 Growth，保持绑定空间。最终检查实际 BakeMesh 的 2,241/2,241 叶片顶点发生有限形变，容器世界矩阵、包围盒在生长/摆动时不变；不把初次失败算作通过。
- ParkRoom 加入 CC0 的 [Wood Table 001](https://polyhaven.com/a/wood_table_001)、[Rough Linen](https://polyhaven.com/a/rough_linen)、[Beige Wall 002](https://polyhaven.com/a/beige_wall_002) 原始颜色与法线。六张 1K JPEG 共 2,125,644 字节，用 URP Lit 分别表达木材、蓝灰色亚麻与灰泥；不把 ARM 的错误通道直接绑定到 URP。坐垫、靠背、扶手、书架和书本改用共享倒角网格。房间仍需更细致的空间与装饰表现，不记为最终美术完成。
- `Tools/fetch_polyhaven_model.py`、`Tools/fetch_room_textures.py` 仅从官方清单下载受限大小的 1K 资源，并验证大小/MD5、记录 SHA-256 与作者许可；`Tools/prepare_natural_plant.py` 用单线程 Blender 添加叶片骨骼。来源与修改记录保存在 `Assets/ThirdParty/NaturalPlants/Haworthia`、`Assets/ThirdParty/NaturalDecor/RoomTextures`。
- `.codex-tmp/natural-materials-scene-timeline.log`：14 场景缺失脚本/材质为 0，实际蒙皮、固定盆土、模型回归与六场景真实 Play Mode 时间序列通过。实际预览位于 `.codex-tmp/park-natural-models`，动画帧位于 `.codex-tmp/park-animation-timeline`；此处是 Unity 结果，未连接头显。
- 构建资源限制已补全：Unity 的 `AndroidExternalToolsSettings.maxJvmHeapSize` 在本次构建中暂设 2048，避免 Unity 的 `-Dorg.gradle.jvmargs` 覆盖生成文件；退出后恢复原值。`JDK_JAVA_OPTIONS` 对子 JVM 施加 512MiB 元空间上限，结束后恢复调用者环境。Gradle workers=2、parallel=false、daemon=false，IL2CPP jobs=2。先前只改 gradle.properties 并不足以保证 2GiB 堆，以最终 assemble 命令行日志为准。
- 该阶段盆栽/房间材质版三个 APK 均通过构建与检查，之后已由上述自然树版取代；当前产物与哈希以下方最新表格为准。两种未接受猫候选未打包。尚未安装或实机验收。

已查阅的造型/材质参考：
- Jessica Helgesson 的 [Greenhouse](https://katinkakick.artstation.com/projects/ybg5yK)：自然植物、陶土、木材与光影。
- Meghna Anil 的 [Office Greenhouse](https://megnacarta.artstation.com/projects/vbwrlA)：植物、雕像及空间关系。
- [写实行走猫参考](https://sketchfab.com/3d-models/realistic-animated-walking-cat-3d-model-92d8b9c4a33b4456ae6e066d85b9a04c)：正常身体比例与步态。
- [昆虫结构参考](https://www.artstation.com/marketplace/p/A7LNe/cockroach-insect-3d-model)：外壳、足部与触角。以上页面仅作参考，尚未购买、下载或采用其模型资产。

基础模型修订已接入：每个部件保留原始 albedo，取消默认硬黑描边和阶梯光照；树干与冠叶改为共享网格，宠物加入四足、较小头部和细小眼部，以及部件级呼吸、头部与尾部姿态。程序网格总缓存固定为八类（含家具倒角和管状花），不逐帧生成。植物已由上述真实资源替换。新增检查覆盖网格有限值、索引范围、颜色复位与 renderer 数量。

首轮叶尖浮点边界出现非有限顶点，已修复正弦端点的分数幂计算，并增加手动尺寸确认对非有限 bounds 的拒绝。`.codex-tmp/park-natural-models-2.log` 中完整回归和新网格检查通过，生成六张实际 Unity 模型图。

整体视觉审查仍为 **未完成**：Cat 已采用 UV 修复后的自然骨骼模型及新四拍原地步态，Dog 已采用自然猎狼犬和源步态/新嗅闻，两者均可在虚拟Pet界面选择并预览；房间已接入CC0椅子/花瓶、窗洞/分板地面/书页与PBR数据；虫类/树的虚拟变体入口、实机及整体验收仍未完成。树木、雕像和盆栽已接入上述 CC0 自然资源，树木卡片的设备表现待验收。静态渲染、Play Mode 时间序列、Android 构建与头显验收分别记录，不能互相替代。

### 连续网格与骨骼动画候选审查

为避免继续堆叠基础几何，已下载 [Innerscene CC0 猫模型](https://www.innerscene.com/tools/library/3d-parts/domestic-cat-walking-4e8ff6c2)（891,264 字节），保留来源与 CC0 说明。另一个 Grab3D 高面数候选经多种保留证书校验的方式均未能建立 TLS 连接，没有导入该来源的模型。

已用官方 SHA-256 校验的 Blender 4.5.3 LTS 便携版（D 盘，未安装系统服务）进行单线程离线转换。`Tools/prepare_natural_cat.py` 生成连续网格、合并 renderer、约 24,781 三角面、15,929 顶点、14 骨骼、原地待机/行走动画，以及原创程序化 1K 毛色贴图。原素材没有动画，不将新动作描述为实测动物运动。

候选位于 `Assets/ThirdParty/NaturalPets/Cat`，文件总量约 2.13 MiB；**没有加入 Resources 或替换 ParkPet 的默认模型**。`NaturalPetAssetBuilder.Run` 验证 URP 材质、骨骼与三角面上限，并通过 CPU 蒙皮采样和空白图检测输出六张实际 Unity 动画帧；日志为 `.codex-tmp/natural-cat-preview-bounds.log`，采样记录与图片在 `.codex-tmp/natural-cat-candidate/`。已修正快照重复应用 FBX 缩放及同帧蒙皮缓存问题，不能将此前空白/重复图当有效证据。

视觉筛选结果：**不作为正式宠物资源交付**。静止与运动采样中存在脚掌连接瑕疵，脸部和毛色仍偏简化，未达到用户要求的成熟自然视觉。保留候选与可复用转换/验证流程，后续需更好的原始模型或针对性的几何/拓扑修整。该候选的技术检查通过不等于视觉验收通过，也不等于 Park 场景交互或头显动画完成。

## 当前整合进度

- 已选择性导入 `4d` 的 75 个新增 Park 文件（约 0.78 MiB），包括六个场景、六类 prefab、独立运行时/编辑器组件、手型资源和许可证；没有直接覆盖快照同名文件。额外补齐 DiscoverySelection、RecognitionReplyPolicy 和 SharedInterfaceChecks 及其 meta。
- 已手工整合 PicoStereoCapture：保留快照 F2G 字段、右相机标定与两个采集时间戳，同时接入 Park 接收器和可选限量录制，默认最多保存 60 组；没有删除 poseClient 路径。
- 检测 DTO 增加多场景身份/路由字段，保留 pose_available / pose_model_ready；发送器默认 profile 0 继续原 v1 协议，Park profile 才选择 v2，并为界面提供最多四帧有界预览缓存的发送回调。
- 整合后的 `ParkRegressionChecks.Run` 已执行成功，退出码 0，日志 `.codex-tmp/park-regression.log` 包含 `STEREO_RECORDER_REGRESSION_PASS` 与 `PARK_REGRESSION_PASS`。这只验证模型/状态、门槛、锚点隔离和清理上限；不代表六个场景已完成视觉或实机验收。
- 主线共享入口已注册，新增配置入口可保留现有构建场景并启用六个 Park 场景；BridgeStereoBuild 会在重建主线时再次接入共享 UI，并将六个 Park 场景纳入主线 APK 场景列表，姿态/修复包仍独立。`.codex-tmp/park-shared-integration-2.log` 的完整共享回归通过，包括场景序列化、脚本 GUID、注册表、UI、手型与复位。补齐约 0.91 MiB 录制图片回放及 provenance 资源供现有检查使用。
- PC v2 路由已手工整合到当前服务，保留 v1 和主线 v6、较新的最新帧处理与 F2G 健康状态；51 项 PC 回归通过。真实 CPU 权重在同一连接内完成房间/盆栽/宠物、两次 discovery、三个缺失模型回复、切回 v1，并完成重连，共 11 次请求；证据 `.codex-tmp/position-smoke/replies.json`。测试使用黑图，不证明识别精度或头显注册正确。
- 多场景模型仅驻留主线加一个辅助，discovery 每帧只执行一个模型，复用共享 COCO 权重；替换权重/元数据必须重启服务，避免缓存身份混淆。树、虫、雕像配置为空；没有启用旧分支未通过的模型，也没有训练。房间/盆栽/宠物公开基础模型保留 PICO_PENDING 标记。
- 石雕改为 Rico Cilliers / Poly Haven 的 CC0 Marble Bust 01，保留原始网格和 PBR 图像，17,456 三角面、Unity 贴图上限 1K。来源、许可、源 GLB SHA-256 和转换方式存于 `Assets/ThirdParty/NaturalDecor/Statue`；实际运行 prefab 在 Resources，旧胶囊人形已替换。昆虫改为胸腹/翅壳/触角及三节足，区分六足昆虫和八足蜘蛛；房间改为木材、亚麻、抹灰墙的阅读角。后两项仍是基础几何，材质与细节仍待完善，不能称为写实成品。
- `.codex-tmp/park-natural-models-4.log` 完整 Park 回归、组件颜色复位、有限网格、renderer 上限均通过，并生成六张 Unity 预览。首张石雕预览发现背朝相机，已修正 prefab 的朝向；后续需要重新渲染正面与时间序列。
- Android 整合开发包首次成功构建，包含主线与六个 Park 场景。可选入口 `ResourceBoundedAndroidBuild.Run` 限制 IL2CPP jobs=2、Gradle heap=2048MiB、metaspace=512MiB、workers=2、禁止并行/常驻 daemon；结束恢复额外 IL2CPP 项目参数。后续桌面捕捉和录制修复仍需增量构建，首次包的 SHA 不代表新代码已经打入。
- 待处理：模型与动画时间序列验收、Android 构建结果、14 个场景逐项审查，以及必须头显的准确率、标定与性能验证。

## 分支审查基线

已查询所有远程分支并以过滤 blob 的浅拉取取得最新提交和完整目录；详细路径差异保存在本机 `.codex-tmp/branch-audit.json`。后续按需读取代码和资源，不下载全部历史数据集。

| 分支 | 固定提交 | 项目场景数（Assets/Scenes） | 处理方向 |
| --- | --- | ---: | --- |
| main | 8e248048231c164099c46f094f7d60a9f5967b6a | 6 | 对照原始定位、手势与构建行为 |
| codex/mr-development-snapshot-20261109 | 8359a0cb7c700a0d6f91b6804fae9cb6758c33b2 | 8 | 当前基线，保留较新的定位、F2G、内部结构和 VFX，以及本地通信修复 |
| 4d | 0e7616b47ab5421394ed6f31f8dab60a389f60f3 | 12 | 提取六个 Park 场景、模型、动画、共享交互及检查 |
| restore-4d-lighter | 831fcc512adb00cb98fb426efcdf726fcfab3757 | 12 | Assets 与 4d 的目录及内容比较无差异；另行审查训练/数据工具，不重复整合场景资源 |
| pico-hci-yolo-vfx | eae26868f857a7607a9fa2973fe9bc88c0260740 | 5 | 审查旧特效、模型及第三方资源，保留许可证，避免引入旧代码回退 |

样例场景另外列入兼容性检查：XR Hands HandVisualizer、XR Interaction Toolkit HandsDemoScene/DemoScene；pico-hci-yolo-vfx 还包含 Devtoid 金银铜币资源展示场景。这些属于 SDK/第三方演示，先检查资源和许可，再决定复用位置，不将其误计为新建原创场景。

### 分支内容采用决策

- `main`：与快照逐项比较运行时/编辑器差异；快照的 F2G、打火机外观、内部结构、花朵及 VFX 更新保留，没有以旧 main 覆盖。原始 AR/桌面/透视场景均保留，纳入 14 场景序列化审查。
- `4d`：采用六类 Park 场景、共享手势与目标选择、限量双目录制、回放与几何/生命周期检查；同名双目采集、协议 DTO、发送器采用手工整合。原始几何继续逐项替换，不能因导入完成而宣称视觉完成。
- `restore-4d-lighter`：Assets 与 4d 完全相同；采用较新的依赖独立协议、discovery、模型哈希/验证策略及其回归。保留当前 v6 部署权重，不采用 restore 中较旧的 lighter 默认路径；归档训练/数据结果作为研究记录，未通过验证的权重不启用、不重新训练。
- `pico-hci-yolo-vfx`：已读取独有 `HciMeetingDirector`、`HciMeetingStore`、`HciMeetingScenario`、`HciMeetingVfx`、`MagicMRAnim`，并审查同名代码差异与 WarFX 说明。其八个会议用例与现有主线 + Park 六类重叠（打人柳/四季树共用 Tree），旧实现直接隐藏 lighter 锚点、维护第二套手势消费与动画协程；不与当前追踪根/共享界面并行挂载。现有 Park 的池化反馈、主线较新 VFX 及独立动画根覆盖相应用例。通用 ease 函数与现有 Mathf 插值重复；旧文件命令自动进入 Play 的编辑器入口由显式批处理验证入口替代。
- 旧 HCI 的 `HciStore` 是第三方 prefab/材质辅助层，使用 WarFX、Devtoid coins、boxes 的旧版资源。WarFX 说明标注 2019 v1.8，不能把“free”当作 CC0；本轮未整包导入这些资源或旧展示场景，没有新增其运行时依赖。使用本项目反馈网格与明确 CC0 的石雕，保留旧分支作来源记录。SDK 样例继续由固定包版本管理。

### 本机验收及接续（2026-11-14–15）

- `ProjectSceneAudit.Run` 审查完整 14 场景：缺失脚本 0、未赋材质槽 0；报告 `.codex-tmp/scene-audit/scenes.json`。这只是序列化依赖证据，不替代所有旧场景的运行时和实机检查。
- `ProjectSceneAudit.RunWithTimeline` 在修正石雕 FBX 轴向/朝向后，完成六场景实际渲染及隔离 Play Mode 采样：18 张时间序列图、进入/动作/锚点隔离/复位/追踪丢失与恢复/退出和 AudioSource 清理通过。日志 `.codex-tmp/project-scene-audit-timeline.log`，图片/结果 `.codex-tmp/park-animation-timeline/`。它验证运行时动画与清理，不代表头显交互、识别精度或宠物视觉完成。
- 通过 `Tools/build_android_bounded.ps1` 的完整 Unity Android 构建：`Builds/Android/BridgeStereoFusion.apk`，63,014,676 字节，SHA-256 `fbc2588f6891e9fa87b46d88618aa17160ee55b7af972640594962bead39a382`；包名 `com.yn.picmagicmr.stereo`，版本 1.10.0 / 22，ARM64，minSdk 29 / targetSdk 35。APK CRC 检查通过，包含 level0–6 七个场景。构建日志 `.codex-tmp/android-bounded-build.log`。
- 首次构建在 Gradle 的 Java NIO 回环 socket 初始化失败；单独探针确认 D 盘 socket 临时目录可用。仅向本次构建进程及子 JVM 传入 `JDK_JAVA_OPTIONS=-Djdk.net.unixdomain.tmpdir=D:/Unity/JavaSockets` 后解决；没有修改防火墙或系统安全设置，脚本结束恢复进程环境。没有重复下载 Unity 或重做首次全部编译。
- ADB 当前没有已连接设备；已通知用户可以连接头显并开启 USB 调试，尚未安装 APK 或声明实机通过。
- WSL 现在已实际启动，内核 `6.18.58.1-microsoft-standard-WSL2`，内存约 7,944 MiB、swap 2,048 MiB，识别 RTX4050 Laptop 6,141 MiB。此前待重启结论已由本次探针更新；本代理没有执行重启。F2G 改用官方测试 Python3.10/PyTorch2.4/CUDA12.4，正在独立 D 盘环境安装，尚未有真实 F2G 推理通过结论。

2026-11-15 更新：

- Linux F2G 安装、pointnet2_ops/chamfer 编译、官方 15 项测试、三份权重校验及 `pip check` 均通过。真实 YOLO/SAM/F2G 官方样例联合预热最后一轮约 1,388.6/477.5ms、peak allocated 4,381MiB。Windows 生产 PoseAvailability 与 WSL 的 5006/5007 端口交互通过，身份校验匹配当前 v6 检测器。复现入口 `Tools/smoke_f2g_windows.py`；结果 `.codex-tmp/f2g-cross-windows.json`。这只证明环境、样例和跨系统服务，不证明 PICO 实物姿态或端到端延迟。
- 当前为青铜兽模型/动画/虚拟切换版四包；签名/CRC/ARM64、精确场景路径、40项资源清单与APK SHA绑定检查通过，完整记录 `.codex-tmp/apk-validation.json`；以下值替代历史构建的旧SHA。

| 产物 | 字节 | 场景数 | SHA-256 |
| --- | ---: | ---: | --- |
| BridgeStereoFusion.apk | 84,962,626 | 7 | f686b145bbcac4dd67908a72e16c3cb89a04a1d27389413fd8ff92c9acc488a3 |
| BridgeStereoPoseFusion.apk | 84,910,036 | 1 | f2f5810221d6e054cfecd9479d90855a25a2531ef722dfb1a07bb6e175f7e979 |
| BridgeStereoRepair.apk | 84,909,868 | 1 | 7016eb8117ce9e88625e44e8292310cb8ef15485f2a937d84a3cb2d7e9322559 |
| AllScenesValidation.apk（仅编译验证） | 75,348,000 | 14 | 68e340855f9558c853ed40062846f2102bfdc6e276986b4442db0ff816d46a03 |

- 普通 Build/Build And Run 的首个启用场景修正为共享主线，Park 六场景各启用一次；新增配置检查通过。花朵重建复用原部件，连续两次生成的六个部件引用 ID 均保持稳定，避免构建时反复重建子对象。
- 桌面捕捉新增 Reset 按钮/R 键，修复中断、复用和暂停时间；相机按目标尺寸构图并仅按需渲染。真实场景的按钮、完整构图、卡片截图、取消/重用/目标切换及主线/Park/F2G 回归通过：`.codex-tmp/integrated-capture-validation.log`。
- 录制仍默认关闭，已接通主线精确已发送采集的记录回调；硬上限 60 组，即使配置为 int.MaxValue 也不能突破。测试通过，最大原始图像写入约 105.5MiB/会话，不训练。
- 早期审查 [kenchoo 的 Bicolor Cat](https://sketchfab.com/3d-models/bicolor-cat-e623a618ca344a8393d7ba4d63ec23cf) 时，原始动画技术检查通过，但缩眼修整未改善视觉，当时未打包。该历史版本现已被顶部记录的 UV0 法线修复与新动作版本取代；原始来源、固定镜像和 CC BY4.0 许可保留。
- 当前未连接头显，尚未安装本次APK；本地模型/动画与编译证据见顶部。真实定位、手势、PICO帧龄/姿态精度、双眼与性能仍待设备验收，缺失自定义识别权重仍明确未配置；goal保持执行。

## 整合原则和阶段

1. 对所有分支的模型、动画、prefab、场景、交互入口逐项审查。当前目录审查已完成，内容与视觉审查正在进行。
2. 以快照和本地修复为底，选择性移植 Park 新增内容。手势规则、双目采集、识别通信等同名代码逐项对比后整合；不得直接用旧分支覆盖较新的 F2G、模型内部结构和特效。
3. 统一各场景模型层级、材质、尺寸、底部锚点与朝向，补全进入、待机、交互、退出、取消和复位动画；避免动画驱动跟踪根节点。明确真实识别目标与编辑器虚拟目标的边界。
4. 按用户最新方向采用成熟、自然比例与真实材质的网络参考及可追溯模型；AI图片只作为可选参考，不能替代Unity实际网格、材质、动画和渲染验收。
5. 每个场景以编译、回归、实际 Unity 渲染、取消/复位清理和资源数量上限验收；随后执行 Android 构建，再通知用户连接头显做实际交互和性能检查。

## 项目场景验收表

以下 14 个项目场景来自上述分支并集。每项均需检查模型、动画与交互接入，不能仅以存在 .unity 文件判定完成。

| 场景 | 验收内容 | 当前状态 |
| --- | --- | --- |
| ParkTree | 树模型、季节/生长、树体生命化、风动、奖励与复位；区分真实树和幻想效果 | CC0自然树/1K原图、五区风动、底部固定生长/季节与Undo通过；自然/幻想虚拟按钮、动作中断保留拟合、表面/历史清理、表面贴合树皮标记/安抚/Undo/Reset通过；真实触摸/手势/双眼与性能待设备验收 |
| ParkBugs | 虫体与行为、风/交互反馈、虚拟奖励、退出和清理 | 原创六足/八足骨骼、两段IK、脚部落地、受击/Undo/Reset/跟踪/退出通过；Cockroach/Spider/Test defeat实际虚拟按钮与忙碌/实时/回放门控通过；识别权重未配置，实机待验收 |
| ParkStatue | 雕像结构、苏醒与扫描、故事/种子表现、动作中断与复位 | CC0 真实比例石雕已接入，朝向/轴向已修正；苏醒时间序列、清理及 Android 首包通过；全手势/实机待验收 |
| ParkRoom | 房间与装饰模型、环境效果、位置尺度确认、退出清理 | CC0椅子/花瓶及窗洞/分板地面/书页/PBR阅读角24,204面、9主体renderer；共享网格跨场景恢复、固定家具、水母与持续糖果雨/Undo/Stop/Reset/跟踪/退出本地通过；真实空间/手势/双眼透明表现和性能待验收 |
| ParkPlant | 植株层级、生长/开合花、形态变化、稳定锚定与复位 | CC0 十二卷原始贴图与 40 叶骨骼接入；实际蒙皮、固定盆土、开花时间序列与清理通过；完整手势和实机待验收 |
| ParkPet | 宠物模型、待机/互动/喂食、气泡与幻想表现、宠物定位边界 | 自然 Cat 和 Wolfhound 均已接入，真实待机/嗅闻及两种宠物完整梦境/喂食/Undo/Reset/跟踪生命周期本地通过；猫新四拍原地步态和犬源步态、虚拟Cat/Dog/Walk按钮及启停/喂食打断本地通过；真实头部定位与实机仍待完成 |
| BridgeStereoFusion | 打火机、碳化、生命化、躲闪、变花、放大和内部结构，手势与锚定一致 | 实际入口启动、原目标核心动作/内部结构/变花和取消复位（7帧）及共享界面回归通过；模型审查隔离定位输入，真实手势/锚定与设备性能待验收 |
| BridgeStereoPoseFusion | 主线模型/动画 + F2G 仅控制旋转，动画与定位互不争夺 | 实际启动/原模型核心动作7帧、几何/真实Linux联合样例/跨系统端口/独立APK通过；模型检查隔离输入，头显旋转待验收 |
| BridgeStereoRepair | 保留独立定位修复/诊断行为，模型与动画一致，不引入姿态连接 | 实际启动/原模型核心动作7帧和独立APK通过，保持无F2G组件；设备定位诊断待验收 |
| BridgeTest | 源场景可再生成主线与实验场景，模型材质和手势配置完整 | 详细模型/旧默认IP已更新；实际启动和原目标核心动作/复位7帧通过，仍是主线生成来源；真实输入/定位待验收 |
| MagicMR | 早期 MR 场景模型、手势和生命周期兼容 | 详细模型、实际Study系统启动及原目标核心动作/取消复位7帧通过；模型检查隔离空间输入，校准/真实手势/锚点待验收 |
| VstTest | 透视基础场景可运行，测试物体与显示切换正确 | 实际挂接受保护重试入口，启动/有效相机/透明背景/关闭后处理与Volume通过；真实透视/恢复与双眼必须设备验收 |
| SampleScene | AR 图像/卡牌模型及捕捉反馈完整，保留平台依赖边界 | 实际本地卡牌纹理/飞行/暂停时间/复位通过；修复共享Canvas/背包被一起动画，专用相机按需渲染；AR provider及图像识别待设备验收 |
| DesktopCaptureDemo | 点击、卡牌转换、飞入背包与复位动画可桌面演示 | 实际截图/重复拒绝/复位复用/暂停时间飞行及背包不动通过；原有按钮/目标切换检查通过，无后台请求功能 |

## 已验证的环境基线

Unity 6000.0.26f1、Android SDK/NDK、OpenJDK 17.0.9+9 已安装到 D 盘；PICO SDK 3.4.0 固定提交已安装。编辑器首次导入、桌面及 Android 目标下 StereoIntegrationChecks、F2GPoseChecks、MagicMRVfxPreview 和生产 C# PoseTransport 故障检查均通过。特效检查生成 7 张实际渲染预览。以上发生在整合 Park 分支之前，不能作为整合后通过的证据。

WSL3.0.5、D 盘 Ubuntu24.04 与真实 Linux F2G 已安装并验证，详情见上方最新验收。WSL 上限 8GB 内存、4核、2GB交换；编译并发1、禁用 pip 缓存，Windows 物理 D 盘也必须至少25GiB可用，不能只依赖 WSL 虚拟文件系统的 df 数字。没有执行模型训练或自动重启。

## 完成标准

- 全部分支的有效成果有明确采用、替换或保留说明；无缺失脚本、GUID 冲突或材质丢失。
- 14 个项目场景分别有模型、动画、交互、生命周期与渲染证据；SDK 和第三方演示有兼容性处理记录。
- PC 回归、Unity 编译与检查、Android 构建通过；产物、命令和日志路径可复现。
- 图像参考与 Unity 实现之间有明确对应，不把静态概念图当动画或交互。
- 真实定位、手势、旋转、双眼表现和性能需头显验收；任何缺失项保持未完成状态。
- YOLO训练、独立验证和通过后接入已列为必做，按本文件顶部资源约束执行；推理服务不得隐式训练。其余模型训练不在本次新增范围内；不自动重启，不丢弃既有修改。

