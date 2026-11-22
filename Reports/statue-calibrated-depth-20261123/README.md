# 石像标定实拍双目与选择验证

输入沿用 ETH3D 同一件石像的 4 对真实校正图，画面 640×480，相机基线 0.56–0.64 m。原项目 PC 服务以隔离的 YOLOv8m-OIV7 Sculpture 候选对每对图的固定左图重复请求 3 次，四个独立会话共 12 条 PICF 回包。每对图用固定真实像素和相机标定验证深度，三次重复用于验证确认逻辑。[数据来源](https://www.eth3d.net/datasets) · [输入照片哈希](../public-statue-eth3d-20261123/frozen-input.json) · [12 条回包](picf-bursts.json)

| 校正视角 | 原 PC 左帧 | 原 C# 双目匹配深度 | Unity 接收器与选择 |
| --- | --- | ---: | --- |
| 493–494 | 置信度 .443 | 接收器拦截 | 3 次低置信，无可选目标 |
| 494–495 | 置信度 .597 | 1.9939 m | 第三次确认，原实例 ID 通过测试捏合选中 |
| 495–496 | 无检测框 | 接收器拦截 | 3 次无检测框，无可选目标 |
| 496–497 | 置信度 .662 | 1.9388 m | 第三次确认，原实例 ID 通过测试捏合选中 |

离线执行 `384039a` 的原 C# StereoPatchMatcher 和接收器的深度门控。用评审框中心作独立对照时，四组均有可用深度。额外开启双向一致性会使 494–495 的实际检测框因有效样本不足被拒，生产石像设置维持原样。测量深度来自检测框周围的表面纹理，区域参考深度来自稀疏标定点，两者采样位置有差异。[深度结果](depth-result.json) · [逐采样点结果](patch-measurements.json)

Unity 使用 `384039a` 的 1,862 项场景、资产与配置和 2 项临时编辑器检查文件，打开保存的 ParkStatue，经原 ParkStereoReceiver 消费上述 PC 回包。相机位姿和点选捏合由检查脚本提供。首轮检查把正向光学深度与负向世界 Z 直接比较，保留失败记录。第二轮使用项目的坐标转换，在两个可选实例上验证通过。隔离项目逐文件恢复已复核。[第二轮输入清单](input-v2.json) · [选择轨迹](trace.txt) · [首轮失败轨迹](trace.txt.attempt1)

本轮各任务串行，最低可用 RAM 采样 6.52 GiB，最高进程树 RSS 采样 1,511 MiB，Unity 既有项目所在 D 盘最大阶段采样净增 1 MiB，保护未触发。[第二轮 Unity 资源](unity-resources.json) · [首轮 Unity 资源](unity-resources-attempt1.json) · [离线匹配资源](depth-resources.json) · [PC 资源](picf-resources.json)

深度结果 SHA256 为 `f374a7197492b5255ba85ab6bf805973a50f3f2371bd3236b1049fcbd4775f9f`，第二轮 Unity 输入 SHA256 为 `dc08920c045c5ab0ab6e57fd03dcbba0742a8ee11e60da41e0e76b3726e24ecf`，12 条回包 SHA256 为 `c261eeb52c0f2642e96054b71cc9519da6983aed670d06720fe669d5ae422c87`。默认石像权重仍为空。真实相机移动、跨实例识别、青铜兽现场定位、四种手势效果的连续实拍及 PICO 待验。
