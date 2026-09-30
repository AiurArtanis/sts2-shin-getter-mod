# issue#238 玩家本局事件状态前置

## 限定接入

- 109 `JsonSerializationUtility.AlphabetizeProperties` 的 Harmony Postfix 只对 `SerializablePlayer` 的 Object metadata 增加可选字符串 `shin_getter_event_state_v1`。不替换原生 Options/resolver，不改其他类型。
- `Player.ToSerializable` 捕获；`FromSerializable` 与 `SyncWithSerializedPlayer` 恢复；`SerializablePlayer.Anonymized` 复制。CWT按玩家对象隔离，真正绑定非NullRunState后才校验seed与run对象；缺字段与显式false严格区分。
- 真盖塔玩家在原生packet尾部固定读写一个版本化字符串（包括未初始化空值）；原版玩家字节与位布局不变。`RelicCmd.Remove`在牺牲两类starter前冻结原值。
- 新局开关由既有 `RunState.CreateForNewRun` 补丁显式初始化；旧档缺字段仅惰性迁移一次，不依赖任意卡/遗物作为后备权威。
- 限制48KiB、64个访问记录、每组最多3候选；未知版本、损坏字段和错误run均拒绝，不当成缺字段重新迁移。
- 本分支manifest改为 `v1.3.0-dev.issue238`、`affects_gameplay=true`，以原生id-version联网检查隔离旧packet协议；未改已发布v1.2.2包。

## 实际执行结果（2026-10-01）

命令：`dotnet run --project tools/issue238-probe/issue238-probe.csproj`。

- 使用实际109 DLL、实际mod Harmony补丁、真实 `JsonSerializationUtility.GetTypeInfo<SerializableRun>()` 嵌套往返：true、false、无字段、完成分支、Anonymized通过；损坏JSON/未知版本拒绝。
- 直接执行实际状态服务方法的inventory-only Player fixtures：两类starter的true/false旧档迁移、移除与重新获得starter、双Owner隔离、无字段Sync不遗留旧值通过。fixture不启动Godot或SaveManager。
- 原生packet：真盖塔/原版/真盖塔/原版四玩家混排与后继int哨兵、完整 `SerializableRun` 与哨兵通过；读写BitPosition完全对齐。
- 原版玩家在本补丁启用/禁用时packet byte/bit完全相同。ID cache使用最小确定性fixture，不初始化游戏资源catalog。
- 正式109引用 `dotnet build --no-restore`：0 warning/0 error。

## 本轮事件接入与实际探针

- 已在第四批15事件／20路线接入访问journal。访问身份包含seed、Owner NetId、act、地图坐标、访问序号、房间子序号、ModelId；真实Create helper的稳定读档与各维度隔离探针通过。
- 随机目标以牌组位置与完整SerializableCard SHA-256固定；候选route命名空间、不同ModelId无放回抽取并先落盘后展示。确认时重检合法性，完成页只读已完成route白名单，不重做命令。
- 原生SaveRun会吞写入异常，因此真实SaveFourth订阅RunSaveManager.Saved并读回比较完整SerializablePlayer JSON。实际Verify helper故障注入覆盖无信号、信号前错误、信号后错误、读回错误和完整玩家不一致：信号前回滚；信号后不在内存重开奖励。订阅有finally退订。
- 事件事务记录完整永久卡组、原遗物实例／SavedProperties、药水槽、HP／MaxHP／金币、玩家RNG与Odds、个人及共享遗物抓袋、发现列表、journal和当前历史条目；不调用全Player.Sync，不重放拾取Hook。实际原生RemoveInternal置位后的遗物恢复标志探针通过。
- 本轮对已存在payload的8个字段全部要求JsonRequired；显式null、缺关键字段、矛盾未初始化对象、未知版本／损坏数据拒绝，真实缺字段仍可迁移。主会话对6959dfad存储前置独立审核通过；这不是整个issue#238的审核结论。
- 无尽传送带／打造时间使用每事件CWT临时回流scope，LocString复制原变量；当前语言重新解析原版与新增两段raw文本，再走原生SmartFormat。实际GetRawText补丁探针覆盖使用前不变、成功拼接、重复不叠、语言刷新、变量保留、另事件隔离及推进清理。
- 活化蓝宝石实际拦截器及AfterOsty最终派发探针覆盖0／小于1整数伤害、非Owner、非战斗、第四次归零、每场一次、重置和BeforeOsty不计数。

## 真实尚未验证边界

以上是正式109 DLL和实际mod方法／补丁的隔离CLR探针，不是游戏实机测试。Save验证使用注入write/readback，未boot真实SaveManager磁盘路径；移除遗物探针不是完整库存事务／UI故障回滚。ID cache、玩家库存、战斗对象和loc查询使用最小fixture。

未执行真实游戏、Live主机客机重连、PCK、隔离模组初始化或部署。完整UI选择／控制器、卡牌wrapper重放、战斗结束还原、真实存档SL和库存故障回滚仍需独立白盒复核及Artanis实机验收；不得写成实机PASS。

原生 `EventSynchronizer` 在所有peer为每个Owner创建事件实例，并将OptionIndexChosen转发后在各peer执行相同回调。第四批本身严格单人，因此不会出现客机第四批journal结算；前置开关可随原生Player完整packet恢复。普通EventRoom不保存中间页且禁止Ancient以外的pre-finished恢复，完成页将用同一玩家journal恢复，不能调用MarkPreFinished。
