# 开发接续记录

## GitHub整合源码验证补充（2026-11-15）

- Android托管脚本编译已补充通过：52程序集，最低采样空闲RAM5.091GiB、最大采样磁盘增量248.24MiB；尚未重建整合树IL2CPP/APK。历史129个Datasets目录只有元数据/标签，Git追踪图片0；先恢复并复核来源，不直接计入当前训练集。

- 认证并推送至 `codex/yolo-animation-integration-20261115`，现有[PR #1](https://github.com/JC4747-jc/magic-wand-amusement-park/pull/1)的base为 `restore-4d-lighter`；保留65条旧模型归档，未强推/合并或改主工作区分支和暂存区。
- 隔离整合树14场景审计、自然模型几何、旧6场景协程、6场景真实按钮/渲染流程通过；30张截图、0运行时错误。旧NUnit断言适配后6/6通过，产品代码未因测试调整。
- 首次导入新增磁盘限额和首次图形NUnit内存限额曾触发，均停止并保留记录；后续复用独立缓存，CPU回归无图形/禁Burst，界面回归保留实际图形。最后界面回归最低采样空闲RAM6.57GiB、最大采样新增磁盘0.98MiB。
- 报告、6张动作图、逐场景JSON及NUnit结果在 `Reports/github-delivery-20261115/combined-validation`。现有四份APK来自主工作区，不冒充本整合树重新构建；PICO实机、合格识别权重和完整训练覆盖仍未完成，Goal继续。


## 最新验证（2026-11-15）

- 青铜兽变体已完成本地接入和四包构建：Tina/Poly Haven CC0 Bull Head原始17,148面/1K PBR，保留UV/拓扑、新编固定底座两骨苏醒；Statue/BronzeBeast按种类加载，界面新增Marble statue/Bronze beast选择及Test awaken，Stop可用于雕像，不再整体伸缩。129个35cm拟合/37°朝向独立蒙皮采样底部误差2.98e-8m、动作7.47mm、接缝0，512次更新0分配；96帧离线视频解码/颜色/动作差异通过。初次空白离线预览改用显式CPU蒙皮快照后通过，失败基线保留；初次三场景回归发现Stop不支持雕像，修复后实际Bugs/Tree/Statue共13张截图通过。模型是通用青铜牛首表现，不是特定藏品复原。
- 最终 `bronze-final-{model-timeline,six-scene-flow,all14-startup}.log` 全模型/时间序列、六场景29按钮真实回调流程、14原场景启动0运行时错误通过。四份APK与40项源资源、精确7/1/1/14场景、CRC/签名/ARM64及SHA绑定通过，日志 `android-{main,pose,repair,allscenes}-bronze-final.log`、同名资源JSON、`apk-validation-bronze-final.log`；字节/SHA已更新计划表。最低采样空闲RAM3.280/5.523/5.369/3.233GiB，编译进程2/0/0/2（缓存0非冷编译证明）；无OOM，配额未提高。真实输入、识别、双眼和性能未验收。
- YOLO补充标注初筛完成：余114张全查RGB框图，78张需复核，6张另查原图；`TrainingData/openimages-exact-review-progress-v1.json` 保留逐图原框/作者许可/图像SHA与源清单SHA。确认同一扶手椅chair/couch重叠标签、猫/大沙发漏标、厨房台面误框和蜘蛛脚出框等；野生猫科不能直接算普通宠物域。原图撤回一张“玩偶猫”缩略图猜测，不能据此隔离。全部114张仍待完整训练标注接纳，没有新增下载或训练，没有把未审框目标当可靠背景。
- 训练日志写盘上限已落地：`Tools/yolo_training_logs.py` 接入两个真实训练入口，每次最多1MiB前缀+64KiB末尾，超额中段不持续写盘；保留最近两次成功日志，失败/运行中证据保留，目录8MiB满则停止新训练。只清理本工具记录的SUCCESS日志，不清模型/数据/指标/历史未登记文件。Windows和真实Ubuntu-24.04训练解释器各7项无模型测试通过，含大Unicode输出/异常尾部、限额、失败保留、路径越界和不覆盖。现有历史日志20份共99,825字节，因此本次没有删除它们；清理命令 `.venv/Scripts/python.exe Tools/yolo_training_logs.py --cleanup`。
- 资源复核：空闲RAM9.02GiB、显存5070MiB、C/D盘208.28/415.99GiB，任务重进程已退出，保留Hub后台与用户WSL会话；Linux交换空间当时0。PICO三个必需开关仍1，ADB无设备，未重启。远程五分支提交未变；Goal保持执行，下一步继续修正/扩充13类训练数据，正式识别训练/服务验收和真实头显仍未完成。下方均为历史阶段记录。
- 新增两类候选真实训练：`gap-candidate-20261115-a`，Cockroach/BronzeBeast，23张已逐图/框审核照片、19个来源/实物保守组，训练前冻结train17/valid3/test3，20epoch/340批次/334次首卷积优化器更新，参数确实改变且损失有限。两类AP50在极小测试集为0.28917/0.14214（仅2/1实例）；conf0.25、IoU0.5下3个目标全漏检，明确不通过部署。best/last各5,422,270字节，报告 `.codex-tmp/yolo-training/artifacts/gap-candidate-20261115-a/report.json`；Windows同SHA CPU重载/实图推理通过，但0检测不算识别成功。scene_profiles与初始权重不变，F2G五个关键依赖文件未变。原生测试AP使用conf0.001，不能用其recall1掩盖conf0.25的漏检；不据这3张测试图调参。其余11类训练与更充分数据仍必做。
- 训练沿用低资源配置，未和Unity/Blender/F2G重任务并行：416/batch1/workers0/cacheFalse/AMP、nano/AdamW/nbs1、128MiB运行目录/15分钟及RAM/VRAM/宿主磁盘门槛。PyTorch分配/保留峰值169.39/210MiB，最低采样宿主空闲3.498GiB、最大采样RSS2350.08MiB、Linux输出20.05MiB，45个回调/五秒样本不代表完整峰值。结束空闲RAM9.12GiB、显存5069MiB、C/D盘208.11/415.99GiB；WSL交换空间当时0，任务Python退出，保留既有Hub/WSL shell，未重启。ADB无设备，补独立真实采集时需要连接PICO/USB调试。
- 数据已推进：Open Images189张/51,460,437字节；新iNaturalist/Commons公开API元数据40条蟑螂/60条青铜器，只取16+13张审核图共12,264,924字节。29张逐一原图和框图检查，23个手绘框及6张非负样本隔离记录在 `TrainingData/public-gap-annotations-v1.json`；源作者/许可/来源链接/图像SHA、观察者日期/实物ID/复制品家族都保留。Open Images60张模糊类别已全部语义初筛：20张保留语义候选，40张隔离，仍须框完整性复核；精确类别15张抽样另发现人工圣诞树、未标注的明显大椅子。记录 `TrainingData/openimages-semantic-review-v1.json`；没有把所有候选直接作为训练数据。标注审核图已修复灰度JPEG被绘图默认色表伪彩显示的问题，现统一RGB，原照片未修改。
- 分组审查发现此前精确去重未覆盖的泄漏风险：`candidate-group-audit.json` 扫描355张/98,941,565字节，精确重复0、66个双哈希候选对。原Find lighter/detect的137张均为pico_frame编号系列，缺少独立session信息，按同一未解决来源组处理，横跨train97/valid26/test14；不能用先前“无精确跨split重复”证明独立验证。新两类冻结分组在 `TrainingData/gap-candidate-split-v1.json`，但极小公开照片测试不能证明PICO域准确率。后续须补打火机独立采集/其他来源，不能照搬旧划分宣称正式精度通过。
- 历史模型缺口已由顶部青铜兽版解决：原先BronzeBeast仅加载MarbleBust，现已有独立模型、虚拟选择及苏醒/扫描/Stop/Undo本地验证；识别权重与真实PICO验收仍未通过。
- 桌面退出引用修复/共享唤醒按钮版四包已全部完成，替代下方旧包记录：`.codex-tmp/android-{main,pose,repair,allscenes}-desktop-fix-final.log` / 资源JSON；`apk-validation-desktop-fix-final.log` 验证CRC/签名/ARM64、精确7/1/1/14场景、35项源资源清单与APK SHA绑定及猫狗步态名全部通过。最新SHA/字节见 `BRANCH_SCENE_PLAN.md` 表格与 `apk-validation.json`。全模型/场景时间序列及14原场景实际启动0错误通过：`desktop-fix-model-timeline.log` / `desktop-fix-all14-startup.log`。Main/Pose/Repair/AllScenes最低采样空闲RAM3.391/5.249/5.162/3.510GiB，原生编译进程最多2/0/0/2（0为缓存复用），离散采样不代表完整峰值，本轮无OOM。构建结束空闲RAM9.03GiB、显存5068MiB、C/D盘208.22/416.10GiB；任务Editor/Java/clang/Python退出，保留Hub后台及既有WSL shell，不强制关闭用户会话。ADB无设备，PICO handTracking/videoSeeThrough/portalInited仍为1，未自动重启；YOLO正式精度/部署和真实头显验收继续未完成。
- YOLO已完成首次真实GPU短训验证（尚非精度或部署验收）：`gpu-smoke-20261115-c`，YOLO26n固定初始权重、8张train/4张val、416输入、batch1/workers0/cacheFalse、2epoch、AMP、nbs1。共16批次，首个训练卷积参数确认改变，至少9次优化器状态更新；两epoch损失均有限，best/last各5,424,446字节，已保存、重载并推理。报告与候选在 `.codex-tmp/yolo-training/artifacts/gpu-smoke-20261115-c`，best SHA `22bcd5dc4df2293ce21e90d544ab53c0a591bc17a424ab1deac06de673acc018`。tiny-val的P/R/AP只是试跑指标，未触碰test集，`accepted_for_deployment=false`，原scene_profiles与初始权重不变。Windows CPU环境也通过同一SHA候选重载/实图推理（`windows-reload.json`）。A在GPU分配前因遥测对象名停止；B完成一epoch后因loss统计接口停止，均保留失败日志，不算通过；C修复回调后完整通过。
- 训练环境 `/opt/magicmr-yolo/venv` 使用独立拥有目录、只读引用已验证CUDA依赖，Python3.10.19/Torch2.4.0+cu124/Ultralytics8.4.212，禁写共享包字节码；CUDA反向传播与pip check通过，无新框架下载。`shared-base-integrity.json`确认训练前后F2G五个关键文件哈希不变。短训PyTorch分配/保留峰值169.24/210MiB，最低采样主机空闲3.737GiB、最大采样RSS2371.64MiB，Linux运行目录11.34MiB，7个回调/五秒采样不代表完整设备峰值。保留55%分配器上限、3GiB主机空闲/进程RSS门槛、128MiB运行输出上限、缓存关闭/仅best+last。后续工具又补充独立CUDA缓存128MiB上限、脚本快照/版本锁；这些补充是C之后的工具改进，尚未以新训练重跑，不冒充C的既有设置。所有训练Python退出；存在既有bash会话，因此没有强制终止用户WSL会话。
- 缺失类别数据准备已推进到有界样图审核：官方Open Images类别表12,011字节/601行，加两个官方validation元数据CSV共40,350,533字节，建立367张/10个候选类别索引。单线程仅下载26张JPEG共7,564,761字节，全部可解码且保留作者/许可/原网页/原框及SHA，未创建项目split或训练标签。Windows urllib初次证书失败未下载图片，保留失败manifest；系统Schannel证书链验证通过后下载，未关闭TLS验证。工具 `Tools/index_openimages_candidates.py` / `Tools/fetch_openimages_review_sample.py`，单图2MiB、图片总128MiB、15分钟上限。首6张Statue/PottedPlant实际图像与框已审核：地栽大黄不能当盆栽，盆/基底不可见特写与雕像脚部暂拒；两个人形雕像保留条件候选而非BronzeBeast。6张均未接受训练，其他20张待审。详见 `.codex-tmp/yolo-training/openimages-review/{manifest,semantic-review-first6}.json`；Cockroach/BronzeBeast仍需其他来源，不能把候选等同完整数据。

- YOLO训练数据初审已实际完成：`Tools/audit_yolo_training_data.py` 扫描5组Find lighter数据，共1,380个条目/170,019,992字节，0解码失败/缺标注/非法行，各组内部精确像素跨split重复为0；不同数据版本之间有重叠，不作为独立照片总数。只有Lighter类别，另外12个服务类别缺本地训练标注；原始多边形数据与5列detect框分开记录，merged_v3含旧D盘根路径，许可、近重复与采集组独立性尚待复核。详细报告 `.codex-tmp/yolo-training/data-audit.json`，仅10,937字节；本次未启动训练。
- 输入队列回归已通过：`desktop-queued-keyboard-focus.log` / `desktop-input/queued-keyboard-result.txt` 6次受控按键经实际Input System → ParkController.Update → Room唤醒/Z撤销/R复位及Tree T切模式/唤醒/Z撤销，定位根与原相机固定。最初批处理回归因焦点路由失败，按安装包InputManager规则，在测试内存副本中同时设置IgnoreFocus与GameView路由后通过，结束恢复；未修改产品输入设置。原生自动化2/T仍未验收，不能据队列成功宣称物理键盘或头显通过。退出引用修复、共享唤醒入口的源代码和六场景回归已通过；包含这些修复的四包重建仍待执行，下方四包是上一版。

- 用户已修订目标，要求开展YOLO模型训练；原“默认不训练”被取代，历史“未训练”条目仍是对应阶段事实。完整修订目标、实际识别类别、数据划分/独立验证/服务接入及RAM/VRAM/磁盘限额已写入 `BRANCH_SCENE_PLAN.md` 顶部。当前Goal工具不支持改运行中卡片正文，保持active并按用户最新指令执行。现已发现Find lighter数据及旧训练脚本的batch16/workers4与旧机器路径，先完成数据/环境审查和受限训练入口，再串行试训/正式训练；本条不是训练完成报告。

- 接续进行中：原生Unity窗口检查发现退出Play时已销毁ParkActor仍被控制器通过 `?.` 调用。真实失败日志 `unity-desktop-preview-baseline.log`；已构建树回归 `destroyed-built-target-red.log` 退出1，修复为Unity对象有效性判断后 `destroyed-built-target-green.log` 退出0，并覆盖销毁后的有效目标重选。房间/雕像复用Test awaken鼠标入口，植物补齐回放/物理目标门控；`desktop-awaken-six-scene.log` 六个实际保存场景/30帧通过。原生鼠标已在ParkPet保存场景的六内容菜单中实际操作，少量原始窗口JPG及SHA/来源说明在 `.codex-tmp/native-desktop/evidence.json`，不是相机Render或注入回调；修复版实际退出播放日志未见异常。原生2/T自动化按键未观察到动作，Game焦点/Keyboard启用已确认，128条上限诊断实际写9条/3,078字节；另做Input System队列回归定位，尚未把原生快捷键算作通过。下方四包仍是上一版，完成剩余输入检查后将重建包含本次修复的四包。

- 当前为虫/树虚拟变体及树皮标记版，四包全部构建/核验通过：`.codex-tmp/android-{main,pose,repair,allscenes}-variant-bark-final.log` 与资源JSON、`apk-validation-variant-bark-final.log`。签名/CRC/ARM64、精确7/1/1/14场景路径、35项BuildReport资源清单与APK SHA绑定、猫狗步态名均通过。Main/Pose/Repair/AllScenes最低采样空闲内存3.278/5.254/5.315/3.354GiB；Main/AllScenes实测原生编译最多2进程，Pose/Repair复用缓存；五秒离散采样不代表完整峰值。本轮无OOM，保留Java堆2GiB/元空间512MiB/CPU2/公共池1/Gradleworker1，独立签名核验另限Java512MiB。结束空闲内存9.06GiB、C/D空闲208.86/416.28GiB，任务Editor/Java/clang/Blender/WSL退出，仅既有Hub服务保留。ADB无设备，PICO三必需开关仍为1；未训练/未重启。远程五分支再次复核提交未变。当前SHA见计划表与 `apk-validation.json`，下方旧包为历史记录。goal保持执行，真实手势/识别/双眼/性能及缺失自定义权重保持未完成。
- 虚拟菜单已补齐Cockroach/Spider/Test defeat及Natural/Fantasy tree/Test awaken；只有当前虚拟内容显示对应行（Park共27个按钮，含隐藏变体），Reset后可直接重新生成变体。物理目标、实时/回放、拟合/表面确认及忙碌状态保持门控；树模式切换会中断动作、清除旧手动表面/Undo/特效，但保留手动位置/尺度/朝向。桌面T也走同一树模式接口；宠物Cat/Dog选择共用边界并保留原地步态功能。
- 树的红色球体占位已换成原创哑光褐色树皮标记，97顶点/160面、无贴图/动态光/额外更新组件，全部顶点射线贴合原CC0树干；独立最近三角面检查最大间距0.90002mm，随生长/拟合层级。触摸判定改为标记世界坐标，唤醒/安抚/Undo/切换和Reset清理通过。局部实际Unity图在 `.codex-tmp/tree-mark-review/Bark-EDITOR.png`，不是真实伤口判断。反馈最终收敛为至多六个暖色小光点；风吹的季节叶片/花瓣保留，最终真实场景复验 `variant-bark-motes-final.log` 已通过：要求六个活动反馈使用共享Mote网格且名义尺寸不超过9.1mm，保存8张场景图；尺寸仍受手动拟合缩放，未替代真实触摸或设备验收。
- `.codex-tmp/variant-bark-flow-final.log` 两个真实场景/8帧通过按钮回调、虫体受击/Undo、树模式中断/贴合保留、表面归属清理、安抚/Undo及门控；`variant-bark-model-timeline-final.log` 全部自然模型/14场景结构/六场景18帧通过，`variant-bark-six-scene-final.log` 六场景27按钮/30帧通过，`variant-bark-all14-final.log` 14个原场景启动0运行时错误。都是Editor/合成动作证据，不等同真实指针/手势/识别/双眼/性能。猫狗共用选择器的 `NaturalPetWalkPlayChecks.RunUIOnly` 回归亦通过（`variant-pet-ui-regression.log`）。

## 先前阶段记录（保留历史证据）

- 阅读角细化版四包已全部构建/核验：`.codex-tmp/android-{main,pose,repair,allscenes}-room-detail-final.log` 与资源JSON、`apk-validation-room-detail-final.log`。四包CRC/签名/ARM64、精确7/1/1/14场景、32项真实BuildReport资源/SHA绑定、猫狗步态名、家具作者/CC0与材质转换说明均通过；未接受宠物、错误UV2法线和未使用原始ARM未入包。最低采样空闲内存3.202/5.349/5.565/3.648GiB，Main/AllScenes实测最多2个原生编译进程，Pose/Repair复用缓存；五秒离散采样不代表完整峰值。本轮无OOM，保持Java堆2GiB/元空间512MiB/CPU2/公共池1/Gradleworker1。结束可用内存9.26GiB，C/D空闲209.12/416.28GiB，任务Editor/Java/clang/Blender/WSL退出，仅既有Hub后台服务保留；PICO三个必需开关仍为1，ADB无设备。未训练、未重启。远程五个分支再次只读复核、提交未变。goal保持执行，后续补虫类/树模式的虚拟变体入口并继续剩余验收；实机部分仍需连接PICO。
- ParkRoom 已细化为自然材质阅读角：官方CC0 Modern Arm Chair 01（Vibrant Nordic，原始8,916面）与Ceramic Vase 01（James Ray Cock，实际glTF10,296面），1K原图/UV/网格完整保留；两组官方清单合计3,116,489字节，逐文件大小/MD5/SHA校验，无高分辨率大包。新增分板地面、真实窗洞/窗框/窗台、薄地毯、书本封面/页块、圆边台面与金属底座；模型坐标下小倒角、连续灰泥UV及按尺寸投影木纹。全部静态部分24,204三角面/9个renderer，缓存共享且不增加动态光/刚体。ARM数值转为URP R金属/G遮蔽/A光滑度，同一张图供metal/AO使用，线性导入；原始ARM不作错误通道绑定，转换通道误差小于1.2e-7。来源、原SHA和修改说明随资源保存。
- `.codex-tmp/room-detail-lifecycle-validation.log` 通过模型/切线/非退化三角面、支撑底部与名义尺寸、实际窗洞射线、材质映射、14场景结构和六场景动画18帧；房间真实Play Mode10帧中家具固定，水母渐显/脉动/收起/Undo/模式切换、确认天花板下持续24颗糖果/移动锚点/Stop/Reset/丢失恢复/退出/音源均通过。`room-detail-six-scene-flow.log` 六个真实场景菜单/30帧通过，`room-detail-all14-startup.log` 全14原场景启动0运行时错误。首次导入材质映射丢失已分离贴图重导入与材质保存阶段；实际渲染还发现跨场景原生Mesh已卸载而托管字典仍有项，现按Unity对象有效性重建缓存，并在渲染前要求可见Mesh有效。失败和诊断日志保留，不把最初缺建筑的画面作为完成证据。
- 完整阅读角水母相位预览 `.codex-tmp/room-motion/ReadingCornerPulse.mp4` 已生成，640×640/30fps/84帧/2.8s；帧序列17,465,414字节，小于24MiB固定上限。本机单线程编码，与独立水母片段共同通过解码、PNG颜色/中帧变化检查，报告 `room-motion/movies-validation.json`。这是固定家具+离线shader相位，不含实际输入/世界漂浮或头显录像，实际Play Mode另有上述证据。四份APK及32项资产核验已通过（见最新条目）。完整阅读角视频189,547字节，平均首帧颜色误差1.622/255、中帧变化1.042/255，解码84帧。
- 接续可操作性审查发现：当前蜘蛛已有模型/步态与代码/识别选择路径，但虚拟界面仍默认蟑螂；树的自然/魔法模式目前只在桌面T键切换。后续补齐头显可操作的虚拟变体入口，并检查模式中断/复位，避免把测试脚本可调用等同于完整用户入口。房间空间细节的本地模型/效果工作已完成，实机空间、手势、透明排序及帧率仍未验收。
- 猫狗步态/虚拟入口版四包已完成构建与核验：日志 `.codex-tmp/android-{main,pose,repair,allscenes}-pet-walk-final.log`、资源JSON、`apk-validation-pet-walk-final.log`。四包均验证CRC、签名、ARM64、精确7/1/1/14场景路径、18项源资产/SHA绑定、猫犬署名及新增步态修改说明；还直接在player数据检出 `BicolorCat_Walk` 与 `Wolfhound_WalkSource` 两个动画名。最低采样空闲3.369/5.471/5.435/3.466GiB；Main/AllScenes最多2个原生编译进程，Pose/Repair复用缓存（样本0不算冷编译），本轮无OOM，Java配额不变。当前四包数据见下表/JSON；猫狗步态及入口本地验收已完成，完整14场景原始启动复验通过，0运行时错误，日志 `pet-walk-all14-startup-final.log`；保留原相机和输入组件，桌面缺少AR provider及PC服务的警告按原说明记录。空间细节及实机输入/双眼/性能等验收仍未完成，goal保持执行。
- 本轮结束空闲内存9.19GiB，C/D空闲209.26/416.28GiB；无任务D盘Editor/Java/clang/Blender/WSL重进程，Unity后台服务保留，PICO必需三开关仍为1，ADB无设备。未训练、未重启。复现：单线程Blender运行 `Tools/prepare_bicolor_cat.py`（需固定SHA原始GLB），Unity执行 `NaturalPetWalkPlayChecks.Run`；该Play Mode入口自行退出，批处理不要附加 `-quit`。四包用 `Tools/build_android_bounded.ps1` 顺序构建，再运行 `Tools/verify_android_artifacts.py --unity-data D:/Unity/Hub/Editor/6000.0.26f1/Editor/Data --include-all-scenes`。猫狗原地步态/入口本地工作完成；接续推进昆虫与空间细节、剩余场景验收及实机输入/头部/双眼/帧率，缺失识别权重继续显式未配置且默认不训练。
- 猫已补2s四拍原地慢走（原网格8,646面/40骨、毛色/眼睛/UV0法线不变）：`Tools/author_cat_gait.py` 在源骨架上做解析腿链求解、按骨骼权重选取真实脚掌表面并校正接触，不增加骨骼、不改权重、不训练。源Blender193个采样支撑误差0.166mm、循环顶点差0；Unity在0.35m手动拟合/37°转向下193采样最大支撑误差0.176mm、四足抬起约19.18mm、首尾差0，定位根固定。实际ParkPet的Preview Cat/Preview Dog/Walk in place按钮、Animator步态、回待机、喂食中断/忙碌拒绝切种、Undo/Stop/Reset、丢失/恢复/物理目标及live门控、退出/音源清理均通过；最终日志 `.codex-tmp/pet-walk-final-ui-idempotent.log`，5张原相机场景图。旧梦境/喂食生命周期另通过 `pet-walk-pet-lifecycle.log`。重复生成曾使犬控制器累积旧行走状态，已修复生成器状态复用和清理；初次回归失败日志保留。新按钮仅在虚拟Pet模式显示，其他场景菜单保持原尺寸；步态是原地预览，不推断真实宠物行走。14场景结构/自然模型与六场景实际动画18帧通过 `pet-walk-all-model-timeline.log`；六个实际场景界面/30帧通过 `pet-walk-six-scene-flow.log`。进一步的实际Animator启停过渡100次离散采样未见穿地，`pet-walk-actual-transition-ground.log`；喂食打断从已经播放的Walk状态验证。本轮四包更新/动画入包核验已通过（见最新条目）。
- 四份小型动作预览已更新：`.codex-tmp/pet-motion/CatIdle.mp4`、`CatSniff.mp4`、`CatWalk.mp4`、`DogWalk.mp4`；640×640/24fps，分别60/60/48/26帧（2.5/2.5/2/约1.083s），59,793/64,606/77,849/81,782字节。单线程本机编码、逐帧解码及PNG颜色/中帧变化通过 `pet-motion/movies-validation.json`；四序列合计约21.10MiB。离线直接clip采样/显式蒙皮渲染，不是实时头显录像，实际Play Mode证据另列。四拍时序参考成年猫的同侧后足先于前足的行走序列（https://journals.physiology.org/doi/full/10.1152/jn.00240.2026），不是实验数据或运动捕捉复原。重新只读复核全部远程分支，仍为已审查的五个refs且提交未变，未重复下载。
- 猎狼犬接入后的四包已完整构建/核验通过：`.codex-tmp/android-{main,pose,repair,allscenes}-natural-dog-final.log`、同名资源JSON、`apk-validation-natural-dog-final.log`。CRC/签名/ARM64、精确7/1/1/14场景路径、猫犬许可署名、18项真实BuildReport源资产与APK SHA绑定均通过；未接受獒犬/Innerscene候选和错误UV2法线未入包。原生编译最多2个（Main/AllScenes实测；Pose/Repair复用缓存，样本0不算冷编译证明），最低采样空闲3.206/5.743/5.751/3.447GiB；仍是5秒离散采样，不称完整峰值。实际Gradle守卫核对CPU2/公共池1/2GiB堆/512MiB元空间。本轮无JVM OOM；首次批量调用在主包实际成功后误读残留LASTEXITCODE而停止，已修正调用层，只续建后三包，没有重复重建成功主包。结束时空闲内存9.17GiB、C/D空闲209.28/416.28GiB；任务Editor/Java/clang/Blender/WSL均退出，仅既有Hub服务保留，PICO三开关仍为1，ADB无设备。未训练、未重启；猫步态/产品行走入口、空间细节、缺失识别权重和实机头部/双眼/性能验收仍待完成，goal继续执行。
- 猎狼犬已替换正式 Dog 的临时球体模型：Wildfire Games / 0 A.D. CC BY-SA 3.0，原始849面/47骨/256px毛色，源待机/观察/原地行走及新编2.5s嗅闻；新增 Generic 导入器、URP 材质、控制器和 Resources prefab，幻想气泡绑定真实 Head 骨。仅修改狗资产适用同许可，来源、固定提交、逐文件SHA与修改说明/包内署名保留。Unity 初次实际渲染发现相反前向的 FromToRotation 选轴会倒置模型，改为仅绕Y转向并新增头高于身体断言；不是更改跟踪根来遮蔽错误。`.codex-tmp/natural-dog-review-final.log` 验证真实Play Mode 15个动作帧/3视角、嗅闻自动回待机/Reset、原地源步态；正式猫狗生命周期、两个头部气泡、两种嗅闻及门控/Undo/丢失恢复/退出通过 `natural-dog-integration-final.log`，10帧。五个独立蒙皮拟合姿态脚部位移0、最大皮肤位移12.717mm、头骨旋转8.292°，37°转向和0.35m拟合底部通过；静态拟合直接采样源clip，实际Animator另由Play Mode检验。原始头骨轴心不平移，不以轴心位移代替动作证据。最初失败日志均保留。14场景结构/全部自然模型与六场景真实动画18帧通过 `natural-dog-model-timeline-final.log`，六个实际场景界面流程/30帧通过 `natural-dog-six-scene-flow-final.log`；四份APK已完成构建与18项资产核验（见最新条目）。猫步态/实机定位与性能仍未完成，goal保持执行中。
- Unity固定补丁6000.0.33f1已独立安装到D盘，当前项目/受限构建默认入口已更新；旧6000.0.26f1及其共享SDK/NDK/JDK保留。独立ARM64冷探针完整通过，最多2个clang、最低采样空闲7.488GiB。真实主线冷编译也验证2个clang。随后定位AGP8.3.0 APK压缩使用独立ForkJoin公共池，单Gradleworker未覆盖；加入Java CPU2/公共池1，维持堆2GiB/元空间512MiB，并在Gradle运行时断言实际配额。四份完整APK成功，最低采样空闲5.416/5.496/5.425/2.633GiB（前三包复用原生缓存，AllScenesRelease实际最多2个clang）；四包CRC/签名/ARM64/精确场景路径/署名/16项BuildReport源资产清单及APK SHA绑定均通过，最终 `.codex-tmp/apk-validation.json`。先前OOM和额外CMake配置失败仍保留，不算通过。补丁版全部14场景原始启动、六场景界面/时间序列、旧打火机/捕捉/VST检查均通过，日志/范围/最终SHA见计划顶部及表格。
- 此前候选阶段的Wildfire Games狗资源已有明确CC BY-SA3.0许可、固定提交及源文件SHA，单线程Blender渲染三视角。獒犬因战斗刺项圈不采用；猎狼犬849面/47骨、256px原图及原始待机/行走已转换到候选FBX，并增加2.5s小幅嗅闻；五个Blender变形姿态中56个脚部顶点位移0，头骨尾端最大位移约3.19mm，根不动。当时尚未替换Resources/正式Dog；此历史候选状态已由顶部本地接入/验证结果取代。不是概念图、也未计为完成交付；来源与候选状态见 `Assets/ThirdParty/NaturalPets/Wolfhound`。
- 清理本任务两个已验证安装器缓存，回收约4.20GiB；未删除旧编辑器/共享SDK及原有Blender/WSL安装文件。结束空闲内存9.23GiB、C/D空闲209.56/416.28GiB，任务重进程均已退出，PICO开关保持1、ADB无设备，未训练/重启。goal保持执行中，优先正式接入Dog、继续猫步态与设备缺口。
- 实际 Park 六场景界面流程通过（0条运行时错误）：复现并修复虚拟模式切内容后预览空白，以及回放转虚拟时两种模式状态冲突；虚拟对象向画面内移，避免树冠裁切。退出缩小时保留手动拟合的位置/大小，表面清理不再提前重置同一目标。`ParkSceneFlowChecks.Run` 保留实际相机/输入组件，用真实按钮回调、公开动作 API 与合成 Fit/Plane 点验证；最终日志 `.codex-tmp/park-actual-scene-flow-surface-final.log`，`park-scene-flow` 保存六份同运行ID报告和30张截图（约1.90MiB）。回放候选/忙碌门控、Stop/Reset/切内容、回放→虚拟→真实、退出拟合连续性与目标/音源/表面清理均通过；不是实际鼠标/手势/定位输入验证。Android 更新状态见 `BRANCH_SCENE_PLAN.md` 顶部。
- 四份流程修复版APK全部构建/核验通过，日志 `.codex-tmp/android-{main,pose,repair,allscenes}-actual-scene-flow-final.log`；主线/姿态/修复/14场景验证包分别77,131,826 / 77,078,896 / 77,078,788 / 67,981,192字节，SHA及7/1/1/14精确场景路径、CRC/签名/ARM64/署名/16源资产清单见 `apk-validation.json` 与计划表。本轮无JVM OOM，仍未训练或重启。
- 资源实测纠正：当前6000.0.26f1的Bee原生C++编译曾出现16个clang子进程，现有Unity workers=2和IL2CPP jobs=2不是原生限流成功的证据。AllScenes某次合计工作集约2.535GiB、空闲内存4.30GiB；未完整采样，不称峰值。官方6000.0.33f1才记录向Bee后台扩展BEE_BUILD_THREADS，链接/后续配置缺口见计划顶部；没有升级或替换安装文件。结束空闲9.20GiB、C/D空闲211.19/425.56GiB，任务重进程已退出，ADB仍无设备。原生编译限流和持续资源采样仍待完善。
- 旧入口新增实际启动与原目标动作证据。审查清单原先仅筛选Assembly-CSharp，遗漏MagicMR独立程序集，现按项目脚本路径收集。BridgeTest/MagicMR换用已有真实比例详细打火机且保留根姿态/名义尺寸，继承的旧默认IP迁到本机；VstTest挂接已有透明相机/重试入口。最终14个实际项目场景未经隔离逐一启动/原相机截图，0条错误：`.codex-tmp/all14-actual-startup-final.log` 和 `legacy-startup`。未连接PC服务的警告、桌面AR缺失provider的4条警告明确保留。可选图像空缺发送器状态NOT_CONFIGURED，设备相机在桌面UnsupportedPlatform且无帧；未召唤的目标仍受门控，Park入口等待操作时无Actor，不把启动当识别/交互通过。
- 五个实际场景原打火机各7张状态图，碳化/脉动眼部/躲闪返回/三倍放大内部结构/变花/取消复位通过；日志 `.codex-tmp/legacy-model-animation-startup-validation.log`。模型审查明确隔离定位输入并打开未召唤渲染遮罩，只证明原模型与核心动作，不是跟踪/手势通过。修复动作音源、碎解爆发和花朵在Reset/换维度/销毁时的归属清理，等待原延迟后无迟到花朵。
- 两个实际捕捉场景的非空卡牌、重复拒绝、中途Reset/复用、timeScale=0仍移动/完成、禁用清理通过；背包始终可见固定。旧Sample卡牌直接挂共享Canvas，已修复整Canvas/背包一起移动隐藏的问题；专用截图相机改为启动解析、仅捕捉Render。日志 `.codex-tmp/legacy-card-layout-final-validation.log`，状态图/卡牌与JSON在 `legacy-animation`。VST仅验证实际入口和alpha/后处理配置，AR/原生透视/真实手势/设备性能仍待验证。
- ParkRoom 改用原创薄膜水母灯，5,730 顶点/9,248 面、一个材质/renderer；短缘触手与四条褶皱口腕、内侧马蹄形结构分开建模，参考水族馆资料但不是物种科学复原或照片级扫描。GPU 收缩/摆动，512 次参数更新 0 托管分配，无贴图、骨骼、动态灯或训练。家具在唤醒时保持固定；反馈改为六个小光点。糖果现起于确认天花板下2cm，最多下落约31cm，保持32项池。最终 `.codex-tmp/natural-room-loop-warm-final-validation.log` 通过14场景结构、六场景模型/界面检查、实际Room渐显/收起/Undo/重启/切换、天花板门控、24粒子持续3s且低于/跟随锚点、Reset/丢失/恢复/退出（10帧），随后六场景时间序列18帧通过。加载/首次截图曾产生1.7s/386ms长帧，失败日志保留；现预热截图后再等待五个新的稳定帧，仍保留固定0.38s的渐显及Stop中间帧要求。
- 修复水母初版分数频率在2π回绕处跳变，实际GPU渲染在周期前后±0.1ms平均RGB差约0.0105/255，见 `natural-jellyfish-review/loop-validation.txt`。最终 `.codex-tmp/room-motion/JellyfishPulse.mp4` 为640×640、30fps、84帧/2.8s、237,014字节；帧序列约4.81MiB，单线程编码，解码84帧，首帧平均颜色误差约1.34/255，中帧差异约2.99/255。它是离线shader采样，不是头显录像；实机透明排序/过绘制/帧率及真实房间/手势仍待验收。重新 `git ls-remote --heads origin` 确认仍为五个已审查分支，提交未变，未重新下载大仓库。
- 自然 Cat 已接入正式 ParkPet：恢复源网格/眼睛，修复源法线 UV2 被 URP/Lit 误当 UV0 的问题，用单线程 Blender 重投影为 1K UV0；保留原 albedo、8,646 面、40 骨骼与来源许可，新编待机/嗅闻，胡须随头骨。三视角、五帧待机及五帧低头/恢复通过。独立蒙皮的五个拟合姿态保持底部（最大偏差约 0.000313mm）和 37° 转向的跟踪根。猫/狗梦境食物/星星/爱心、合成头部门控、喂食/Undo/Poke/Reward/Stop/Reset/丢失/恢复/退出真实 Play Mode 通过，保存十帧。修复仅有可靠标记却无头锚点仍能喂食、跟踪丢失保留旧头定位的问题。14 场景及六场景模型回归通过：`.codex-tmp/natural-pet-final-review.log`。Dog 仍为临时模型，猫自然步态与实机头部定位未完成。
- ParkBugs 已接入原创六足/八足骨骼模型：8,652/8,744 三角面，单主体 renderer，27/25 骨骼；改进甲壳、翅脉、触角、足刺、斑纹及细毛。不是扫描或照片级物种识别模型，仍可继续表面打磨。没有下载受限候选、复制照片或训练。独立蒙皮参考验证足部与地面，脚踝最大误差小于 0.07mm；连续 256 次姿态更新 0 托管分配，手动底部/高度拟合在 0.5mm 内一致。修复受击时网格/骨架缩放及 Undo 立即恢复。最终统一日志 `.codex-tmp/natural-insect-final-placement-timeline.log` 中，14 场景、两个昆虫的完整 Play Mode 生命周期（14 帧）及六场景 Play Mode 回归（18 帧）均通过。新增两份 2s/24fps/640×640 离线 Unity 步态视频，解码帧数与画面变化验证通过，路径 `.codex-tmp/insect-motion/Cockroach.mp4`、`Spider.mp4`；它们不等同头显录像，运行时另有 Play Mode 证据。
- ParkTree 已替换为 CC0 自然小树，74,999 三角面、三个主体 renderer、六骨骼、1K 原始图。叶片改为保留原始分布的遮罩卡片，避免整体减面后只剩细点；风动固定树干/定位锚点，生长固定底部，季节与 Undo/Reset 接入。最终 UV 版本的 121,000 个叶片顶点变形、14 场景和六场景 Play Mode 检查通过，日志 `.codex-tmp/natural-tree-final-validation.log`。移动端着色器编译和三个 APK 构建/产物检查通过。PICO 双眼、卡片闪烁/过绘制和帧率尚未验证，不据桌面结果宣称通过。
- 视觉按用户要求继续转为自然比例、真实材质。ParkPlant 已替换为 CC0 十二卷（8,929 三角面、40 叶骨骼、两主体 renderer、1K 原始贴图），实际蒙皮 2,241 个顶点有效变形，盆土在生长/摆动时固定；虚拟花改为细长花茎与六朵小白花。ParkRoom 加入 CC0 木纹、蓝灰亚麻、灰泥颜色/法线与家具倒角，六张贴图共约 2.03MiB。来源、哈希和修改说明已随资源保存。房间细化、昆虫/宠物视觉、完整交互与实机验证仍未全部完成。
- 五个远程分支已审查；主线快照、4d/restore 的六场景与新版协议手工整合，旧 HCI 的重叠用例和资源层有明确采用/保留说明，见 `BRANCH_SCENE_PLAN.md`。
- PC 多场景与通信回归 51 项通过；真实 v1/v2 CPU 服务 11 次请求通过。树、虫、雕像仍没有合格识别权重；房间/盆栽/宠物公开基础权重保留 PICO_PENDING，不训练。
- 14 个项目场景缺失脚本 0、未赋材质槽 0；六个 Park 场景实际 Play Mode 时间序列与清理通过，输出 18 帧。石雕采用明确 CC0 的真实比例/石材模型；宠物候选仍需视觉完善，技术通过不等于视觉通过。
- Main/Pose/Repair 最新包已包含旧入口/捕捉/资源清理修复及全部既有自然资产，分别77,127,794 / 77,074,900 / 77,074,728字节，日志 `.codex-tmp/android-{main,pose,repair}-legacy-validation-final.log`。新增独立`AllScenesValidation.apk`（67,978,188字节，`com.yn.picmagicmr.validation`，14场景，Release编译/打包验证，无新增导航流程），日志`android-allscenes-fresh-release-final.log`。四包CRC、签名、ARM64、包名、署名、16项SHA绑定资产和APK全局设置中精确7/1/1/14场景路径均通过，报告 `.codex-tmp/apk-validation.json`；不能替代实机通过。`build_android_bounded.ps1 -Variant AllScenes` 与 `verify_android_artifacts.py --include-all-scenes` 可复现新增覆盖。清理生成旧APK同时支持debug/release叶文件，均不清理编译缓存或Builds正式产物；本轮四包未出现OOM。检查时内存空闲约9.29GiB，C/D空闲211.2/425.6GiB，任务重进程已退出，未训练/重启，ADB无设备。
- 本轮首次Repair即使单worker仍发生 `packageDebug/IncrementalSplitterRunnable` OOM，保留 `.codex-tmp/android-repair-natural-room.log`。Unity退出后的独立Gradle诊断（`android-repair-room-gradle-diagnostic.log`）及完整Unity全新打包（`android-repair-natural-room-fresh-package.log`）在相同配额成功，具体根因未完全隔离。当前构建回调先校验固定项目Library生成目录，仅移除其可再生成的旧 `launcher-debug.apk`，保留编译缓存与Builds正式产物，并启用错误栈；没有提高2GiB堆/512MiB元空间/单Gradleworker。最终三包均记录清理后成功。结束时可用内存约9.34GiB，C/D空闲212.3/425.6GiB，无任务Editor/Java/Blender/WSL残留，只有既有Hub服务；未训练、未重启，ADB无设备。
- 新增猫待机/低头视频 `.codex-tmp/pet-motion/CatIdle.mp4`、`CatSniff.mp4`，640×640、24fps、各60帧/2.5s，60,437 / 65,382 字节。`NaturalPetMotionFrames.Run` 用显式蒙皮顶点/法线/切线快照离线采样；不是 Play Mode/头显录像。预览修整地面裁剪边缘和接触阴影，最终日志 `.codex-tmp/natural-pet-motion-frames-final.log`。单线程本地编码，逐帧解码均为60，首帧颜色与中帧变化检查通过。真实 Play Mode 证据另见首条日志。本轮检查时可用内存约9.37GiB、C/D空闲212.3/425.6GiB，WSL/Blender/任务Editor已退出；保留既有 Unity Hub 服务。ADB无设备，未训练、未重启。
- 修复版首次两 Gradle worker 打包遇到 JVM OOM；没有提高内存上限。退出 Unity 后单 worker 诊断通过，Unity 内单 worker 整包及移除可再生成输出后的全新打包均通过。当前默认 Gradle worker=1、堆2GiB、元空间512MiB，Unity/IL2CPP 并发2；主线/姿态当次以两 Gradle worker 通过。首次失败日志保留，细分根因尚未完全隔离。入口 `Tools/build_android_bounded.ps1` 保留内存/磁盘预检与本机 Java socket 修复。
- **Linux F2G 环境已安装并实际验证**：D 盘 Ubuntu24.04 WSL2，独立 Python3.10.19、torch2.4.0+cu124、torchvision0.19.0+cu124、CUDA nvcc12.4、gcc12；源码固定原提交，pointnet2_ops/chamfer 单线程编译，传递依赖按官方锁文件收敛，`pip check` 无冲突。
- F2G 官方 15 项测试通过；三份模型权重均校验固定 SHA-256，CUDA 内核探针通过。`pose_preflight.py` Linux 报告 `live_ready=true`。真实 v6 YOLO/SAM/F2G 使用官方 ROPE 样例联合预热，末次约 1,388.6/477.5ms，峰值 allocated 4,381MiB；5006/5007 就绪及检测权重身份验证通过。`Tools/smoke_f2g_windows.py` 进一步确认 Windows 生产 PoseAvailability 到 WSL 的健康端口与图像端口均连通，Windows 端两个 ready 标志为 true。证据 `.codex-tmp/f2g-runtime-smoke/`、`pose-preflight-linux.json`、`f2g-cross-windows.json`。没有训练，不代表 PICO 原生双目姿态精度或真实端到端帧龄已通过。
- 首次联合探针在图像端口误用了换行 JSON，已经改为生产协议的 4 字节小端长度前缀并重跑通过；不把初次失败探针算作通过。测试结束只关闭自己启动的 Linux 服务；随后停止专用 WSL 释放内存，再运行 Unity 模型审查，不把两类重任务并行。
- ADB 上次查询无设备，已通知用户连接 PICO 并开启 USB 调试；尚无安装、原生相机/手势或设备性能验收结论。
- 新增桌面 Reset 按钮 / R 键，修复捕捉中断、重复请求、目标切换及暂停时间时动画不结束；截图相机按目标包围盒构图、仅捕捉时渲染。实际场景检查含非空截图、完整目标构图和按钮回调，通过 `.codex-tmp/integrated-capture-validation.log`。
- 主线录制接入精确已发送的采集时间戳，录制硬上限 60 组，默认关闭；越界上限回归通过。默认 Build/Build And Run 改为主线入口优先，六个 Park 场景各启用一次。花朵重建复用既有部件，连续生成两次保留六个部件 fileID；避免每次构建改变外部引用。
- 双色猫的早期缩眼候选曾不接受；当前以首条记录的 UV 修复版本为准。Innerscene 猫仍因脚掌连接及简化毛色未接受。Grab3D 11.2MiB 候选通过 Windows/WSL 保留证书检查的下载均遭 TLS 中断，未导入、未绕过证书校验；WSL 已停止释放内存。

## 当前机器环境配置（2026-11-14）

- PICO Integration SDK 3.4.0 已安装到 `D:/Unity/PICO`，固定官方标签 `release_3.4.0`、提交 `e0740bf309b26a0b9adae33c506514569bfe5d38`。具有项目使用的相机、标定和取图 API，Unity 桌面及 Android 目标脚本编译已通过。
- Unity Hub 的安装目录已设为 `D:/Unity/Hub/Editor`；6000.0.26f1 与 Android SDK/NDK、OpenJDK 17.0.9+9 均已安装完成。ADB 与 java 已验证可执行。首次导入、桌面及 Android 目标集成检查、F2G 几何检查与特效渲染检查均通过；详见 BRANCH_SCENE_PLAN.md。
- Windows 的 WSL 所需功能已启用；早期结果曾要求重启，但本次实际内核/GPU探针与模型验证已通过，当前以实际结果为准。本代理没有自动重启。WSL3.0.5、D 盘 Ubuntu24.04 和真实 F2G 环境已配置。
- 用户级 `.wslconfig` 已配置内存上限 8GB、4 个处理器、2GB 交换空间放到 `D:/Unity/WSL/swap.vhdx`，启用渐进内存回收；实际 Linux 探针已确认约 7,944MiB 内存和 2,048MiB swap。
- F2G 安装脚本改为从自身位置定位项目；编译并发为 1，pip 下载缓存关闭，安装前要求 Linux 文件系统至少 20 GiB 空闲。训练仍未启动。
- `.gitattributes` 为 shell 文件指定 LF，两个 WSL 入口的 CRLF 已修正；Bash 语法与真实安装脚本已在 WSL 执行。

后续直接使用已安装的 D 盘 WSL，不重复安装或下载依赖。模型服务与 Unity 大型编译/渲染分阶段运行；真实旋转仍需头显双目采集、标定与帧龄验收。

## 基线

- 工作分支：`codex/mr-development-snapshot-20261109`，快照提交 `8359a0c`。
- Unity 6000.0.26f1 / URP；默认 `BridgeStereoFusion` 为双目定位、三帧视觉平均与手势施法。
- `Assets/Scripts/Perception/`：识别、通信与追踪；`Assets/Scripts/MagicMR/`：手势和物体效果；`Assets/Editor/`：构建与合成检查。
- `object-detection-demo/`：PC YOLO、F2G 桥接及通信回归；`Tools/`：数据与训练工具。
- 特效细化尚需头显画质及性能验收；F2G 旋转仍属实验路径。历史构建记录不代表此快照已通过验收。

## 当前修复与验证

- 健康回复现在要求完整换行帧和 JSON 对象，避免合法 JSON 数组、标量或空值导致监控线程退出；无效回复关闭可用状态，并在下一次有效回复恢复。
- `test_pose_availability.py`：4 项通过，含 7 类异常回复后的恢复测试。
- 沙箱内本机 TCP 测试超时；沙箱外原有健康检查通过。不能据此认定历史超时全部是环境问题。
- 项目 `.venv` 依赖安装完成；整合多场景后全量 `unittest discover -s object-detection-demo -v` 在允许本机 TCP 的环境中 **51 项通过**。测试使用显式模型替身；不证明模型识别精度。
- 真实部署 v6 权重成功加载，SHA-256 为 `999bca4f5bf96aeed3ac0f1047e31530e4eb4584873c85dd5bd00031fc71fb3a`；对 640×480 全黑图的 CPU 推理返回 1 个结果、0 个检测。该项只证明当前依赖下模型可执行。
- `Tools/smoke_position_service.py` 已扩展为 11 次真实 CPU 请求，验证 v1/v2 同连接切换、房间/盆栽/宠物公开基础权重、两个 discovery 源、三个缺失模型回复及新会话帧 1。全部通过，黑图无检测、姿态不可用；不证明准确率。拒绝占用已有 5005 服务，退出只关闭自己启动的进程；日志及回复在 `.codex-tmp/position-smoke/`。
- 当前隔离环境：Python 3.12.14、torch 2.15.0+cpu、ultralytics 8.4.212、opencv-python 5.0.0.93。未安装 GPU 训练环境。
- `Tools/run_pose_transport_checks.py` 新增 `--unity-data` / `UNITY_EDITOR_DATA`；使用已安装 Unity 内的 .NET 6.0.21 完成生产 C# 通信代码编译和故障回归，全部通过。
- `pose_preflight.py` 实测 `live_ready=false`：缺少 Linux F2G 运行环境、f2g_pose / pointnet2_ops / segment_anything、CUDA PyTorch、F2G / SAM / RADIO 权重与 RADIO 仓库。报告位于本机 `.codex-tmp/pose-preflight.json`，不提交机器路径报告。

## 执行 goal

本节按用户最新要求修订：**YOLO 模型训练是必须完成的工作，原“默认不训练”不再适用。** 完整目标与逐项验收以 `BRANCH_SCENE_PLAN.md` 顶部为准；现有 Goal 卡片仍为 active，当前工具只支持修改状态，不能直接修改卡片正文。

1. 继续整合有效分支成果，完成各场景模型、动画及交互，保持 PC/F2G 通信、Unity 回归与四份 Android 产物的可复现验证。
2. 审核并补足全部 13 个识别类别的数据：Lighter、Tree、Cockroach、Spider、Statue、BronzeBeast、chair、couch、bed、dining table、PottedPlant、Cat、Dog。记录来源/许可、完整实例框和采集分组，排除跨 split 泄漏；训练前冻结独立测试集。
3. 在隔离 GPU 环境进行真实 YOLO 训练，保存初始权重及输出 SHA、固定参数、优化器实际更新、损失和资源记录。仅打火机试跑、仅两类候选或公共预训练模型推理均不算完成。
4. 独立测试并记录逐类 precision/recall、mAP、混淆矩阵、误检/漏检及推理耗时；合格后接入 scene_profiles、类别映射与共享检测器，复查协议、模型身份和各场景。当前两类候选在 conf0.25 下 3 个测试目标全漏检，保持不部署，不据已揭示的测试集调参。
5. 补独立真实 PICO 采集并完成手势、双眼、姿态、稳定性及设备性能验收；需要头显时通知用户。全部场景的训练、服务接入及设备验收未完成前，Goal 不标记完成。

## 可复现检查与待验收

从仓库根目录执行：

```powershell
.venv/Scripts/python.exe -m unittest discover -s object-detection-demo -v
.venv/Scripts/python.exe Tools/smoke_position_service.py
.venv/Scripts/python.exe object-detection-demo/pose_preflight.py --output .codex-tmp/pose-preflight.json
.venv/Scripts/python.exe Tools/run_pose_transport_checks.py --unity-data '你的 Unity/Editor/Data'
```

第一、二、四项已通过。第三项若使用 Windows CPU 解释器，会列出该解释器缺少 Linux/CUDA 的阻塞；不能替代已经验证的独立 Linux F2G 环境。第四项 Unity 数据目录为 `D:/Unity/Hub/Editor/6000.0.26f1/Editor/Data`。Windows 到真实 Linux 服务的复现命令是 `.venv/Scripts/python.exe Tools/smoke_f2g_windows.py`，需要空闲 5006/5007 端口，会加载真实模型与官方样例并关闭自己的服务，不打开摄像头或训练。
Unity 环境准备后需运行 `StereoIntegrationChecks`、`VisualAverageChecks`、`TabletopGestureChecks`、`MagicMRVfxPreview.Run` 及 F2G 几何检查，保存日志，再构建 APK。设备验收覆盖多次复位→直立实物标定→点赞召唤、头部移动叠加、遮挡恢复、断线重连；特效还需双眼一致性、可见性、遮挡和帧率。姿态实验需单独验证旋转方向、稳定性与端到端帧龄，不能以通信或官方样例结果替代。

## 训练资源约束

YOLO 训练已授权且为必做项。Unity 构建、Blender、F2G 推理和训练串行，安装禁用 pip 下载缓存；当前构建完成前不启动另一项重任务。
本机实测物理内存约 15.74 GiB、检查时可用约 9.4 GiB；RTX 4050 Laptop 显存 6141 MiB。历史 8 GiB 显存机器的参数与峰值不适用于此机器。
开始训练前重新检查实际输出盘余量、物理内存、GPU 显存及已有任务；本轮结束时可用物理内存约 9.39GiB、C 盘约 212.4GiB、D 盘约 425.6GiB，这是当时读数，不是峰值报告或后续启动训练的永久授权条件。
训练前先评估数据及解压大小，禁止无界数据复制；nano 模型从 batch=1、workers=0、416 输入和 AMP 开始，关闭 RAM/磁盘图像缓存、自动批量扩张及可选遥测。现有候选运行限制为每次 15 分钟、运行目录 128MiB、最多 1,000 文件、仅必要 best/last；当前脚本允许 2–40 epoch，不进行无界参数搜索。正式扩大训练前须按测量结果制定有界预算，不能默默解除限额。
沿用运行守卫：宿主空闲 RAM 至少 3GiB、训练进程 RSS 至多 3GiB、GPU 全局空闲至少 1GiB（启动前至少 4GiB），PyTorch 分配器比例 0.55；C/D/训练 Linux 文件系统分别至少保留 20/25/8GiB。五秒及回调采样有观测间隔，不能称连续峰值或绝对硬限。输出写入已授权且空间充足的目录，不靠持续交换内存或重复写入大型检查点推进训练。
日志另有写入预算：两个训练脚本直接运行即使用有界日志入口，保存1MiB开头和最终64KiB，超额输出只更新内存尾部。`.codex-tmp/yolo-training/run-logs` 总预算8MiB；仅删除本工具确认成功且超过保留数量的日志，保留失败证据与简明回执。预算不足则停止新训练；不会反复压缩/搬移大型VHD或删除数据、权重。每次日志实际写入上限不含训练指标/模型文件，后两者仍由128MiB运行目录和epoch/检查点预算控制。
空间或内存不足时停止并调整，不无限重试；不自动删除原始数据、模型或既有实验。任何软件策略都不能保证物理磁盘绝不故障。
