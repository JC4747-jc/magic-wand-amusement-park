# HCI 统一手势 Android 构建验证

已为统一 YOLO→定位/选择→火机共用手势→对象魔法的源码构建新的全场景 Android 验证包。源码 `c901a0c8aa21c63f8269b255b415baff1018dc50` 与发布提交 `36901e9806c4aa3c2b3bc3cc1e7165a57c75c7e0` 的 Assets/Packages/ProjectSettings 完全一致；1581 文件清单复核无差异。编辑器合成轨迹、条件矩阵和启动证据见上一份统一线路报告。

## 已完成

- 完整 Unity IL2CPP/原生编译/Gradle 打包成功。
- APK 签名和 ZIP CRC 通过，ABI 为 ARM64，包内 14 个场景名称逐一一致。
- BuildReport 与当前 APK SHA256 绑定，40 项必须模型、贴图和材质来源存在；许可署名与猫/狗步态存在，排除未接受资源。
- 包内 IL2CPP 元数据包含 TabletopHandSample、TabletopGestureRules、GestureReachWindow；仅证明类型进入包，不代表真机行为验收。
- [下载验证包](https://github.com/JC4747-jc/magic-wand-amusement-park/releases/download/hci-unified-validation-20261116/HciUnifiedAllScenesValidation-20261116.apk)；[GitHub 预发布](https://github.com/JC4747-jc/magic-wand-amusement-park/releases/tag/hci-unified-validation-20261116)。包名 `com.yn.picmagicmr.validation`，不覆盖生产应用。SHA256：`2aa77e45eee33b32ac417fc899ca48dc65d74ec47211676b865ef9d4bd4b50fe`。

## 内存恢复经过

旧 DAG 在临时目录变化后失效，首个离线调用退出 4，未执行转换，保留其失败记录。让 Unity 重新准备当前 DAG，在 IL2CPP 启动时只终止此次所属进程树，再独立执行不依赖 Editor RPC 的 4 节点转换目标。转换确实完成后返回 DAG 刷新信号；该信号本身不代表 APK 完成。随后备份并恢复本次构建暂存的项目设置/XR 文件，核对源码清单，再交回 Unity 正常打包。

转换最低采样空闲内存 3.827 GiB、最大所属 RSS 4017.77 MiB、磁盘增长 597.45 MiB。最终重打包阶段最低空闲内存 4.279 GiB、最大所属 RSS 3618.0 MiB、磁盘增长 61.0 MiB；该阶段复用原生缓存，未启动编译进程。此前原生编译阶段最多一个编译进程，最低空闲内存 4.891 GiB、最大所属 RSS 3480 MiB。以上为五秒采样，不能声称捕获连续峰值。

首次原生编译与 Gradle 已完成，但增量 BuildReport 没有源资产信息，校验器正确拒绝其包。只将约 60.48 MiB 的 PlayerDataCache 移出并保留备份，再序列化场景内容；未清理原生编译缓存。最终包须重新通过绑定当前 SHA256 的 40 项来源检查。该中间失败的日志哈希、资源与缓存移动清单也已归档。

没有降低 3 GiB 空闲内存下限，没有删除 Library，没有并行训练、重新下载模型或重启电脑。过程脚本归档为 `build-driver-used.txt`、`offline-driver-used.txt`，供审计本次命令；准备阶段会中断 Unity 并留下需恢复的临时项目文件，因此这些归档不是无需检查的一键发布流程，也未替换仓库默认构建脚本。

## 仍待验收

尚未在 PICO 运行本包。相机、真实 YOLO 检测、双目位置、真实关节输入、双眼画面、帧率与 32 项 HCI 效果须分别实测。无新合格 YOLO 权重，不将虚拟测试或打包成功计为实物链路完成。可连接 PICO 进行下一阶段；完整 Goal 保持进行中。

详细身份、各阶段资源、包内来源和 GitHub 上传回执见本目录 JSON。此前内存中止与旧 APK 的历史报告保持原样，不能用于冒充本次新包证据。
