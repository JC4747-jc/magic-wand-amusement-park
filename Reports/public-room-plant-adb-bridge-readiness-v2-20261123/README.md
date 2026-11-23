# PICO 连接前的 PC 桥接实测

已核对已发布的 Android 候选包与 GitHub `mr/jc@1d4cc53`。从 APK 来源 `9b557b3` 到当前版本，`Assets`、`Packages`、`ProjectSettings` 和 PC 识别目录的 Git 对象相同。本机包及发布回执的 SHA256 均为 `c9f9ea3399cd6523bda9c21f0ab75bada6635bba4ae3eaa64518532bc1c7e5d9`。抽查 APK 权限包含相机、网络和 PICO 手部追踪，启动包名为 `com.yn.picmagicmr.validation`。BridgeStereoFusion 和六个 Park 保存场景各只有一个已启用的生产 TCP 客户端，均连接 `127.0.0.1:5005` 且启用自动重连。[场景与包的核对明细](result.json) · [已发布 APK](https://github.com/JC4747-jc/magic-wand-amusement-park/releases/tag/mr-jc-pet-head-android-20261123)

用发布分支的原 PC 命令行入口在本机 5005 端口启动服务。两次独立 TCP 会话发出 13 个照片请求，原 v1 火机与 v2 场景帧均收到正确帧号和时间戳。直接进入场景时，实拍回包分别给出 1 个火机、2 把椅子、1 株盆栽和 2 只狗。房间和宠物在同一会话内重复请求时保留各自两个 ID，断开重连后分配了新会话 ID。树、虫、石像各回明确的待配置权重状态，检测列表为空。帧级语义已人工核对，框与空间配准精度仍需现场量测。[13 条原始回包](raw-picf.json)

另外用原发现路由在同一会话回放 10 帧。默认每帧只跑一个权重，火机模型与共享的盆栽/宠物模型交替。第 1 帧路由至火机，第 4 帧至盆栽，第 6 和第 8 帧均给出两只狗，两个 ID 各自不变。非对应模型轮询时的空回包符合现有预算。房间没有加入发现候选，直接房间路由仍识别两把椅子。[发现路由回包](../public-room-plant-adb-bridge-discovery-20261123/raw-picf.json) · [轮询和目标结果](../public-room-plant-adb-bridge-discovery-20261123/result.json)

ADB 当时没有列出设备。现场仍须绑定该头显的 `adb reverse tcp:5005 tcp:5005`，再检查安装、首次权限、双眼相机、实手输入及设备上的断线恢复。本轮用照片接收真实 YOLO 回包，运行范围在 PC。首轮启动等待在 Windows 上收到连接超时，改为识别启动期间的短暂超时后复跑，生产服务及包的代码不变。

直接回包与发现两次有界检查最低采样 RAM 分别为 7.53 和 7.74 GiB，进程树最高采样 RSS 分别为 837.19 和 709.20 MiB。资源保护均未触发，试验所用的 PC 服务进程已退出。直接复测回包 SHA256 `1b8b6b3061195b23cbf8ba84b56fcfa47d7f62f6ba3f09e71c76d8f1bb2c4327`，发现回包 `291a65f0a7896512502578b8ec240cc9105df0f706a75c9b5c6eb8f0a9d264b7`。[直接测试资源](resources.json) · [发现测试资源](../public-room-plant-adb-bridge-discovery-20261123/resources.json)
