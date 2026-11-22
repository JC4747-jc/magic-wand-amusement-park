# 出租屋 · 雪山天鹅湖与水母灯

在 BridgeStereoFusion 选择出租屋并完成墙面定位后，B 魔法让照片化成 1,536 个采样其原色的粒子，旋入施法手掌，再沿弧线飞到定位处。湖面从中心展开，边缘留下细碎空隙。原创湖景采用浅浮雕表现近水和远山的视差。C 魔法独立唤起头顶的水母灯，伞盖收缩，触须随漂移动作摆动。房间保留完整微缩墙面、窗洞、椅子、圆桌和储物柜。

[手掌聚拢](captures/swan-lake-palm.png) · [粒子释放](captures/swan-lake-release.png) · [墙上湖景](captures/swan-lake-wall.png) · [水母灯与房间](captures/recognized-room-effects.png)

## 验证范围

2026-11-23 北京时间，在 Unity Editor 由保存的 BridgeStereoFusion 进入 ParkRoom，通过原 TCP 组件回放已记录的 YOLO 回包，并注入合成姿态和手势。59 个有效回包维持同一目标，四种房间效果可共存。中断回包后目标过期，恢复后旧窗口清空，需要重新标定。检查了掌心跟随、窗口内外透明度、恢复链路及微缩房间双视点。已查看四张阶段图。透明背景显示的是虚拟层，现场合成由 PICO 验收。

会议 PDF 第 6 页作为房间墙窗和活体水母灯的效果依据。触发沿用工程的 HCI 映射。B 触发后自动聚拢和释放。当前湖景版本通过 Editor 回放。新版 Android 封包、现场 YOLO、真实手势、头显双眼透视及遮挡待验。

该次运行约 29 秒。资源采样最低可用内存 6.19 GiB，任务进程组最高 1,728 MiB，目标盘相对启动时的占用增量最大 3.0 MiB。资源保护未触发。详见[逐文件哈希及验证摘要](verification.json)和[回放记录](captures/trace.txt)。

## 分支内容与复现

本次交付以指定开发基线 `8359a0cb7c700a0d6f91b6804fae9cb6758c33b2` 为父提交。分支 Assets 内可直接取用微缩结构、家具材质与模型、原创天鹅湖纹理及着色器、水母网格、材质、预制件和动画脚本。家具的来源与许可随资源保留。[家具与墙面清单](../room-art-delivery-20261123/source-manifest.json)

完整交互的[源码补丁](unity-editor-source.patch)针对此前已发布的 JC 整合提交 `312fc0d5495a7cbfb5cd6c58da471078ecf78a77`。在该提交的独立干净工作区执行下列命令，将最后一个参数换成下载的补丁路径。构建环境和本地包准备沿用该提交的 LOCAL_DEPENDENCIES.md。

```sh
git apply --check --binary /path/to/unity-editor-source.patch
git apply --binary /path/to/unity-editor-source.patch
```

已在独立 Git 索引复核补丁应用。还原后的树为 `00405345263b10154144e41ef088d40a1b366b7d`，Assets、Packages 和 ProjectSettings 与本轮冻结的 1,854 个 Editor 输入逐项对应。补丁包含 69 个新增或更改文件，并排除四个 Unity 自动生成的旧 XR 模拟偏好文件。项目库、APK、训练缓存、用户参考截图与会议 PDF 均未打入补丁。
