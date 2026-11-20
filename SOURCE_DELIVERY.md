# 柳树宝箱金币：原生遮挡检查候选交付（2026-11-20）

父版本 restore-4d-lighter@340acdc49e1ff18ad63b54f4d73ff6ce4db12e58，开发基线仍 codex/mr-development-snapshot-20261109@8359a0c。仅改HciSeasonTreeChecks Editor诊断，并附金币摘要/状态、柳树D表补充和一幅已查看对照图。金币/箱体/树模型、骨骼/材质/场景/meta、生产运行时/识别链路/手势和火机均未改；traeye原分支只读，历史不改写。

现有宝箱已有七枚金币。使用原细节浅角和上方相机，隔离箱体的白币/黑箱自遮挡mask、无箱轮廓及逐币可见像素测量，证实浅角0/8100像素、七币全被箱壁遮挡；上方约10778/15565（约69.25%），七币各有可见像素，正常原材质上方渲染也看到了金币。闭盖/底/墙间隙约.03/.002/.007，单位为chest局部模型单位、未经拟合/world scale，不是头显物理距离测量。因此保留已有模型，不为截图搬动金币。

当前虚拟/合成Observe选择v2两项终态0，35.23/33.93秒，13+14原生图（含mask）及对照图已查看；A/B/C/D、Undo/Reset、拟合/锚点和选择/UI继续通过。诊断临时相机/材质/层/MPB/启用/盖姿态/雾/RT在finally恢复或销毁，身份/层/姿态/HasPropertyBlock及原材质画面已核。首轮白像素诊断因原MaterialPropertyBlock覆盖白材质而失效，终态2；修正为保存/清理/恢复renderer与材质索引MPB后再测，失败证据本地保留，不把它当成缺模型结论。

范围仅为两个固定视角的箱体自遮挡、现有几何无穿透和当前原生合成回归，不证明任意头位、主观可读性、实际摄像/TCP/YOLO/手部/房间重建/PICO或最终美术。正常上方图和mask都是Unity渲染，不是概念图；每币mask像素不能替代真实显示舒适度验收。

便携摘要 [Reports/willow-coins-review-20261120/delivery-summary.json](Reports/willow-coins-review-20261120/delivery-summary.json)；金币补充状态 [Reports/willow-coins-review-20261120/delivery-case-status.json](Reports/willow-coins-review-20261120/delivery-case-status.json)；更新柳树D范围 [Reports/willow-current-review-20261120/delivery-case-status.json](Reports/willow-current-review-20261120/delivery-case-status.json)。原图/JSON/合成CSV/资源日志留本地，QA及哈希随摘要交付。

沿用已核Android包 PlantPresentationValidation-20261120.apk，78064133B，SHA256 f3c8553d646f9871631dd023da4eafb45c3d3719cb4857965afaa1505f40fe31；15场景/CRC/签名/zipalign4/16个ARM64已核。只改Editor，玩家代码/资产/包配置不变，未重建/上传APK；静态包检查不证明PICO运行。

SOURCE_VALIDATION.sourcePayloadTree为完整候选树去掉元数据自身；payloadFiles为已核范围，其余继承不新增验收。独立索引显式八项，JC4747-jc身份，实际远端提交/文件哈希见独立回执。真实YOLO→实际HCI手势→效果、PICO、最终美术、独立采集/非火机模型质量和完整8案例32效果仍待验。头显暂不连接，不自动定时提交。
