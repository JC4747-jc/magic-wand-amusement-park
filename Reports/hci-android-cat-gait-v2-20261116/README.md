# 魔法乐园：猫步态接地转场 Android 验证包

本包加入猫原地步态 v2 的接地速度衔接修正，保留打人柳冠层、同一 Tree 体验选择、植物语音、火机扑翼/咳嗽和已接受的宠物/石像/房间效果。原模型、毛色、骨架及火机共用 A/B/C/D 手势不变。

1652 个 Assets/Packages/ProjectSettings 文件在构建前后与 runtimeSourceCommit 一致。Unity Android IL2CPP/Gradle、14 场景、签名、ZIP CRC 和 ARM64 检查通过；BuildReport 来源清单与新 APK SHA256 绑定。核对既有 40 项自然资源，并检查 15 项效果/宠物相关资源，包含猫 FBX、Animator Controller 和 Resources 预制体，包内存在 BicolorCat_Walk 动画名。

另对旧打人柳包与新包进行有界 ZIP/序列化文件对比。包含 BicolorCat_Walk 的同名资源文件为 1381092 字节，37856 个变化字节全部落在位置不变的 Walk 名称与后续 Idle 名称之间，其余该文件字节一致。这是新动画数据进入包的补充证据，不是完整解析运行时曲线或 PICO 播放测量。具体偏移与摘要见 packed-cat-animation-delta.json。

编辑器的 193 个导入蒙皮姿态、99 个实际 Animator 接地样本、启停/撤销/复位、猫狗切换、描边/梦境和共用手势回归见 [步态报告](../hci-cat-gait-v2-20261116/README.md)。数学轨迹平滑不能单独证明导入后的自然度；嗅闻仍未被当作完整进食或睡眠。

本轮原生编译缓存继续复用，重任务串行，并发 1；启动至少 6 GiB 可用内存，运行下限 3 GiB、项目盘 20 GiB、单任务增长 1 GiB、日志 16 MiB、20 分钟守卫生效。任务 PlayerDataCache 移入备份后重新封包，未删除用户资产、模型或原生缓存。遥测是五秒采样，不是连续峰值；没有训练、下载模型或重启，主工作区 Git 暂存区保持不变。

本地文件为 Builds/Android/HciCatGaitV2Validation-20261116.apk，包标识 com.yn.picmagicmr.validation；它是独立 14 场景编译验证包，正式导航和设备运行仍需验收。PICO 未连接，真实 YOLO、手部、双眼/遮挡、听感、帧率和内存尚无设备结果；场景照明、毛色/描边和其他美术仍需完善。全部八用例 32 效果、训练/适用大模型接入 Goal 保持 active。上传后附服务器摘要回执。

下载：[猫步态 Android 验证包](https://github.com/JC4747-jc/magic-wand-amusement-park/releases/tag/hci-cat-gait-v2-validation-20261116)。服务器摘要与本地 SHA256 一致，见 [上传回执](release-receipt.json)。这是未完成 PICO 验收的预览版本。
