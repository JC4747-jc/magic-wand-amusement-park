# 出租屋水彩与运动：源码候选交付（2026-11-20）

父版本：restore-4d-lighter@533ebe214200295fe76843d2140578525214df55；开发基线仍为 codex/mr-development-snapshot-20261109@8359a0c。本次仅改房间 RoomStyle.shader 的水彩部分，保留源色、分层水洗、局部纸纹和笔触；赛博片段及自然模式原材质不变。火机场景、模型、动画、效果、YOLO权重和生产手势规则未改，前一版非火机自动路由门禁保持。

Assets/Editor/HciRoomMagicChecks.cs 修正过时的D测试输入：3个握拳样本，实测约.125秒，落在未改生产规则.04–.55秒内，两次各触发1事件。新增C水母隔离GPU渲染两次取样、D糖果池位置两次取样。它们是明确合成的手势/校准空间测试；不替代生产 BridgeStereoFusion 真实YOLO、HCI手势或PICO验证。

保存的 ParkRoom 本地原生回归退出0（44.36秒/59采样，最低可用RAM7.180GiB，最高拥有进程RSS1903.71MiB）。A水彩/赛博、B开窗、C水母灯、D合成天花板门控和对象池、共存、Undo/Stop/丢失恢复/Reset通过；13张960x800原图及3张对比/总览均实际查看。C约.453秒相位变化1.016rad，两次轮廓XOR4911像素；D约.431秒24/24对象移动、池容量32。两次取样不代表全周期或长期流畅。墙面色度增加而书本/座椅色度下降，全部ROI结果保留；数值变化不是美术验收。

回归入口：PowerShell 7 的 Tools/run_room_magic_review_bounded.ps1 -ProjectRoot <已配置SDK的隔离工程绝对路径> -RunId <新的唯一名称>。使用现有 D:/Unity/Hub/Editor/6000.0.33f1/Editor/Unity.exe；需6GiB空闲RAM、无已有Unity，C盘20GiB/工程盘25GiB余量。限制240秒、3GiB空闲RAM、拥有进程RSS3584MiB、512MiB净盘增长、32MiB截图、8MiB日志；单编译工作线程。输出在调用工具所在工程 Reports/room-effect-review-20261120/<RunId>，候选截图在 .codex-tmp/hci-room-magic；拒绝覆盖旧结果。可选 EditorOverrideManifest 仅接受 Reports 内显式绑定原SHA/新SHA的房间Editor与shader两项；默认要求 SOURCE_VALIDATION.payloadFiles 精确匹配。源码/截图/日志应保留；不能用启动器PASS替代查看画面。采样不是连续峰值。

对应本地APK：RoomWatercolorValidation-20261120.apk，78,059,179B，SHA256 1a685e2f9d8ac8511d080a15fd7ab8220549b82bf43ee78fb84aa010dfbbd231。15场景、签名/CRC/打包资源/16 ARM64 ELF/4字节ZIP对齐/min29target35及RoomStyle实际编译序列化通过；最低采样RAM5.076GiB，最高拥有进程RSS3546MiB。APK未上传，旧包保持；打包时Editor代码不进入player。详细便携摘要：Reports/room-watercolor-motion-20261120/delivery-summary.json，包含输入与报告/画面哈希。

SOURCE_VALIDATION.json 的 sourcePayloadTree 定义为完整候选Git树去掉其自身；payloadFiles声明显式核对范围，其余继承路径原样保留，不升级验收。通过独立索引提交，不动主索引、用户暂存、主HEAD或traeye分支；不强推，不进行四小时自动上传。实际提交和远端状态以单独回执为准。

未完成：阅读角只是代理房间，窗外仍是青灰平面；真实房间适配、最终美术、长时间运动、独立实拍数据与非火机模型准确率、PICO安装启动与真实相机/定位/手势全链、8案例32效果整体验收。设备按用户安排暂不连接。前一版门禁原生和Android证据见 Reports/nonfire-validation-native-20261120/delivery-summary.json。
