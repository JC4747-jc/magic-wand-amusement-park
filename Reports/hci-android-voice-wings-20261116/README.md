# 魔法乐园：语音与扑翼 Android 验证包

本次将植物 B 离线语音/嘴部包络，以及火机 B 扑翼/定时烟团/合成咳嗽音效编入 Android。保留宠物描边、石像卡通/伸展/荷花许愿树、房间四维映射等已接受增量。所有线路继续按 YOLO 实物目标 → 定位/选中 → 共用火机 A/B/C/D 手势 → 对象魔法执行；检测服务和权重不因打包而自动获得新类别。

## 通过范围

1635 个 Assets/Packages/ProjectSettings 文件与 runtimeSourceCommit 核对一致。Unity 6000.0.33f1 的 Android IL2CPP/Gradle 构建、14 场景、ARM64、签名、ZIP CRC 及既有 40 项自然资源来源检查通过。另核对四个效果 shader、全身石像 prefab/FBX/材质和 PlantDialogue.wav，共八项资源；APK 元数据含 LighterAgencyVisual、共用手势类型及植物语音方法。资源清单与本次 APK SHA256 绑定，不能使用旧清单替代。

火机翅膀网格及咳嗽声由运行时代码生成，不是额外打包的 FBX/WAV；包内类型存在仅证明编译/引用范围，不证明设备播放效果。植物语音为现有 113 KB 英文 PCM 资源。对应的播放/中断、共用手势及实际模型渲染证据见此前 hci-plant-voice-20261116 和 hci-lighter-agency-20261116 报告。

## 资源控制

先由 Unity 准备 DAG，随后关闭 Unity 进行离线 IL2CPP，最后完整打包；重任务串行、单个原生编译进程。RAM 可用下限 3 GiB、项目盘余量 20 GiB、每步磁盘增长 1 GiB、日志 16 MiB、每步 20 分钟守卫继续生效。保留 Library 和原生编译缓存；仅将约 62 MiB PlayerDataCache 移入本地备份，重新生成打包来源清单。中断准备产生的 XR/ProjectSettings 临时变化按白名单备份并恢复，完整构建前后重新核对源码。

遥测是五秒采样，不是连续峰值；没有训练、下载模型或重启。详细数字见 manifest.json。主工作区 Git 暂存区未改变。

## 未验收与后续

这是 com.yn.picmagicmr.validation 的 14 场景编译验证包，应用内正式导航范围仍需单独核对。PICO 实时相机/YOLO、真手势、双眼/遮挡、声音听感与性能尚未验收，也没有新增合格 YOLO 权重。植物声音、翅膀膜面等美术仍需精修，全部八用例 32 效果和模型训练/接入 Goal 继续执行。打包成功不代表这些工作完成。

下载：[语音与扑翼 Android 验证包](https://github.com/JC4747-jc/magic-wand-amusement-park/releases/tag/hci-voice-wings-validation-20261116)。上传回执见 [release-receipt.json](release-receipt.json)，GitHub 摘要与本地一致；发布为预览版本，未完成 PICO 真机验收。
