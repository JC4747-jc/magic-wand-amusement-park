# PC 检测服务与 Unity PICF

运行 Unity 的 `BridgeStereoFusion` 前，在 PC 的独立 Python 环境装好 [requirements.txt](requirements.txt)，并从仓库根目录运行：

```powershell
.venv/Scripts/python.exe object-detection-demo/tcp_server.py --headless --host 127.0.0.1 --device cpu
```

服务在 TCP 5005 接收 Unity 传来的 JPEG，并回传框、类别、场景、实例 ID、输入帧号和原时间戳。PICF v1 保留火机识别，PICF v2 支持场景选择和发现。房间、植物、宠物共用仓库内已有的 YOLOv8n 权重，火机继续使用本仓库的 YOLO26n v6。树、虫和石像的默认配置目前明确返回 `SCENE_NOT_CONFIGURED`。发现模式按帧轮流使用已配置的模型。两种权重都会从本地读取，启动火机检测时输出 SHA256。

Windows CPU 验证使用 6 条火机回包、2 条房间回包、2 条盆栽回包、1 条狗回包、3 条未配置回包和 2 条发现回包。所有回包均保持输入帧号、尺寸和时间戳。椅子和盆栽在各自重复输入时保持实例 ID。53 项自动回归通过。若 5005 已有服务占用，先确认进程归属，再启动另一实例。[测试记录](../Reports/mr-jc-live-picf-20261123-v3/README.md)

```powershell
.venv/Scripts/python.exe -m unittest discover -s object-detection-demo -v
```

Windows 本机或经过已配置的 USB 转发时使用上面的回环配置。相机采集和设备连接以当前 Unity 工程的部署说明为准。F2G 姿态服务有独立入口 [pose_bridge.py](pose_bridge.py)。本次验证的功能是 TCP 图像检测，PICO 现场速度、深度、实际手势以及全部场景的检测和训练验收仍待完成。

## 独立电脑摄像头演示

`python object-detection-demo/detect_webcam.py` 使用电脑摄像头。它使用独立的早期模型配置。
