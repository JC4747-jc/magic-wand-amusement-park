# 宠物 HCI 梦境交互修复

HCI 分支要求 B 画圈打开梦境泡、C 横挥戳破；此前 ParkController 已把它们路由至 Awaken/Wind，但 ParkActor 未执行对应气泡状态。现已补齐两项行为，并更新操作提示；直接触碰的 Poke/Feed 仍要求可靠头部位置，横挥魔法不伪装成实际触碰。

- 修复前，同一个检查在“B 未打开梦境泡”处失败：[原始结果](before-result.txt)。
- 修复后，保存的 ParkPet 场景进入实际 Play Mode，经 Virtual test 按钮创建猫，再切换狗；两者的打开/重复打开、戳破/拒绝空泡、撤销、D 好感/撤销、Stop/重开、Reset、无效/超距/追踪丢失门控以及锚点不动均通过，运行时错误 0。[结果](pet/result.txt)
- 六个 Park 场景原流程回归通过，包括实际按钮回调、内容切换、合成 Fit/Plane、停止、复位、回放边界和对象/音源清理。[结果](six-scenes/result.txt)
- 猫/狗打开、戳破、复位各一张实际场景相机渲染，共六张 PNG。已查看猫打开/戳破和狗打开画面，气泡可见性与状态一致；这些是交互证据，不是美术质量最终验收。

运行入口：Unity Editor 执行 `HciPetDreamChecks.Run`，通过当前项目有界 Unity 启动流程运行；异步 Play Mode 检查自行退出，启动时不要传 `-quit`。本轮没有 Android 重打包或 PICO 实机测试。

## 证据范围

这是合成控制器动作，覆盖当前画圈/横挥路由后的效果，尚未覆盖真实手部识别、YOLO、双眼或头显性能。宠物 A 漫画描边仍有差异，不能据此称整个宠物 HCI 用例完成。

验证在隔离整合工程进行。运行时 ParkActor/ParkController 与主工作区字节一致；ParkSceneFlowChecks 和 ParkPet 场景仅换行不同，Wolfhound 预制体仅三个空字段的尾部空格不同。隔离工程仍使用先前的猫步态 v2 候选 FBX，主工作区正式猫 FBX 未替换。各原始哈希见 [manifest.json](manifest.json)，不能将这次交互回归当作主工作区猫步态已部署验收。

修复检查最低采样空闲 RAM 6.071 GiB、最大采样磁盘增量 1.57 MiB；六场景回归为 6.291 GiB / 1.55 MiB。两次成功运行均 exit 0、guardStop 为 null；五秒离散采样不是连续峰值。失败与成功资源记录均保留，无训练、重启或大型下载。
