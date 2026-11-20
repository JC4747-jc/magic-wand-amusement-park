# 交付证据与缺口 v3

v1/v2保留。本版增加同一当前输入下SampleScene与DesktopCaptureDemo各自的实际截图和完整捕获生命周期记录；仅修正Editor场景假设，未修改场景/运行时/火机，不升级为完整验收。

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

详细SHA、原报告范围及32效果状态见 [inventory-v3.json](inventory-v3.json)；[v1](CASE_SCENE_STATUS-v1.md)仍可复核。

## v3 捕获补充

- SampleScene与DesktopCaptureDemo分别运行原场景，两次均终态0；逐张实际查看各自捕获PNG。Sample是天空/地面前的灰立方体，Desktop是黑底灰立方体，美术均未完成验收。
- Sample没有桌面专用复位按钮，以既有ResetCapture API验证流程；Desktop仍调用真实复位按钮回调。未验证键盘、手或AR触发。
- 验证卡片实际可见性及背包仍显示，不再误将Sample共享Canvas当卡片。重复捕获、途中复位、暂停时间完成、禁用/启用、换目标清理通过。
- 失败v1/v2/v3保留：专用按钮缺失、共享Canvas断言错误、一次遗漏旧引用导致CS0103；v4修正后两场景通过。详见Reports/sample-capture-review-20261120/verification.json。
- Android f3c8553d保持原字节；真实摄像、YOLO、HCI、PICO和最终画面均未因本次检查判通过。
