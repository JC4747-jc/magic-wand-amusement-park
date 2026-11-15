# restore-4d-lighter 已验证基线同步

用户授权：将已有可通过文件上传到 4drestore；并要求所有线路遵循火机的 YOLO + 手势识别 → 魔法主线。

- 来源：`codex/yolo-animation-integration-20261115` 的 `f0e6183b20cf1e7fd7ad188431c4934cf80f9bad`。该提交是原目标分支 `831fcc512adb00cb98fb426efcdf726fcfab3757` 的后代，按快进更新，保留历史。
- 同步范围：已有整合源码、自然模型/动画与署名、九个动画预览、训练/构建工具及带范围说明的验证报告。新增改动只涉及文档与移除本次不合格候选权重目录；Assets、Packages、ProjectSettings、运行时检测服务与已验证基线一致。
- Unity 6000.0.33f1：14 场景结构审计无缺失脚本，六个实际保存 Park 场景的按钮/合成动作流程、六项 NUnit 回归及自然模型几何检查通过。原始范围见 [整合验证](github-delivery-20261115/combined-validation/summary.json)。
- 同一整合基线的 AllScenes Android 包已通过构建与签名/内容核验；SHA256 `e4ce79f942817dea569ae0e7a3397b45572a1b92f2f8497a4d8fa922aed20271`。本次不重新构建，也不把 APK 塞入 Git 源码；当前本地交付文件为 `Builds/Android/CombinedAllScenesValidation-20261115.apk`。见 [APK 验证](github-delivery-20261115/combined-validation/android-apk-validation.json)。
- `gap-candidate-20261115-a` 在独立验证未达标，本次分支顶端不纳入其八个候选文件；诊断与原始候选仍保留在来源分支及本地，不替换正式权重。既有历史模型归档保持原状，存档不等于当前验收通过。
- 本地后续植物/宠物效果、猫步态 v2、森林试作及统一手势新调整不包含在此次运行时源码中。植物动作 API 检查通过也不证明与火机共用完整识别；统一所有线路仍是下一步工作。
- 尚未通过：完整八用例 32 项 HCI 闭环、13 类识别准确性、PICO 实机相机/手势/空间定位/双眼/性能。虚拟测试、按钮和手动摆放不能替代正式入口。
- 新 Goal 见 [魔法乐园计划](../BRANCH_SCENE_PLAN.md)。ProBuilder/Polybrush、itch.io、World Labs/fal/image-blaster 等仅用于具体 HCI 缺口，不照搬教程森林/宫殿项目。

本次发布使用独立 Git 索引，不切换、重置或暂存主工作区，不删除用户文件，不执行强推。
