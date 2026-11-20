# 房间山湖窗景与资源释放：源码候选交付（2026-11-20）

父版本 restore-4d-lighter@1f443d3b73334406c94053925cd683b20d3cd6e2；开发基线仍 codex/mr-development-snapshot-20261109@8359a0c。把房间B的天空/方块山脊替换为原创山湖浮雕网格与云水shader，保留原窗框、位置、.65秒开合和A/B/C/D共存、撤销、停止、丢失恢复；不修改相机透视画面或火机。运行时只有RoomMagicVisual和新RoomWindowLandscape网格生成器、新RoomWindowLandscape.shader改变，其他新增检查均为Editor/工具。

网格426顶点/336三角形，窗景1渲染器+6个窗框、无碰撞，局部深度<.025m。雪山、山坡、树林和湖面为真实Unity网格，太阳、缓慢云影和水纹为shader；不是概念图、外部3D世界或真实房间重建。17张960x800原生渲染及3张总览/对比实际查看；B约.458秒相位推进.0824，正面相机云/水ROI都出现像素变化（总15152像素阈值3）。这只证明取样运动。座椅仍遮挡较多，阅读角仍是代理，最终美术、长期动作与双眼舒适度未验收。

原生保存ParkRoom合成A/B/C/D、家具材质稳定、共存/Undo/Stop/追踪丢失恢复/Reset、新几何/材质/GPU相位检查退出0（42.99秒，最低采样RAM7.549GiB，拥有进程RSS1561.58MiB）。第一次shader变量line为HLSL保留字被正确拒绝，改为rippleLine、冻结新输入后第二次通过；失败日志保留，不把失败算作通过。

新增Editor RoomWindowOwnershipChecks在空场景保留外部父对象，执行20轮Unity延迟Destroy：10轮只移除组件（拥有GameObject仍存活）、10轮移除拥有对象。每轮等待后网格、两种材质和窗体子对象均回到0，最大观察1网格/2材质；17.78秒，最低采样RAM7.526GiB/RSS1622.98MiB。证据限定为对象释放，不代表长期进程RAM、帧率或PICO通过。

PowerShell 7：Tools/run_room_magic_review_bounded.ps1 -ProjectRoot <已配置SDK的隔离工程绝对路径> -RunId <唯一名称>。默认Check RoomMagic运行保存场景/截图；增加 -Check RoomWindowOwnership 只运行对象生命周期检查，输出 .codex-tmp/room-window-ownership，不产生画面。两模式都拒绝覆盖已有结果，需无已有Unity、6GiB空闲RAM，C盘20GiB/工程盘25GiB余量；使用 D:/Unity/Hub/Editor/6000.0.33f1/Editor/Unity.exe，240秒/空闲RAM3GiB/RSS3584MiB/净盘增512MiB/截图32MiB/日志8MiB，拥有进程Job，单工作线程。候选 SOURCE_VALIDATION.payloadFiles 必须精确匹配；可选EditorOverrideManifest仍只允许显式房间fixture与RoomStyle水彩shader两项。截图必须另行查看，采样不是连续峰值。

对应本地APK RoomWindowValidation-20261120.apk：78064869B，SHA256 b7da2296950e73765fd2153119f7bb16861054d04a591fc071c904e006f0fe89。15场景、签名CRC、打包资源、新网格C#类/访问器IL2CPP metadata、新shader编译序列化、16 ARM64 ELF、zipalign、min29/target35通过；最低采样RAM3.369GiB/RSS5618MiB，编译并发1，最大净盘增80MiB。后续只增加Editor/工具，因此复用精确相同player输入的包，不重复构建。包未上传，旧包保持；SDK四项XR预加载差异已归档并精确恢复。

便携摘要：Reports/room-window-landscape-20261120/delivery-summary.json，含源码、画面与详细报告哈希。SOURCE_VALIDATION的sourcePayloadTree为完整候选Git树去掉其自身；payloadFiles仅声明核对范围，其他继承内容不新增验收。独立索引、显式13项、JC4747-jc身份；主暂存/HEAD/用户改动与traeye分支不动，不强推，不进行四小时自动上传。实际远端结果由单独回执核对。

未完成：真实房间定位与遮挡、生产YOLO到HCI真实手势到效果、独立项目数据和非火机模型准确率、最终美术/长期动作/设备帧率、PICO安装启动与双眼，以及完整8案例32效果。用户暂不连接设备。火机场景、模型、动画、效果、原YOLO权重和生产手势规则保持冻结；生产Tree模型仍未部署。前序水彩与路由门禁摘要继续保留。
