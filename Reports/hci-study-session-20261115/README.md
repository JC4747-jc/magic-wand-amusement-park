# 跨线路会话与有界轨迹记录：本地候选

DataLogger 独立持久化，火机 GestureManager 随场景销毁；销毁手势组件不再关闭整个研究会话。新火机场景加入已有 subject/condition/trial，MagicMR bootstrap 不再把 DataLogger 挂在整组 GestureDetectors 上。场景切换不会保存旧识别器去重复消费下一线路的输入。Park 场景已有禁用的 GestureManager 保留，检查对象是已销毁旧实例与当前无活动火机管理器，不把禁用组件误认为双输入。

实际保存场景 Play Mode：ParkPlant → BridgeStereoFusion → MagicMR → ParkPet，持续使用 SIM_SYNTHETIC_SESSION_CHECK、C3、trial 7 和同一 CSV；重复开启不覆盖会话，重复结束仅一条 session_end。另测独立 MagicMR 自动初始化 → ParkPlant，日志对象与场景识别组件分离。全部是合成验证，自动初始化的历史默认 ID 也不是参与者数据。

CSV 文件名用经过限制的片段、毫秒 UTC 和随机 ID，并以 CreateNew 创建；连续会话不会覆盖前次文件。身份原文保留在 CSV，逗号、引号和换行正确转义。Python 标准 CSV 解析器独立核对列数、身份、首尾事件、条件和原始换行。轨迹拒绝非有限位置，配置区间限制为 0.05–10 秒，非有限间隔回退 0.1 秒；同帧 100 次有效调用加一次 NaN 调用仅记一条轨迹。该上限控制写入频率，不等于完整日志容量配额。

CalibrationRitual 每帧更新桌面参考高度，已移除该 setter 的逐帧 Debug.Log；合法高度仍逐帧更新，非有限高度忽略。没有删除实验 CSV、模型或数据。六类内容条件矩阵 96 次动作、139 条动作结果回归，以及 14 个入口场景启动回归见归档；资源采样是每 5 秒主机快照，不是连续峰值。

初次夹具误写宠物场景名、将禁用组件当成活动消费、直接比较斜杠格式不同的路径，三次失败保留摘要。最终夹具检查真实 ParkPet 路径、isActiveAndEnabled 与规范化绝对路径。

边界：这轮没有验证真实相机、YOLO 命中、真实 XR 关节、PICO 双眼/性能或 Android 构建，也未发布新运行时代码。旧条件报告和语义审计保留历史源码哈希。仍需操作者会话 UI、完整选择/标定/追踪事件、带目标身份的轨迹、IO/磁盘满异常处理和容量上限；32 项 HCI 效果及设备闭环均不得因此标为完成。隔离工程仍含猫步态 v2 候选，与主工作区猫 FBX 不同。
