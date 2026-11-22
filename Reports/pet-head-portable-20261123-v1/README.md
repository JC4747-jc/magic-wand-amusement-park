# ParkPet 同帧头位服务独立入口

PC 头位服务从干净目录运行。通过 `--model` 显式传入本地 AP-10K ONNX，权重哈希校验在打开端口前完成。代码复用现有网络封包与已验证的鼻眼算法，生产入口不依赖研究脚本或历史缓存路径。Unity 端候选源码采用前一轮验证过的捕获与失效规则。ParkPet 保存场景保留端口零，正式 YOLO 权重和 Android 包沿用现有版本。[启动说明](../../object-detection-demo/README.md#可选宠物头位服务) · [前一轮候选证据](../bridge-live-pet-runtime-head-20261123-v9/README.md)

- 四项协议测试通过，覆盖拒绝错误权重、图块长度、请求元数据和图块坐标恢复。独立命令收到畸形请求后仍可服务下一个连接。用原捕获图块复测三帧，右目匹配导致的鼻眼投影坐标和视差与前一轮逐项相同。独立请求约 93–94 ms。[协议测试](protocol-tests.txt) · [独立结果](result.json)
- 保存的 Bridge→ParkPet 经原 PICF 与本地猫候选取得 12 条真实回包。头位服务部署在独立目录。六次有效结果的采集至接收年龄为 148.5–187.8 ms，三帧确认后两次挂接同一实例。传输延迟跨过 300 ms 后清除，恢复重做三次确认。新的检测 ID 未继承旧头位。[Unity 轨迹](runtime-trace.txt) · [真实 PICF 回包](runtime-picf.jsonl) · [头位服务结果](runtime-head.log)
- 前一轮关闭端口时的犬 YOLOv8n 实时回归继续适用，Unity 候选源码逐文件哈希与该轮一致。已有 F2G 的 16 项 PC 测试复跑通过。此次用桌面供帧和合成点捏，PICO 无线、实手接触、正式猫狗自动头位仍待验。[默认犬回归](../bridge-live-pet-runtime-head-20261123-v9/default-dog-trace.txt)

复跑最低采样可用内存 5.50 GiB，候选盘最大采样净增 0 MiB，资源保护未触发。模型沿用本机文件。[资源采样](runtime-unity-resources.json) · [发布清单](verification.json)
