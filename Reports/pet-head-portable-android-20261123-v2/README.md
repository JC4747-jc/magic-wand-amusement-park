# Android 候选包：宠物头位接入

APK 对应 `mr/jc` 的 `9b557b3e22f5e213e41754ede9cdc32d9faed9e0`。已将 1,850 项封包输入逐项核对到 Git 对象，排除 12 项 Editor 回放夹具，并将上版 APK 中的一处 Editor 检查脚本同步为已发布版本。默认保存场景仍关闭头位功能。先单工作进程预热 IL2CPP，再恢复封包所用的原 ProjectSettings。隔离工程和主 Git 索引均已复核恢复。[输入清单](input.json) · [恢复记录](restoration.json)

- 15 场景、16 个 ARM64 库、签名、ZIP CRC 和对齐通过。IL2CPP 元数据含头位候选、默认端口字段及接触门控。
- 出租屋保存场景使用实景模式。包内核对湖景纹理、湖景着色器、实景透视着色器和水母灯着色器。仍未包含微缩墙面切换入口。
- PICO 真机启动、双眼透视、真实手势和现场接触待验。[验收记录](acceptance.json)

构建时最低采样可用 RAM 4.14 GiB，候选盘最大采样净增 618 MiB。各阶段资源保护均未触发。[预热采样](prewarm.log.resources.json) · [封包采样](pack.log.resources.json)

[下载 APK](https://github.com/JC4747-jc/magic-wand-amusement-park/releases/download/mr-jc-pet-head-android-20261123/PetHeadPortableCandidate-20261123.apk) · [预发布与哈希](https://github.com/JC4747-jc/magic-wand-amusement-park/releases/tag/mr-jc-pet-head-android-20261123)
