# 魔法乐园：打人柳与体验选择 Android 验证包

本次将独立的垂枝/窄柳叶冠层，以及同一可靠 Tree 目标的 Four seasons / Whomping willow 共享菜单选择入口编入 Android。保留植物语音、火机扑翼/咳嗽、宠物描边、石像和房间的既有增量。仍按 YOLO 实物识别 → 定位/选中 → 火机共用 A 捏合松开、B 指尖画圈、C 张掌横挥、D 握拳张开 → 对象魔法执行。

## 已验证范围

完整 Assets/Packages/ProjectSettings 的 1650 个文件在准备前、准备恢复后、完整构建前和构建后与 runtimeSourceCommit 一致。Unity 6000.0.33f1 的 Android IL2CPP/Gradle 构建、14 场景、ARM64、签名和 ZIP CRC 检查通过。BuildReport 的实际打包资源清单与 APK SHA256 绑定，含既有 40 项自然资源，以及本次核对的 12 项增量资源：此前 8 项效果/石像/语音，加 WillowCanopy 配置、柳枝网格、柳叶网格和柳叶材质。

元数据还确认 WillowCanopyData、共享界面以及 SetTreeExperience / ApplyTreeCanopyVariant 等代码存在；类型与方法存在证明编译和引用范围，不证明设备行为。原火机/植物等元数据检查继续保留。编辑器模型、生产合成手势、两种 Tree 体验、撤销/复位和实际渲染见 [冠层报告](../hci-willow-canopy-20261116/README.md)；实体目标代码分支仍明确使用合成 Observe 输入。

## 资源控制

先由 Unity 准备 DAG，再关闭 Unity 单独完成/核对 IL2CPP 代码生成，最后完整打包；各重任务串行，原生编译并发 1。启动至少 6 GiB 可用内存，运行下限 3 GiB，项目盘最低 20 GiB，单步骤新增空间 1 GiB、日志 16 MiB、20 分钟守卫生效。保留 Bee/原生缓存，约 62 MiB 的任务 PlayerDataCache 移至本地备份，生成新的打包来源清单。准备中断后的 XR/ProjectSettings 临时变化经白名单备份恢复，未改变主工作区源文件或暂存区。

首轮完整增量构建虽产生 APK，但 BuildReport 没有 packedAssets，按交付检查拒绝且未上传。将代码生成之后的新任务 PlayerDataCache 再次移入备份后重新封包，得到本次 APK 对应的完整来源清单。失败日志/遥测保留，未放宽来源验证。资源数字为五秒采样，不是连续峰值；详细记录见 manifest。没有启动模型训练、下载或重启。

## 交付边界

本地文件为 Builds/Android/HciWillowValidation-20261116.apk，应用标识 com.yn.picmagicmr.validation。这是独立的 14 场景编译验证包，应用导航和头显播放仍需验收。此前报告的“未进入 APK”保留为当时状态，本包包含树木选择和新冠层。

PICO 未连接，未进行实物 YOLO、真实手势、双眼、遮挡、音效听感或帧率/内存设备验收。没有新增合格 YOLO 权重；树干裸枝、暗色冠层与其他美术仍需完善，八用例 32 效果和模型训练/适用大模型接入 Goal 保持 active。GitHub 上传回执将在实际上传并校验后附上。

下载：[打人柳 Android 验证包](https://github.com/JC4747-jc/magic-wand-amusement-park/releases/tag/hci-willow-validation-20261116)。服务器摘要与本地 SHA256 一致，见 [上传回执](release-receipt.json)。这是未完成 PICO 验收的预览版本。
