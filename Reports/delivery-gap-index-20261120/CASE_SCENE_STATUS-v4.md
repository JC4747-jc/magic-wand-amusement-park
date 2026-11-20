# 交付证据与缺口 v4

v1/v2/v3保留。此前v3增加同一当前输入下SampleScene与DesktopCaptureDemo各自的实际截图和完整捕获生命周期记录；仅修正Editor场景假设，未修改场景/运行时/火机，不升级为完整验收。

- 8案例/32效果：28项仍有原受限本地证据，火机四效果没有因启动成功而判通过。
- 14个正式清单场景均已关联原入口启动或Park案例原生记录，完整真实输入/设备验收仍未证明。
- ForestBlockout按计划297/380行仅保留为候选研究资产；15场景验包不等于正式APK纳入森林项目。

| 场景 | 本次/既有证据 | 仍未完成 |
|---|---|---|
| BridgeStereoFusion | 当前原样启动0错误/原相机图已看 | 真实输入、设备及对应功能/画面范围 |
| BridgeStereoPoseFusion | 当前原样启动0错误/原相机图已看 | 真实输入、设备及对应功能/画面范围 |
| BridgeStereoRepair | 当前原样启动0错误/原相机图已看 | 真实输入、设备及对应功能/画面范围 |
| BridgeTest | 当前原样启动0错误/原相机图已看 | 真实输入、设备及对应功能/画面范围 |
| DesktopCaptureDemo | 当前原样启动0错误/原相机图已看；捕获/卡片/恢复已验 | 真实YOLO/手势/PICO及最终美术 |
| ForestBlockout | 历史森林白模/几何候选 | 不升级为主线/正式森林成品 |
| MagicMR | 当前原样启动0错误/原相机图已看 | 真实输入、设备及对应功能/画面范围 |
| ParkBugs | Park案例受限原生记录 | 真实YOLO/手势/PICO及最终美术 |
| ParkPet | Park案例受限原生记录 | 真实YOLO/手势/PICO及最终美术 |
| ParkPlant | Park案例受限原生记录 | 真实YOLO/手势/PICO及最终美术 |
| ParkRoom | Park案例受限原生记录 | 真实YOLO/手势/PICO及最终美术 |
| ParkStatue | Park案例受限原生记录 | 真实YOLO/手势/PICO及最终美术 |
| ParkTree | Park案例受限原生记录 | 真实YOLO/手势/PICO及最终美术 |
| SampleScene | 原样启动与自身捕获生命周期通过/API复位/截图已看 | 真实输入、设备及对应功能/画面范围 |
| VstTest | 当前原样启动0错误/原相机图已看 | 真实输入、设备及对应功能/画面范围 |

## 当前警告与界限

Bridge四入口各有PC连接超时警告；SampleScene有四项未激活Windows XR/AR子系统警告。八入口均无真实相机帧；黑背景/UI/测试色块不能算透视或YOLO成功。Desktop捕获图是场景自身灰色立方体，不是后端重建模型或真实相机结果。

详细SHA、原报告范围及32效果状态见 [inventory-v4.json](inventory-v4.json)；[v1](CASE_SCENE_STATUS-v1.md)仍可复核。

## v3 捕获补充

- SampleScene与DesktopCaptureDemo分别运行原场景，两次均终态0；逐张实际查看各自捕获PNG。Sample是天空/地面前的灰立方体，Desktop是黑底灰立方体，美术均未完成验收。
- Sample没有桌面专用复位按钮，以既有ResetCapture API验证流程；Desktop仍调用真实复位按钮回调。未验证键盘、手或AR触发。
- 验证卡片实际可见性及背包仍显示，不再误将Sample共享Canvas当卡片。重复捕获、途中复位、暂停时间完成、禁用/启用、换目标清理通过。
- 失败v1/v2/v3保留：专用按钮缺失、共享Canvas断言错误、一次遗漏旧引用导致CS0103；v4修正后两场景通过。详见Reports/sample-capture-review-20261120/verification.json。
- Android f3c8553d保持原字节；真实摄像、YOLO、HCI、PICO和最终画面均未因本次检查判通过。

## v4 狗四拍步态与新 Android 候选

- 原实际两周期128采样有48个整个网格离开参考地面；新动画以原有许可骨骼制作独立1.6秒四拍循环，原FBX/源动作保留。
- 已保存控制器直接引用新动作，无运行时替换；191个实际TwoBones蒙皮采样始终至少三脚支撑，零全脚离地，最大角速度358.65度/秒。固定参考平面/场景单位，不是实测物理力或滑动摩擦验收。
- 原ParkPet猫狗Walk按钮、Stop/Undo/Reset/丢失/退出/live门槛和A/B/C/D合成生产手势回归通过；29原生图已实际查看，完整美术/生物动作自然度仍待验，不将嗅探当进食/睡眠。
- 新包DogFourBeatValidation-20261120.apk：78008507字节，SHA256 ed111617feea1bf88fb2f2b14163f4fdbe051262c2f4864de3329b5d8049bd59。新动画GUID/对象名、签名CRC、15场景、16个ARM64及zipalign4通过；旧包全部保留，未安装PICO。
- 当前12显式源码/资产已接入mr；完整报告见Reports/dog-walk-authoring-20261120/verification.json。发表源码前仍标候选，实际提交/树/远端字节以独立发布回执为准；旧输入/报告不被此补充升级为全工程重跑。
