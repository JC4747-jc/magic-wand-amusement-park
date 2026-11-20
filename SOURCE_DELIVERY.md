# 狗四拍原地行走：已核本地范围的源码候选（2026-11-20）

父版本 restore-4d-lighter@bc27d84ed46ad990a122cb20053652bab3c5acaa，基线仍 codex/mr-development-snapshot-20261109@8359a0c。新增独立 WolfhoundFourBeatWalk.anim（1.6 秒/49 关键姿态）并让既有狗控制器的 Walk in place 状态引用它；原 FBX、47 骨骼/849 三角面模型、贴图、源动作、运行时 HCI/识别/手势代码、原场景和火机全部保留。旧源动作仍在 FBX 内。新动画沿用现有 Wolfhound 骨骼授权并补充 CC BY-SA 3.0 修改说明。

制作的是实际可运行的 Unity 动画资产：四脚错相位的平滑轨迹，以原中立膝盖弯曲方向为约束做解析髋/膝求解，脚踝/趾姿态固定，并按实际 TwoBones 蒙皮校正脚底。静态站姿调整包括四个髋关节，为腿部弯曲留余量；四脚目标独立保持参考平面，识别根/尺寸锚点不移动，没有逐帧把整个模型上下挪动以掩盖腾空。编辑器安装器以后优先复用新动作，现有模型/meta GUID 不改。

当前原保存 ParkPet 经原 Virtual test/Preview Dog/Walk 按钮及射线回调，保存控制器直接播放，无临时 override。两完整周期 191 个实际 BakeMesh/正常 LateUpdate 采样始终至少三脚接近参考平面，零全脚离地；最低网格下穿约 0.00002259 场景单位，最大连续骨骼角速度 358.65 度/秒，固定根/尺寸和返回待机通过。原动作两周期 128 采样有 48 个全网格离参考地面，最大约 0.02997；比较完整周期而非匹配相位，不将采样数比例当连续时长比例或标定物理厘米。

原 Cat/Dog 行走按钮、Stop/Undo/Reset/跟踪丢失/退出/live 限制回归通过；Pet A/B/C/D 的生产分类器合成关节轨迹、漫画轮廓/梦境/戳破/好感独立状态、同帧像素/共享骨骼、fit/anchor 和 Reset 再核通过。8 步态+5 原相机生命周期+16 HCI 原生图共 29 张已查看。旧猫手算落地检查不升级为实际 TwoBones 物理落地验收。主场景模型/UI 仍需最终可读性和美术验收，不把基本原地预览说成真实动物运动捕捉、地面摩擦或完整生物动作。

关键失败保留在本地：独立 CCD 姿态虽然落地，却被实际 4301.597 度/秒关节突变护栏拒绝；连续 CCD 又被 179 度折叠护栏拒绝。最终改用中立膝盖方向约束的解析求解，并核对源骨架发现后髋不属于 spine1 子树，改为四髋共同的骨架站姿。原有脚底/可达性/角度/首尾/实际速度门槛均未放宽。一次孤立 meta 被 Unity 移除后，恢复原精确 188 字节/哈希/GUID；后续只在原资产对象内更新，现有 GUID 保持。编辑器 API/预览 clip 选择失败也有记录，不重试旧失败前提。

新 Android 本地候选 DogFourBeatValidation-20261120.apk：78008507 字节，SHA256 ed111617feea1bf88fb2f2b14163f4fdbe051262c2f4864de3329b5d8049bd59。BuildReport 列出新动作，保存控制器/动画 GUID 3cc6091b514eaf942bd9f535c2b9f408 与包内同 GUID 数据及 WolfhoundFourBeatWalk 名称相符；签名、CRC、15 场景、16 个 ARM64、zipalign4、min29/target35 通过。检查器据 APK 绑定的 BuildReport 选择旧/新动作期望，旧包仍识别 Wolfhound_WalkSource。已恢复 SDK 已知四项预加载副作用及旧默认 APK/旧报告，所有已存在版本包保留。无 APK 上传/安装/PICO 执行。

- [便携证据与来源/输入/产物 SHA 摘要](Reports/dog-walk-authoring-20261120/delivery-summary.json)。原日志、源清单、失败和逐帧图只保留本地，摘要中的原始路径不代表全部发布。
- [新步态八帧](Reports/dog-walk-authoring-20261120/dog-four-beat-installed-frames.png)、[新旧全网格离地曲线](Reports/dog-walk-authoring-20261120/dog-four-beat-ground-comparison.png)、[宠物 HCI 画面](Reports/dog-walk-authoring-20261120/hci-outline-v1-frames.png) 均来自实际 Unity 输出。
- [缺口表 v4](Reports/delivery-gap-index-20261120/CASE_SCENE_STATUS-v4.md) 与 [结构化索引](Reports/delivery-gap-index-20261120/inventory-v4.json)；旧版本保持各自输入范围，不因本次狗增量升级为全工程重跑。

十二个显式源码/资产文件与候选逐字节一致后接入当前 mr；保留其他用户暂存/删除/未跟踪内容，主 HEAD/索引不动。独立索引以 JC4747-jc 身份发表，其他分支尤其 traeye 不动，历史不改写，无定时四小时提交。SOURCE_VALIDATION.sourcePayloadTree 是完整候选树排除元数据自身；payloadFiles 仅为受限已核范围，继承内容不会自动新增验收。

真实 YOLO→实际 HCI 手势→魔法、PICO VST/双眼/遮挡/性能、独立数据和非火机模型质量、最终美术及完整 8 案例/32 效果仍待完成。嗅探不算进食或睡眠，新步态不代表这些缺口消失；头显仍按用户安排暂不接。火机原码/外观/效果/权重及 meta 继续冻结，完整 Goal 未完成。
