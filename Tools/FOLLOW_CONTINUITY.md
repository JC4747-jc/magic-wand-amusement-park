# 同帧检测连续性对照

本轮代码已通过 Unity `StereoIntegrationChecks.Run()`（包含低分双目投影与 `VisualCandidateChecks` 多候选关联检查）、11 项检测配置／TCP／姿态就绪回归和 6 项离线统计回归。Unity 日志为 `.codex-tmp/follow-continuity-final.log`。本轮未重新打包安装或重启正在运行的检测服务。

实机验收时，在同一段“静止 → 拿起 → 缓慢三轴移动 → 短暂遮挡 → 放下”录制上对照两组参数，检查冻结时长、错误跳动与恢复；同步保留头显 `[StereoMeasure]`、`[StereoYOLO]` 和 `[FollowMetrics]` 日志，用最终位置更新判断效果。首次标定仍需直立、静止且完整可见。训练补数、实机跟随验收和第二轮丢失恢复调整尚未完成。

`compare_follow_continuity.py` 离线比较 v4、v6，分别运行旧参数 `conf=0.399 / max_det=1` 和候选参数 `conf=0.28 / max_det=5`。不连接检测服务，不替换部署权重。

在项目根目录运行：

```powershell
.\object-detection-demo\.venv-gpu\Scripts\python.exe Tools\compare_follow_continuity.py --manifest recordings\movement\frames.jsonl --output .codex-tmp\movement-comparison --recording-note '实机录制日期、静止/拿起/移动/放下过程、是否完整连续采样'
```

默认用 CPU，避免和运行中的 GPU 检测服务争用；离线时可指定 `--device 0`。默认对输入图像按 JPEG 质量 90 编码，与当前位置发送通路一致。如果输入已经是发送给检测器的 JPEG，指定 `--jpeg-quality 0`，避免再次压缩。保持图像方向与检测服务一致。

`frames.jsonl` 每行一个采集帧，路径相对清单所在目录，时间单位为秒且严格递增：

```json
{"image":"000001.png","timestamp_s":0.0,"frame_id":1}
{"image":"000002.png","timestamp_s":0.15,"frame_id":2}
```

需要包含没有检测到打火机的帧；只保存成功检测的帧会使连续性统计失真。每次只比较一段采集会话，避免把重启或重新连接前后的时间串起来。

已有 F2G 录制目录（每帧一个子目录，包含 `header.json` 和 `left.png`）可用 `--captures DIR` 替代 `--manifest`。目录包含多次连接时，必须用 `--session ID` 选择一次会话。

输出包含：

- `input-frames.json`：输入路径、时间、帧 ID 和实际推理像素 SHA-256。
- `v4/v6-baseline/candidates-frames.jsonl`：每帧的全部打火机候选框与分数，保留空结果。
- `summary.json`：模型 SHA-256、环境版本、采样间隔、各阈值下有预测框的帧比例、连续无框帧数和采样时间跨度、候选数、相邻有框帧的最高分框位移和尺寸变化。

**这些指标不等于识别准确率或三维跟随效果。** 无标注时无法区分误检、目标切换和真实运动；框位移也包含物体和头部运动。没有检测的单个采样帧的时间跨度记为 0，不能把它当作实际丢失 0 秒。工具不执行双目深度、首次标定、Unity 目标关联和三帧平均，不能输出最终有效位置更新率。

本次仅对 2026-11-03 的历史 F2G 稀疏录制完成工具冒烟验证，结果保存在 `.codex-tmp/follow-continuity-20261112/`，没有用它决定换回 v4。该记录的 26 帧跨约 39.5 秒，采样间隔中位数约 0.97 秒，不能替代当前版本“静止 → 拿起 → 移动 → 放下”的完整实机对照。

统计逻辑回归：

```powershell
.\object-detection-demo\.venv-gpu\Scripts\python.exe -m unittest discover -s Tools -p test_follow_continuity.py -v
```
