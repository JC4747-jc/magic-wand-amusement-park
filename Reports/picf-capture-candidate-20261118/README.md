# 有界 PICF 数据采集入口候选

2026-11-18；开发基线 `8359a0cb7c700a0d6f91b6804fae9cb6758c33b2`，工作分支 `jc/mr-optimization-20261117`。本轮新增独立采集工具，没有修改火机、Unity 场景、手势、生产识别入口或模型配置。

## 已实际检查的范围

- `Tools/capture_picf_dataset.py` 直接复用 `object-detection-demo/scene_protocol.py:read_packet`，支持 PICF v1/v2。使用单个整包截止时间，在读取 JPEG 负载前限制长度、像素、长边、场景及总线缆字节；验证实际 JPEG 格式/尺寸/方向，原样保存字节，记录 SHA256 与 RGB 像素 SHA256。
- `tests-v1.txt`：21 项检查通过。包含真实分段 socket 收发、整包持续小流量超时、超大负载/尺寸在读取前拦截、场景错配、截断 JPEG、重复帧、采样间隔、存储限额、磁盘底线、既有目录保护、会话分组、来源不自动批准、v1/v2 回环及递减 frame_id 拒绝。
- `Tools/run_picf_capture_bounded.ps1 -SelfTest` 实际在 Windows Job 中运行独立工作进程：3 个合成协议包、2 张原样 JPEG、1 个重复 JPEG 未重复存储，存储 1,354 字节；回复始终为空 detections 并带 `capture_only_no_inference`。`run-resources.json` 退出 0、无 guard stop、0.852 秒；仅 1 次存活进程采样，不能作为连续峰值或主动触线终止验收。没有加载 torch/ultralytics/numpy。
- 自测数据为明确标记的 `editor_synthetic`，位于 `.codex-tmp/device-capture/picf-loopback-20261118`。真实接收监听已随工作进程退出，当前没有采集会话或后台采集端口在等待设备。

## 正常采集限额与来源边界

默认最多 24 图、每图 2 MiB、长边 2048、4 MP、图片累计 32 MiB、收到的线缆数据累计 64 MiB、300 次包读取尝试、4 次连接、120 秒会话、3 秒整包截止、30 秒空闲、至少 1 秒存图间隔。精确 JPEG SHA 去重；所有帧沿用同一个 capture_family，即使重连也不能拆成不同训练/测试来源。没有标签目录、空负例或自动类别批准。

宿主启动前 RAM≥4 GiB、C 余量≥20 GiB，运行 RAM 底线 3 GiB、所拥有进程树 RSS≤512 MiB、会话文件≤34 MiB、日志≤2 MiB、磁盘增长≤64 MiB、外部 150 秒。使用现有 Windows Job 只管理本任务树；不关闭已有 Unity。采样有间隔，非连续峰值。

只允许明确的 loopback/RFC1918 IPv4，LAN 还须指定预期对端；默认独立端口 15055，CLI 拒绝生产端口 5005，不设置防火墙规则。工具没有 YOLO 推理；空检测回复只是采集确认，不能算作生产识别或手势交互成功。

`PicoYoloFrameSender` 也能编码编辑器 Texture2D，PICF 格式没有设备/相机/曝光身份认证。v1 的场景仅来自操作员会话；v2 的场景字节也不是图片里的目标标签。来源即使声明 `pico_camera_claimed`，`device_domain_verified`、`target_presence_verified`、`independent_project_test_eligible`、`training_approved` 均保持 false。发送端现有时间戳在 JPEG 编码后取 UTC，并非曝光时间或时钟同步证明。相机眼别、内参和立体配对也不能从此协议证明。后续须用独立设备/应用证据及逐图审核提升范围，不自动混入任何冻结数据。

## APK 与设备前提

`apk-preflight.json` 实际重算旧 `HciCatGaitV2Validation-20261116.apk` 的 77,455,483 字节 SHA，与既有发布记录 `dc2e8c59198e81d363962bb41e946eb9f17c932402d96b528ac15bd1e6d8aeab` 一致。aapt 确认包名 `com.yn.picmagicmr.validation`，声明 CAMERA/INTERNET 等权限；声明不等于运行时授权或 SDK 相机可用。

`apk-capture-source-parity.json` 只读比较历史 runtime 提交 `fcdce1fc16c5e10f237907a8e12970c47fa48f95`：PicoYoloFrameSender、PicoCameraCapture、TcpClient 三个源文件与当前 LF 文本相同。历史包场景入口为 BridgeStereoFusion，源场景 host=127.0.0.1、port=5005、自动重连开启。可以在设备和已装包核实后，将设备 localhost:5005 通过 USB 转发至 PC 的采集端口 15055；本轮**没有执行转发、安装或启动设备应用**。

该历史 APK 不含本轮最新火机样式/青铜候选验收，不是当前全场景新包。相机三源码对应也不等于全部当前源码或打包内容通过。

`device-inventory.json`：用现有 SDK adb 检查，未检测到设备。启动了正常 adb 服务，没有重启既有 adb 服务或电脑。已请求用户连接 PICO、启用 USB 调试并在头显授权；下一步先核对型号、已装包哈希、相机权限和实际相机日志，再开始有界采集。当前 PICO/真实相机/真实 YOLO/Android 新包/32 效果验收仍全部未因本工具完成。

## 命令

只运行合成回环自测，使用全新的 RunId/ReportId：
```powershell
& Tools/run_picf_capture_bounded.ps1 -SelfTest -RunId picf-loopback-newid -ReportId picf-loopback-newid
```

核对设备、APK 与转发后才运行实际采集会话；下面是配置示例，尚未执行：
```powershell
& Tools/run_picf_capture_bounded.ps1 -Capture -RunId pico-tree-session-newid -ReportId pico-tree-session-newid -Scene tree -DeclaredSource pico_camera_claimed
```

保持原 Goal：RAM≥6 GiB 后先做当前 LighterAgency、BronzeFullBody 原生检查并实际看图，再执行固定 Statue 训练；PICO 原图采集有助于补域内数据，不能替代模型、完整交互、渲染或设备验收。

## 本批交付范围

仅选择三个新采集工具及本目录的技术证据，作为 `restore-4d-lighter` 上的采集候选；原始相机目录、旧 APK 和其他未提交开发不属于本批清单。现有 Git 凭据已通过 GitHub 用户 API 核对为 JC4747-jc，提交沿用该身份，不改写历史作者或 traeye 分支。

交付预检父提交 `19298632ba9a0bf4c727c5e036f085eda311bb21` 的生产 `scene_protocol.py` 与本轮测试版本逐字节相同。其 Windows Job helper 比当前工作区少 Contains/Include 两个新增 API；采集启动器只调用原有 Attach/Dispose。为避免替换远程已有 helper，另将该父提交的 helper 原样加载进独立 PowerShell，再运行同一个采集启动器：回环 3 包/2 图/1354 字节通过，且确认所用类型没有 Contains 方法。证据见 `compatibility/`；这次受影响依赖的实际检查通过后，远程 helper 保持原样。

本批不包含当前全场景运行通过声明。正式上传的提交与显式文件哈希清单由独立交付回执记录，不把技术工具发布标成设备或模型验收。
