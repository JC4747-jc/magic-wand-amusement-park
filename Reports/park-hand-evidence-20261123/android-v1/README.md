# Android 候选包 · Park 手势诊断

这个 Android 预发布包基于 `mr/jc@cccc817e9e8d3cd4299d7c9a4e06139efaebac27`。1,854 项输入已核对到发布源码，15 个场景、16 个 ARM64 库、签名、ZIP CRC 和对齐均已通过静态检查。包名和签名证书与上版候选一致。APK SHA256 为 `6566d229bd296ad1346d623fa347910c6cefe7d9291a01a729483b2b1bd4c049`。[下载 APK](https://github.com/JC4747-jc/magic-wand-amusement-park/releases/download/mr-jc-park-hand-evidence-android-20261123/ParkHandEvidenceCandidate-20261123.apk) · [预发布回执](release-receipt.json) · [封包输入](input.json)

六个 Park 保存场景的诊断默认关闭。编入了手势样本和最近已保存 PICF 回包的关联入口。录制需同时在设备应用数据目录启用 `record-stereo.enabled` 和 `record-hands.enabled`。出租屋保持摄像头实景透视，已核对雪山天鹅湖纹理、湖景着色器、实景透视着色器和水母灯着色器。宠物头位仍保留上版的默认关闭配置。[包内验收](acceptance.json) · [诊断字段](../README.md)

本包的设备启动、双眼透视、YOLO 到实手势触发、实际落盘和帧率均待 PICO 验收。构建采用单并发，最低采样可用 RAM 3.92 GiB，候选盘最大采样净增 618 MiB，保护未触发。1,840 项隔离源码和主索引均已恢复。[资源记录](pack.log.resources.json) · [恢复回执](restoration.json)
