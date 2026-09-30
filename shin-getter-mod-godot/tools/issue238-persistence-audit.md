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

## 尚未验证／后续功能提交

这仅是存储接入前置，不表示issue#238已完成。事件访问key、完成页幂等恢复、候选稳定缓存、保存成功信号/读回与故障回滚将随事件实现补测。未执行真实游戏、Live主机客机重连、PCK、隔离模组初始化或部署。删/变/复制奖励卡在结构上无存储依赖，尚未执行真实CardCmd场景，不能冒称实机PASS。

原生 `EventSynchronizer` 在所有peer为每个Owner创建事件实例，并将OptionIndexChosen转发后在各peer执行相同回调。第四批本身严格单人，因此不会出现客机第四批journal结算；前置开关可随原生Player完整packet恢复。普通EventRoom不保存中间页且禁止Ancient以外的pre-finished恢复，完成页将用同一玩家journal恢复，不能调用MarkPreFinished。
