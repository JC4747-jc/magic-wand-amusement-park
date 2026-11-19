# 本地 Unity 包依赖

工程使用 PICO SDK 3.4.0 和 Unity MCP Editor 10.3.0。包的版本、文件数量、总字节数、package.json SHA256 和完整内容指纹保存在 `Tools/local-package-pins.json`。

`Packages/manifest.json` 和锁文件只引用工程内的相对路径：

- `file:../.local-packages/pico-sdk`
- `file:../.local-packages/unity-mcp`

`.local-packages` 中是 Windows 目录联接，Git 忽略该目录。SDK 本体继续保存在已安装的位置；引导脚本不复制、下载、安装或修改包内容。

## 首次打开或克隆后

先准备与 pin 一致的现有包，保存并关闭 Unity，再用 PowerShell 7.2 或更高版本运行：

```powershell
pwsh -NoProfile -File Tools/connect_local_packages.ps1 -PicoSdkPath "D:/Unity/PICO" -UnityMcpPath "D:/MagicMRTools/unity-mcp/10.3.0/editor-package" -ReportPath "Reports/local-dependency-check/links.json"
```

上面两个目录是当前机器的安装示例，换机器时通过参数指定实际目录。报告路径必须位于该工程的 Reports 下且尚不存在。完成后从 Unity Hub 打开工程。

脚本先完整验证两个包，再创建联接。已有指向相同目录的联接可复用；普通目录、不同目标、版本或内容不符均拒绝覆盖。包升级需要重新审查 pins 和验证结果，不能只改版本号绕过内容检查。

## 只验证现有联接

使用相同命令，加上 `-VerifyOnly` 并选择新的报告路径。此模式不创建或改变联接。当前校验包括：

- 包名、固定版本、元数据和完整内容指纹；
- 每包最多 5,000 个有效文件、512 MiB，遍历最多 10,000 个文件；
- 两个真实包验证完成后才操作工程联接，已有用户目录保持不变。

原始 SDK 和 MCP 包的供应、许可及更新仍由各自安装来源负责；本仓库不包含其完整副本。

## MCP 服务与验证范围

上述引导处理 Unity 包引用。HTTP MCP 服务还需要独立的 Python 服务环境。当前机器已有 `mcpforunityserver 10.3.0`；其 Python 3.12.14 基座已改为稳定目录 `D:/MagicMRTraining/python-3.12.14-amd64`，没有重装现有包。

`Tools/serve_unity_mcp_bounded.ps1` 使用该现有环境，并提供本任务 Job、RAM/日志/时限以及出生身份和 nonce 对应的停止控制。`Tools/probe_local_package_bindings_bounded.ps1` 与 `Tools/check_local_package_mcp.py` 是当前机器的有界验证入口；服务需先启动，端口不能由别的进程占用。

已有 Python 服务环境时，可在独立终端运行下列服务命令。每次使用新的 RunId；已有报告会被保留并拒绝覆盖。服务到时退出，本次验证结束后也已停止。

```powershell
pwsh -NoProfile -File Tools/serve_unity_mcp_bounded.ps1 -RunId unity-mcp-session-20261120 -MaxSeconds 1800
```

2026-11-20 已实际验证 Unity 包解析、PICO/MCP 程序集加载、MCP 连接，以及通过 MCP 加载和查询保存的非火机 `ParkStatue` 场景；没有保存场景或执行 PICO 真机测试。完整证据见 `Reports/local-package-bootstrap-20261120`。

本步骤不替代真实摄像头 YOLO、物理手势、双眼渲染、阅读距离和设备性能验收。

Unity 相对本地包路径规则：[Unity 6000.0 官方手册](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-localpath.html)。
