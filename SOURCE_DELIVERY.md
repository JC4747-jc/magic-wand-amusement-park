# 非火机自动路由门禁：源码候选交付（2026-11-20）

交付父版本为 restore-4d-lighter@b3b7d35792243b3c4f0675db2337e5dfd72f9275；原开发基线仍为 codex/mr-development-snapshot-20261109@8359a0c。仅修改 DiscoverySelection.cs 的非火机自动路由：明确 UNVALIDATED 的目标不能自动切换，火机旧判断原样保留。火机场景、模型、动画、效果与原 YOLO 权重未改；生产 Tree 权重仍空。

42 项完整 C# 状态机检查、来源过期/返回清理/升级后重新满足稳定窗口、Unity Roslyn 71/58 源编译通过。原生 Unity JsonUtility 实际解析原 PICF/discovery/YOLO 的四帧 JSON，验证非火机拒绝及已有共享 UI；可重复有界工具另外运行通过，并按原哈希清理临时探针，1804 项既有工程源文件不变。摘要见 Reports/nonfire-validation-native-20261120/delivery-summary.json。

回归材料：Tools/NonFireDiscoveryNativeChecks.cs、Tools/run_nonfire_gate_regression.ps1，以及 Tools/ValidationData/nonfire-discovery-yolo-20261120.jsonl 和同名 provenance.json。输入为三帧重复已知公开树图和一帧训练家族背景，不含图片/权重，不用于训练或独立准确率验收。两个检出框包含已知枝冠误报；消息保留 UNVALIDATED 和 pose_available=false。

在已配置 SDK 的隔离 Unity 工程上，运行 Tools/run_nonfire_gate_regression.ps1 -ProjectRoot <隔离工程绝对路径> -RunId <新的唯一名称>。默认 Unity 为 D:/Unity/Hub/Editor/6000.0.33f1/Editor/Unity.exe，可显式覆盖。启动器拒绝已有 Unity/不足6GiB RAM，限制拥有进程、300秒/3GiB空闲RAM/3584MiB RSS/512MiB净盘增长/8MiB日志；只临时导入并按精确哈希删除它创建的检查文件，不覆盖用户源文件或GUID。详细日志由本地 Reports 保留，采样不是连续峰值。

本地 Android：D:/MagicMRSourceCandidate/20261120/Builds/Android/NonFireGateValidation-20261120.apk，78,058,411B，SHA256 bb303d5712b9b39b4a09779e1c7e0e6c86b5ff659645fe08ca6c2a09006a442e。15场景、签名/CRC/打包资源/16 ARM64 ELF/4字节ZIP对齐/min29target35通过；新player元数据包含新路由字段。APK不在源码提交中；旧候选ca9daae4…及主包4a76217e…保持原路径与字节。SDK四项XR预加载移除已归档并恢复，外部包pin只读复核通过。

当前 SOURCE_VALIDATION.json 的 sourcePayloadTree 定义为候选完整 Git 树去掉 SOURCE_VALIDATION.json 自身；payloadFiles 只声明显式核对的源码/证据，其他继承路径原样保留，不因此升级为验收通过。提交及远端发布状态由单独回执核对。主工作区索引与 HEAD 不改；不强推，不改 traeye 分支，不进行四小时自动上传。

尚未验收：PICO 安装/启动和真实同步相机/定位/手势到效果全链、独立项目数据与模型部署、最终美术和8案例/32效果。公开新枯树图仍漏检1，旧诊断仍有重复枝冠框。工具/编译/单图/包检查不替代这些验收。
