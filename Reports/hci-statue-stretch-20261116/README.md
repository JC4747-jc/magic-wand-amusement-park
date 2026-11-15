# HCI 石像 B 全身伸展候选（2026-11-16）

本批解决现有大理石半身像没有双臂、无法完成全身伸懒腰的资源缺口。新增带骨骼全身候选、原创伸展动画与可重现制作脚本，**尚未替换正式 ParkStatue 模型，不能标记 B 场景完成**。

## 与魔法乐园的关系

所有用例继续采用同一路径：YOLO 识别现实目标 → 定位/选择 → 火机共用 `TabletopHandSample` / `TabletopGestureRules` → 分派对象 A/B/C/D 效果。本候选为石像 B 的表现层准备，不增加独立手势，不用图片或按钮演示冒充真实识别闭环。尺寸标定和平面确认仅为定位辅助。

## 资源与制作

- 来源：[Quaternius Universal Base Characters](https://quaternius.com/packs/universalbasecharacters.html)、[作者 itch.io 页面](https://quaternius.itch.io/universal-base-characters)。实际使用免费 Standard 包的 `Superhero_Male_FullBody.fbx`，不是付费完整包中的 Regular 类型。随包许可为 CC0，原文保存在 ArtSource。
- 保留成人全身骨架与眼球，去掉眉毛网格，自行制作暖石材质、骨盆权重披布及 5.067 秒动作。人物为虚构古典风雕塑，不是历史文物扫描，也不提供历史身份或故事依据。
- 动作包含预备、双臂举起、躯干舒展、左右释放、回位。153 帧，30 fps，12,950 三角形。腿、脚和骨盆保持固定。
- 披布根据实际蒙皮身体截面椭圆包络留出间隙，同时移除披布下不可见的身体面，修正首次背面检查发现的髋部和大腿穿透。当前披布仍较简化，肌肉比例来自超级英雄基础模型；不是最终艺术质量验收。
- Blender 的程序化噪声/凹凸不会自动通过 FBX 成为 Unity 材质；Unity 检查使用独立 URP/Lit 石色材质。Blender 展示底座也没有导出到 FBX。

## 验证证据与边界

- Blender 4.5.3：153 帧检查脚和骨盆漂移小于 0.01 mm、手部回到初态，单线程 CPU 渲染。
- Unity 6000.0.33f1：Generic 导入、单片段长度、骨骼引用、三角形预算、153 个姿态的脚/骨盆漂移小于 0.1 mm、模型根节点不变、手部回位，并检查骨骼和真实蒙皮顶点共同运动。数值以 result.txt 为准。
- 六张 Unity 相机图片来自 `SampleAnimation` 后显式 CPU 蒙皮快照。它们是实际网格渲染，**不是 Play Mode 连续播放、正式场景、PICO 透视或双眼截图**。对比静止/抬臂/回位像素以排除缓存姿态假通过。
- 第一轮检查重复应用 FBX 缩放，出现 36 米的错误高度；同步截图未刷新 GPU 蒙皮，错误显示初态。已撤销该轮通过结论，明确使用 `BakeMesh(mesh, true)` 和匹配的 Transform，并增加高度上下限、独立像素对比。诊断失败日志保留本地，不发布为通过证据。
- 正式 Assets/Packages/ProjectSettings 没有因本候选改变。项目文件核对见 manifest；本轮没有重新宣称正式共用手势或 14 场景回归通过。

## 后续验收

1. 确定全身人像与既有大理石/青铜变体的组合，完善披布、石材和角色比例；保留资产来源。
2. 接入石像 B 共用画圈手势，按动画时长处理忙碌状态；检查 A 外观与 D 荷花许愿树互不覆盖。
3. 验证播放、中断、Stop、Undo、Reset、目标丢失和切换，确保实物锚点与标定不漂移；执行生产手势回归。
4. 构建 Android，再接 PICO 验证 YOLO→定位→手势→动画和性能。已有验证 APK 不包含本候选。

## 重现

用 `Tools/prepare_stretch_statue.py` 和 ArtSource 中的原始 FBX，在 Blender 4.5.3 中以 `--background --threads 1 --python-exit-code 1 --python ... -- --source ... --output ...` 运行。输出目录应位于临时目录，不直接写正式 Resources。

在独立、与基线提交匹配的 Unity 项目中，把生成 FBX 放到 `Assets/StatueStretchCandidate/StandingStatue.fbx`，把 `Tools/Unity/StatueStretchAssetChecks.cs` 复制到 `Assets/Editor/`，运行 `-batchmode -job-worker-count 1 -executeMethod StatueStretchAssetChecks.Run`。结果位于该项目 `.codex-tmp/statue-stretch-asset/`。检查会创建空场景，不能对有未保存工作的交互编辑器执行。

本次使用单进程资源守卫：可用 RAM 3 GiB 下限、C 盘 20 GiB 下限、有限磁盘增长/日志/时间；资源 JSON 是间隔采样而非连续峰值。仅下载一次约 122 MB 官方免费包，原包留作缓存，Git 只上传所用文件。未训练、未复制整套资源包、未删除 Library、未自动重启。Goal 保持 active。
