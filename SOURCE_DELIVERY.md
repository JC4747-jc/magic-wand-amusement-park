# 石像当前场景原生检查候选交付（2026-11-20）

父版本 restore-4d-lighter@2148b2632b7b88b6eb413e8d9f5945a304c8af7b，开发基线仍 codex/mr-development-snapshot-20261109@8359a0c。只改两个Editor检查中的过时D握拳测试输入和十三模式有界启动器；生产识别链路、手势、运行时代码、模型/骨骼/贴图/场景/meta及火机未改。traeye原分支只读、历史不改写。

在已保存的ParkStatue上串行运行Style/Playback/Garden，均终态0，分别55.69/48.73/66.43秒。10+6+8张实际原生渲染和三QA已查看。A卡通材质可逆，同帧人体18948/青铜102308像素变化；人体B实际PlayableGraph伸展/回位，794观察帧、200行轨迹，0.4m拟合下手抬升约0.219523m，脚/骨盆逐观察帧守卫<0.1mm，根/拟合不漂移，保留先前A/D、Undo、途中Stop/Reset/丢失/图重建/变体清理通过。人体B结果不证明青铜全身动作。

D在人体/青铜两变体上须有限手工确认地面；六朵分层莲花、七许愿条、渐进生长/摆动、三共享网格一材质、无碰撞体，以及Undo/Stop含开启中/Reset/无效平面/丢失清理通过。旧45拳测试按0.03s节拍会超生产时限，改用未变常量派生三拳；11条实际时戳合成D约0.125s、各一事件。生产规则未改，手工地面不等于真实房间重建。

C：当前Style检查验证合成路由和生命周期，但其相机仅框石像主体，侧面板被裁，不能证明阅读布局。仅复用本日既有叙事固定视角验收：修正后的叙事运行时和Editor两源码、86石像资源、15证据哈希仍匹配，最小独立栅格间隙30px。其他植物/房间/选择源码和包差异不冒充全工程或设备对应；任意角度/距离/双眼阅读仍待验。

沿用已核Android包 PlantPresentationValidation-20261120.apk，78064133B，SHA256 f3c8553d646f9871631dd023da4eafb45c3d3719cb4857965afaa1505f40fe31。15场景、CRC、签名、zipalign4、16个ARM64 ELF已核；当前仅Editor/启动器输入变化，玩家代码/资源/包配置不变，不重复构建，不上传APK。静态包检查不证明PICO运行。

便携摘要 [Reports/statue-current-review-20261120/delivery-summary.json](Reports/statue-current-review-20261120/delivery-summary.json)；四项范围/未完成状态 [Reports/statue-current-review-20261120/delivery-case-status.json](Reports/statue-current-review-20261120/delivery-case-status.json)。三QA哈希见摘要，完整原图/轨迹/资源日志留本地。SOURCE_VALIDATION.sourcePayloadTree为完整候选树去掉元数据自身；payloadFiles只是已核范围，其他继承内容不新增验收。独立索引显式十一项，JC4747-jc身份；实际提交/远端哈希由独立回执核对。

未完成：真实YOLO→实际HCI手势→效果、PICO双眼/读距/生命周期/性能、最终美术、青铜全身动作证据、独立采集与非火机模型质量、完整8案例32效果。头显按用户安排暂不连接，不自动定时提交。
