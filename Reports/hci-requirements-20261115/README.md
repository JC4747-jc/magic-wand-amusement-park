# HCI 魔法乐园需求差异审计

八用例 × 四维，共 32 项。以下是代码语义审查，不是通过清单；所有用例仍须完整编辑器、Android 与 PICO 验收。

需求来源：`pico-hci-yolo-vfx`，提交 `eae26868f857a7607a9fa2973fe9bc88c0260740` 的 `HciMeetingScenario.cs`；会议 PDF 原件未取得。逐项源文件行号与 SHA256 见 [audit.json](audit.json)。

| ID | 要求效果 | 当前发现 |
| --- | --- | --- |
| LighterDemon.A | 焦炭火 | Appearance 路由及烧焦材质存在；焦炭火持续表现须对照渲染复验。 |
| LighterDemon.B | 翅膀咳嗽烟 | Agency 有眼睛/生命化；当前 RealityEditor 未提供原分支翅膀与咳嗽烟的完整对应证据。 |
| LighterDemon.C | 躲闪 | Rule 躲闪与返回逻辑存在；本轮未重测 HCI 输入及实物锚点。 |
| LighterDemon.D | 爆炸净化开花 | Deconstruction 变花逻辑存在；爆炸净化、完整前后状态与实机待逐项复验。 |
| WhompingWillow.A | 黑暗风 | Pinch/Style 当前推进季节，没有对应黑暗风转换。 |
| WhompingWillow.B | 枝条抽打 | Circle/Awaken 设置 awake/scar；抽打 bend 位于 Wind，动作维度错位。 |
| WhompingWillow.C | 树疤呼吸 | Swipe/Wind 驱动枝条与花叶；树疤由 Awaken 开启，非 C 的独立反馈。 |
| WhompingWillow.D | 封印宝箱 | Reward 有 scar 前置、封印与宝箱可见性；需连贯故事动作与真实手势验收。 |
| PixelCritter.A | 8bit | 共用火机 Pinch 已路由 Style，合成轨迹通过；像素 shader 候选是否清晰达到 8bit 表现仍需验收。 |
| PixelCritter.B | 卡顿舞 | Circle/Awaken 有离散左右舞步和昆虫步态；需检查组合后是否清晰表达卡顿舞。 |
| PixelCritter.C | 眩晕 | Swipe/Wind 有 720 度旋转；眩晕姿态、恢复与可读性需实际对照。 |
| PixelCritter.D | 爆金币 | 共用火机 Fist-open 已路由 Reward，合成轨迹通过；金币表现与真实识别闭环仍需验收。 |
| HistoricStatue.A | 卡通 | Style 同时推进故事章节与通用材质样式，不能作为独立卡通外观维度验收。 |
| HistoricStatue.B | 伸懒腰 | Awaken 有扫描/苏醒及首章提示；当前石雕/牛首不能证明伸懒腰动作完整。 |
| HistoricStatue.C | 讲史 | Wind 显示 story 并可播放 storyAudio；当前文案标明原创幻想，不是已核实藏品历史。 |
| HistoricStatue.D | 荷花许愿树 | Reward 需要确认平面并显示 environment；荷花/许愿树形态与互动需逐项渲染验收。 |
| Bedroom.A | 水彩/赛博 | Style 选择水母/糖果雨，未实现独立水彩/赛博房间风格转换。 |
| Bedroom.B | 落地窗 | Circle/Awaken 唤出当前灯/糖果效果；portal 由 Pull 控制，B 落地窗映射错位。 |
| Bedroom.C | 水母灯 | 水母由 Awaken 控制，Swipe/Wind 不开启灯；C 映射错位。 |
| Bedroom.D | 糖果雨 | Reward 依赖已选 Candy 和确认天花板；有持续限量糖果雨，需在 HCI 流程复验。 |
| DeskPlant.A | 选中 | 本地 A 改为选中、不触发开花；已共用火机采样/仲裁并通过合成轨迹，真实输入待验收。 |
| DeskPlant.B | 拟人说话 | 本地 B 有标注虚拟身份的文字对话与口型；尚无音频语音及真手势验收。 |
| DeskPlant.C | 长大/幼苗 | 本地 C 切换长大/幼苗，叶片生长且花盆固定；共用火机横挥合成轨迹通过，真实输入待验收。 |
| DeskPlant.D | 去掉效果 | 本地 D 清除对话/开花/生长，保留选择和标定，可撤销；不是完整线路验收。 |
| MangaPet.A | 漫画描边 | Pinch/Style 选择梦境内容并驱动通用样式；明确的漫画描边尚需独立实现/验证。 |
| MangaPet.B | 梦境泡 | 本轮修复 Awaken 打开梦境且重复 B 不关闭；需绑定独立 Play Mode 报告，真实画圈仍未验收。 |
| MangaPet.C | 戳破 | 本轮修复 Wind 戳破梦境并拒绝空泡；保留直接触碰的头部定位门控，真实横挥仍未验收。 |
| MangaPet.D | 好感+1 | Reward 增加 affection、收起气泡并发出心形；本轮 Play 检查覆盖状态与撤销，不代替真手势。 |
| SeasonTree.A | 动漫季 | Style 当前推进季节，未独立表达 A 动漫风格切换。 |
| SeasonTree.B | 拟人 | Awaken 开启生命化状态，但通用 Eyes 只是节点，树表情几何与拟人表达仍需补齐验收。 |
| SeasonTree.C | 春夏秋冬 | 四季逻辑存在于 Style/Grow，Swipe/Wind 只触发风动花叶；C 映射错位。 |
| SeasonTree.D | 风吹落叶 | 自然树 Reward 会设置奖励并显示树的 reward；风吹落叶位于 Wind，D 映射错位。 |

六类 Park 已共用火机采样与四维识别规则，通过合成轨迹回归，见 [共享识别证据](../hci-shared-gestures-20261115/README.md)。公共缺口：真实 YOLO/XR 输入、各用例效果语义、实验条件门控和 DataLogger 记录仍需贯通；不能直接复制旧 Director 造成重复消费手势。HCI 指定的漫画/像素等转换必须保留，森林试作不计为用例完成。

宠物 B/C 的修复与验证单独记录在 [编辑器证据](../hci-pet-dream-20261115/README.md)；只证明其注明的编辑器范围，不将 A 描边、设备输入或全部八用例标为完成。
