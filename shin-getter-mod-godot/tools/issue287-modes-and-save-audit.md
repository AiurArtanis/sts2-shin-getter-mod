# issue#287 模式边界与原生存档只读审计

## 结论与冻结状态

审计日期：2026-11-13。只读审计，唯一新增本文件；产品修正均由主开发实施。本报告不构成原生游戏验收、发布批准或全部新模式支持承诺。

- 发现并阶段回传的羁绊 P1/P2 已由主开发修正；最终源码复核通过。主开发补充的 `sgd` 写入口 guard 也已复核。
- 商店 Blitz 隔离与 Grid 类型修正由主开发接管，已看到相应源码；不重复要求实施。
- 本审计范围内，当前没有另一个确证、尚未修正的 P1/P2 模式/存档代码阻断项。下文“待原生实测”不是已发现故障，也不是全面禁用新模式的建议。
- 保留 Dread 普通事件入侵、Act 2 曼荼罗、普通商店、Continue。羁绊仍按既有设计只参与非 custom 的 Standard 单人存档局。
- Blitz 默认无普通事件节点，普通事件入侵和曼荼罗自然不可达；不能以此认定 Harmony 失效。不要向 Blitz 强塞 Standard 地图或普通商店流程。

状态含义：`PASS` 只指本轮源码/官方元数据已核的具体断言；`适用` 描述现有行为范围；`需要修改` 指确证代码问题；`待原生实测` 指尚未启动游戏验证的明确不变量。

## 证据身份与权限

为简洁起见，以下路径前缀均为绝对路径的确定缩写：

- `M` = `E:/Work/StS2 Mods/_worktrees/ShinGetterMod-issue-287-support-113-beta-20261113/shin-getter-mod-godot`
- `N` = `E:/Work/SlaytheSpare2-113-beta`
- `H` = `E:/Work/SlaytheSpare2-111-beta`，仅作历史源码比较，不修改、不构建、不为其新增维护。
- 本工作树 HEAD：`1ad7daf1f5828b09ed97a5b6c17e3118df0216d3`。审计包括主开发正在修改的工作区内容，不是干净 HEAD 的验收。
- 实际读取 `M/lib/sts2.dll`，SHA-256 = `A6CC282C52B96AD2E01D5692B1144B33AA8E3BA4E969582F648ECB13B2F751D7`，与用户指定值一致；官方 MVID = `ad562b4e-7352-4ca2-ae45-0ff44cd284bc`。
- 先调用 113 `codegraph_explore` 定位模式、RNG、Continue；不足部分读取实际源码并与 H 作 `git diff --no-index`。对官方 DLL 另做只读反射、PE 元数据与 IL 检查，不以反编译重编译 DLL 替代官方文件。
- 没有启动游戏、部署、改用户存档、提交、推送或修改 GitHub/Obsidian/docs。未运行会写入 obj/build 的产品或 probe 构建。
- 尝试过一次纯托管 RNG 离线实例化，但在官方程序集 `<Module>` 初始化处抛出异常，未完成 32-draw 验证；不计作 RNG 动态 PASS，不转用重编译游戏替代。反射元数据与 IL 的已核结果不受此未完成实例化影响。
- 开始时已有 AGENTS、manifest、事件、卡牌、商店等脏文件；后续新增的主开发测试/审计文件不归本 worker。只写本文件。

## 确证问题与主开发修正复核

| 编号/原严重性 | 原问题与来源 | 最小修正及当前状态 |
| --- | --- | --- |
| C1 / P1 | `M/src/Services/ShinGetterBondSession.cs:49` 的当前局资格原先只筛 Standard/modifier。113 自定义 Standard 可以 `Modifiers=[]`、`IsCustomGame=true`，会误写真实 profile 羁绊进度。`N/src/Core/Nodes/Screens/CustomRun/NCustomRunScreen.cs:381` 直接传 true。 | 主开发已在同文件 `:52` 加 `!owner.RunState.IsCustomGame`；**PASS，源码复核**。不修改事件入侵资格。 |
| C2 / P2 | 原同文件 `:238` 历史过滤遗漏 custom，可能让 custom 胜负参与正式局回应。`N/src/Core/Runs/RunHistory.cs:22` 新 bool 区分 custom；`N/src/Core/Runs/RunHistoryUtilities.cs:56` 写入该 bool。 | 主开发已在现 `M/src/Services/ShinGetterBondSession.cs:239` 排除 `history.IsCustomGame`；**PASS，源码复核**。 |
| C3 / P2，同类入口补齐 | `sgd` 的既有错误说明明确禁止 custom 写进度，但旧判断也只筛 GameMode/modifier。 | 主开发已在 `M/src/Diagnostics/ShinGetterDialogueConsoleCmd.cs:36` 加 `state.IsCustomGame`。判断仍包在 `if (!plan.IsReadOnly)`（`:27`）内；**PASS，status 不受新 guard 限制**。主菜单写入原有权限不变。 |

113 的根因不是新增了某个 modifier，而是原 `GameMode.Custom` 被拆成实际模式与 bool：`N/src/Core/Runs/GameMode.cs:6`、`N/src/Core/Runs/IRunState.cs:63`、`N/src/Core/Saves/SerializableRun.cs:112`。旧存档 schema 20→21 和历史 10→11 会把 `"custom"` 迁为 Standard + true：`N/src/Core/Saves/Migrations/SerializableRuns/0020_SerializableRunV20ToV21.cs:16`、`N/src/Core/Saves/Migrations/RunHistories/0010_RunHistoryV10ToV11.cs:17`。不能把旧枚举数值直接强转成 Dread。

主开发仅给 linked fixture 增加了对应 bool：`E:/Work/StS2 Mods/_worktrees/ShinGetterMod-issue-287-support-113-beta-20261113/tests/Issue206Console/Bindings.cs:52`、`:61`。这能修复 fixture 合约，**不是原生接口/原生存档实测**；本报告的原生 bool 证据来自官方 DLL 元数据和 N 源码。

## 模式适用矩阵

| 功能 | Standard（非 custom） | Blitz | Dread |
| --- | --- | --- | --- |
| 真盖塔先古羁绊进度/UI | **适用**：真盖塔、单人、ShouldSave、既有 modifier 白名单、非 Daily/测试/非交互，并在 catalog NPC 内。 | **明确不适用**：现有 `GameMode == Standard` 排除。不是“Blitz modifier”排除。 | **明确不适用**：同一既有模式条件排除；不据此禁用其他 Dread 功能。 |
| 原有先古对白/奖励/Proceed 补丁 | **适用**，各补丁按真盖塔身份限定。 | **适用的先古路径保留**；Blitz Neow 原生为选 3/10 卡，不能替成 Standard Neow 奖励。羁绊不接管不等于无先古。 | **适用**；羁绊不接管时继续原生/既有旧对白路径。 |
| 普通事件入侵 | **适用**：owner 必须是真盖塔，事件非 shared、对应页面、run 记录的开关为 true。 | **默认自然不可达**：直线图无 Unknown/普通事件。通过额外地图修改或 debug 造出事件时，入侵服务并无 GameMode 禁令；不承诺这种扩展路径已验收。 | **适用**：保留原事件和选择语义；不能加“非 Standard 一律返回”的新限制。 |
| 第四批 15 事件持久化 | **适用**：单人、非 shared、当前 base EventRoom、稳定 visit 身份；不是全部事件的通用 checkpoint。 | 默认无普通事件，**自然不触发**；存在额外真实普通事件时仍须满足同一严格所有权条件。 | **适用**：同一单人范围；存档后不得重复成本、奖励、history 或 room-entry 副作用。 |
| Act 2 真盖塔曼荼罗 | **适用**：index 1 的下一普通事件，队伍有真盖塔且未访问。 | 默认**不可达**：下一先古走 `PullAncient`，不经过 `ModifyNextEvent`。 | **适用**：保留原 Act index/visited 检查，不因 Dread 禁用。 |
| 良好市民证 | **适用**：持有者每 Act 一次；不要求持有者类型为真盖塔。 | **主开发已隔离**：原生免费选项不消耗次数、不显示自定义普通商店欢迎；没有把 native Blitz 选择换成付金币。 | **适用**：普通 MerchantRoom；现 guard 只排 Blitz，不排 Dread。 |
| RandomCharacter | 原生先解析到实际角色；真盖塔和 Fool 都由现有列表进入候选。 | 相同解析；独立 NBlitzScreen 负责 UI，不等于 NCharacterSelectScreen 的 UI patch 自动覆盖。 | **不提供同义随机流程**：Dread 为 rotating 选择；残留 Random/Rotating 占位对象进入 BeginRunLocally 会被原生拒绝。 |
| 保存/Continue | 原生入口；保存实际模式与 custom bool。 | 同一 Continue，恢复 BlitzActMap 与 BlitzRunModel。 | 同一 Continue，恢复普通地图、DreadRunModel 和当前 Dominion。 |

矩阵代码落点：

- 羁绊：`M/src/Services/ShinGetterBondSession.cs:49`；modifier 白名单 `M/src/Services/ShinGetterBondRunCompatibility.cs:16`，只接受空列表或明确的唯一 BS2 stats modifier，不应扩大成接受任意 modifier。
- 入侵：`M/src/Events/ShinGetterEventInvasionService.cs:132`；开局开关绑定 `M/src/Patches/ShinGetterEventInvasionRunSettingsPatch.cs:10`；第四批额外限制 `M/src/Events/ShinGetterEventInvasionService.Issue238.cs:189`、`:407`。
- 曼荼罗：`M/src/Patches/ShinGetterGetterMandalaPatch.cs:15`、`M/src/Models/Events/SGE_GetterMandala.cs:49`。party-any-ShinGetter 的事件替换与 owner-only 的入侵不是同一层范围；混合队伍不能假定全员收到真盖塔专属入侵。
- 先古旧路径：`M/src/Patches/ShinGetterAncientDialoguePatch.cs:19` 只改 `SHIN_GETTER` 字典项；`M/src/Patches/ShinGetterAncientProceedPatch.cs:14` 只在真盖塔且选项为空时补 native Proceed。
- 商店已适配：`M/src/Models/Relics/SGR_GoodCitizenCard.cs:61`、`:72` 均排 `GameMode.Blitz`/`BlitzMerchantEntry`；`M/src/Nodes/Screens/Shops/ShinGetterMerchantReactions.cs:156` 排 Blitz 自定义欢迎。不是建议重新实现这些 guard。

## 地图、Act、房间与 modifier 的具体交叉

### Blitz

**PASS / 适用事实**：`N/src/Core/Map/BlitzActMap.cs:10` 固定普通节点顺序为 Monster、Monster、Shop、Elite、Treasure、RestSite；起点为 Ancient（`:40`），随后 Boss，可有第二 Boss（`:44`）。因此默认地图没有普通事件抽取机会。`N/src/Core/Runs/RunManager.cs:1048` 的 Shop 仍创建 `MerchantRoom`，但其 `EnterInternal` 在 `N/src/Core/Rooms/MerchantRoom.cs:47` 选择 `NBlitzMerchantRoom`。只检查 `CurrentRoom is MerchantRoom` 不能区分普通商店。

`N/src/Core/Entities/Merchant/BlitzMerchantEntry.cs:112` 的 Cost 恒为 0、`:131` 的成功返回 goldSpent=0；共用 `MerchantEntry.OnTryPurchaseUnsynchronized` 的成功分支仍触发 `AfterItemPurchased`（`N/src/Core/Entities/Merchant/MerchantEntry.cs:116`）。这正是已修证件误消耗的原因，不是证件应该给额外 Blitz 免费次数。

**没有原生 Blitz modifier**：`N/src/Core/Nodes/Screens/Blitz/NBlitzScreen.cs:416`、`:467` 传 `Array.Empty<ModifierModel>()`。`N/src/Core/Runs/RunState.cs:357` 按 GameMode 增加的是 `RunModels` 内的 `BlitzRunModel`。`N/src/Core/Models/Events/Neow.cs:221` 也明确检查 `RunModels.OfType<BlitzRunModel>()`，先于 modifier 奖励分支。

Continue 读回保存的 BlitzActMap（`N/src/Core/Runs/RunManager.cs:872`），不从 Standard 地图推算；`BlitzActMap.GetMapPoints` 沿已保存连接顺序遍历（`:87`）。Proceed 走 `RunManager.Proceed` / `TryAdvanceToNextBlitzNode`，不要求打开普通地图（`N/src/Core/Runs/RunManager.cs:1547`、`:1561`；`N/src/Core/Nodes/Rooms/NEventRoom.cs:302` 转发）。模组先古空选项兜底使用该 native Proceed，源码未发现硬开 Standard 地图。

证件仍位于真盖塔遗物池：`M/src/Models/RelicPools/ShinGetterRelicPool.cs:27`；模组未覆写允许范围，原生过滤位于 `N/src/Core/Models/RelicModel.cs:441`。所以“Blitz 不误消耗”**不等于**“它已从 Blitz 掉落池移除”或“它在 Blitz 必然有收益”。是否调整无收益遗物池是设计决定，不是本轮自行实施的禁用。

### Dread

**PASS / 适用事实**：`N/src/Core/Nodes/Screens/Dread/NDreadScreen.cs:326` 传空 modifier、Dread、isCustomGame=false、ascension 10。地图生成走非 Blitz 的 `Act.CreateMap` → `StandardActMap.CreateFor`（`N/src/Core/Runs/RunManager.cs:889`；`N/src/Core/Models/ActModel.cs:548`）；普通商店走 `NStandardMerchantRoom`。相同 Act index、event pool 与 visited 语义仍适用，所以曼荼罗和入侵不需新加 Standard 限制。

`N/src/Core/Models/Dread/DreadRunModel.cs:17` 在新 Act 增加 AscendersBane 并绑定 Dominion；`:31` 的 AfterRunLoaded 只重新绑定当前 Dominion。正式 Boss 入口在 `:51` 移除对应 Dominion。`N/src/Core/Runs/RunState.cs:604` 同时枚举 Modifiers、RunModels、Dominion，三者不能混称 modifier。

具体交叉：ByrdonisNest/Trial 的入侵战斗也走 CombatRoom，因此仍须接受该 Act 的原生 Dominion；不能把“事件战斗”误当作 Dread 正式 Boss 而清除全 Act 效果。例：`N/src/Core/Models/Dread/Dominions/KinDominion.cs:28` 给 CombatRoom 玩家 Weak/Frail；`WaterfallGiantDominion.cs:22` 为对应敌人加 Explosive。第四批已进入的普通 EventRoom checkpoint 不含战斗，现有重载 AfterRoomEntered 抑制也不会误吞这些 CombatRoom 专用效果；不应把抑制范围扩大到普通战斗或原生新 Act。

## Continue 与 EventCheckpoint 旧适配

这里的 EventCheckpoint 指模组已进入事件的 `EnteredRoomCheckpoint` + detached pre-finished `EventRoom`，**不是** 113 原生名为 EventCheckpoint 的类型/方法。官方 PE 类型/方法定义检查未找到该名称，不能据名新增 Harmony target。

| 断言 | 113 事实与模组交叉 | 状态 |
| --- | --- | --- |
| 原生 Continue 链 | `N/src/Core/Nodes/Screens/MainMenu/NMainMenu.cs:403` 读取 DTO → RunState.FromSerializable → SetUpSavedSingleplayer → NGame.LoadRun(PreFinishedRoom)。没有 Standard-only 分支。 | **PASS** |
| State/NetService 时序 | `N/src/Core/Runs/RunManager.cs:407` 先 State=state；`:414` await IncrementNumReloads；`:416` 才 InitializeShared；最后 InitializeSavedRun。`RunState.FromSerializable:307` 不预先入房。 | **PASS**，旧 OwnsFourthReloadCheckpoint 的前提仍成立。 |
| 官方异步机与一次 callsite | 官方类型 `RunManager+<SetUpSavedSingleplayer>d__195` 仍有唯一 RunManager `<>4__this`、RunState `state`、SerializableRun `save`。MoveNext 的 `IL_003d` 调用一次三参数 `IncrementNumReloads`，前有 Singleplayer=1、false=0；之后才初始化服务。 | **PASS，官方元数据/IL**；不等于 Harmony 运行时安装通过。 |
| scoped reload wrapper | `M/src/Patches/ShinGetterEventSaveScopePatch.cs:33` 从 AsyncStateMachineAttribute 取 MoveNext，不硬编码 d__195；`:45` 要求一次调用；`:48`/`:49` 按类型及 state 名取字段；`:64` 保留 type/forceInTest，不拥有的 DTO 原样转发。 | **PASS**，不需改回旧 API 或全局包住所有 reload。 |
| NumReloads 重写绕过 ToSave | `N/src/Core/Saves/SaveManager.cs:299` 增一后直接 `RunSaveManager.SaveRun(save, ...)`；不重新 ToSave。`M/src/Patches/ShinGetterEventSaveScopePatch.cs:73` 的 InspectSnapshot 因而仍必要。 | **PASS** |
| 已进入事件的恢复边界 | `M/src/Events/ShinGetterEventInvasionService.Issue238.cs:443` 建 detached checkpoint 并 MarkPreFinished，不结束 live event；`N/src/Core/Runs/RunManager.cs:998` 对 preFinished 不再 append history；`:1270` 跳过 BeforeRoomEntered/MarkRoomVisited；`N/src/Core/Rooms/EventRoom.cs:83` 仍触发 AfterRoomEntered。 | **PASS**，现 `M/src/Patches/ShinGetterEventInvasionPatch.cs:53` 仅对拥有的第四批恢复抑制 AfterRoomEntered。 |
| 非先古 preFinished event | 原生 `N/src/Core/Models/EventModel.cs:268` 仍会拒绝；模组 `SetInitialEventState` Prefix（`M/src/Patches/ShinGetterEventInvasionPatch.cs:44`）只在 RestoreFourthState 认领时绕过。 | **PASS**，不是所有事件的 Continue 支持承诺。 |
| 所有权/visit 身份 | `M/src/Events/ShinGetterEventInvasionService.Issue238.cs:119` 校验单人、character、seed、Act、visited coords、history/room、player wire 与 live state；`:73` visit key 含 Act/coord/point/room/model。 | **PASS**，无 Standard map 具体类型强转；Dread 可适用，Blitz 默认不触发。 |
| 写入成功判定 | `N/src/Core/Saves/SaveManager.cs:267` 仍吞 SaveRun 异常；原生 Saved 在 `N/src/Core/Saves/Managers/RunSaveManager.cs:110` 写盘后发出。`M/src/Events/ShinGetterEventSaveVerification.cs:117` 同时要求 Saved 与 readback。 | **PASS**，完成 Task 不能当写入成功。 |
| 新 mode/custom 字段不应被快照漏掉 | `N/src/Core/Runs/RunManager.cs:755` 保存 GameMode/IsCustomGame；FromSerializable `:312` 恢复；`M/src/Events/ShinGetterEventSaveVerification.cs:28` 对整个 native JSON 比较，仅排 save_time/run_time。 | **PASS**，没有仅拷旧字段的新 DTO 白名单。 |

原生存档槽仍是 profile 的 `current_run.save` / `current_run_mp.save`，不按 Standard/Blitz/Dread 分槽：`N/src/Core/Saves/Managers/RunSaveManager.cs:17`、`:29`、`:94`。因此新开另一模式不能假定保留另一份独立 Continue；本轮不创建或操作这些文件。

`SerializableRun.Anonymized()`（`N/src/Core/Saves/SerializableRun.cs:206`）本身未拷新 `IsCustomGame`，也不是完整存档克隆。模组 checkpoint/readback 没走该路径，不是本轮产品阻断项；**不能拿匿名化 DTO 代替原始 DTO 证明 custom flag/NumReloads 不丢**。

## RNG 四 state 与 saved_properties

### RNG

**PASS，源码 + 官方字段元数据**：`N/src/Core/Saves/SerializableRng.cs:8` 仍为 counter + 四个 ulong state0..3，JSON 名为 counter/s0/s1/s2/s3。`N/src/Core/Random/Rng.cs:74` 原地恢复 counter 和 MegaRandom；`:348` 输出完整状态；`N/src/Core/Random/MegaRandom.cs:110`、`:306` 直接恢复/填充四 state。SerializableRng 和 MegaRandom 与 H 文件字节相同；不是旧 109 的仅 counter 回放。

- `M/src/Events/ShinGetterEventInvasionService.Issue238.cs:178` 恢复四 state，`:469` 捕获四 state；`M/src/Services/ShinGetterPlayerEventState.cs:134` 拒绝短数组、全零、无效 counter。适配继续成立。
- 存档字段 **保留 `RngState111` 名称**（`M/src/Services/ShinGetterPlayerEventState.cs:35`）。它现在是既有 wire key，不意味着只允许运行在 111；本轮不为美观改名，也不猜测迁移 counter-only checkpoint。错误文案中的“111 Beta”可另作窄化文案调整，不是运行失败依据。
- 真正新增的是 `PlayerRngType.NonCombatGeneration`（`N/src/Core/Entities/Rngs/PlayerRngType.cs:8`）。`PlayerRngSet.FromSerializable:67` 和 `RunRngSet.FromSave:158` 会按当前枚举初始化并覆盖已存状态；旧存档缺少新 stream 由原生 seeded construction 处理，不需模组补一个固定随机数。
- `M/src/Events/ShinGetterEventTransaction.cs:32` 取得完整 SerializablePlayer，`:79` 回滚完整 PlayerRng，因此包含新 stream，不是只拷 Rewards/Shops/Transformations 的旧三项白名单。`NonCombatGeneration` 已被 WarPaint/Whetstone/AlchemicalCoffer 等原生物品使用，事件获得它们时会发生具体交叉。
- 事件的四处抽遗物已由主开发传实际 `eventModel.Rng`：`M/src/Events/ShinGetterEventInvasionService.cs:576`、`:784`、`:818`、`:819`。原生奖励专用 API 显式使用 Rewards（`N/src/Core/Factories/RelicFactory.cs:25`）；不应以该奖励 API 替代事件 RNG。
- `Rng.Chaotic` 改成 OS 随机种子、移除旧 Shuffle，不意味着 seeded MegaRandom 存档状态失效；本轮未发现这几条保存路径用 Chaotic 恢复确定性状态。旧 111 的抽取结果也不能作为 113 全局内容排序不变的证明。

### SavedProperty 与 JSON metadata

此处 saved_properties 指 `[SavedProperty]` 保存机制；原生遗物 DTO JSON 实际属性是 **`props`**，不是一个名为 `saved_properties` 的新顶层字段：`N/src/Core/Saves/Runs/SerializableRelic.cs:12`，官方 DLL 属性特性已复核。

| 核查点 | 结论 |
| --- | --- |
| 属性发现与私有属性 | **PASS**：`N/src/Core/Multiplayer/Serialization/ModelIdSerializationCache.cs:157` 从 Public/NonPublic 实例属性收集 SavedProperty，`:316` 从已初始化 cache 读取。是原生 ModelIdSerializationCache，不是 109 的 SavedPropertiesTypeCache。官方旧类缺失已复核，不应重新加旧 patch。 |
| 良好市民证的使用次数 | **PASS（结构）**：`M/src/Models/Relics/SGR_GoodCitizenCard.cs:38` 私有 int[] SavedFreePurchaseActIndices；native `SavedProperties` 支持 intArrays（`N/src/Core/Saves/Runs/SavedProperties.cs:36`）、`:292` 按 Public/NonPublic 属性名 Fill。LastFreeFloor 不是次数唯一依据；不能只检查它没变就认定 Continue 正常。 |
| 现有 relic props 载入 | **PASS（结构）**：`N/src/Core/Models/RelicModel.cs:587` 生成 SavedProperties，FromSerializable 在 `:598` 恢复；SavedProperties 与 SavedPropertyAttribute 同 H 字节一致。模组 starter/fragment 的持久开关仍有属性标记。注册顺序/实际 cache 内容需原生初始化证据。 |
| 独立玩家事件 journal | **PASS（结构）**：`M/src/Services/ShinGetterPlayerEventState.cs:25` 的 shin_getter_event_state_v1 是 SerializablePlayer JSON 扩展，**不依附可移除 relic props**；`:147`/`:151` 按实际 ShinGetter 捕获/恢复。已初始化的 false 不是“字段缺失”，不能被新配置覆盖。 |
| native serializer 扩展落点 | **PASS（结构）**：`N/src/Core/Saves/JsonSerializationUtility.cs:23` 仍调用 AlphabetizeProperties modifier；该文件与 H 字节相同。`M/src/Patches/ShinGetterPlayerEventStatePatch.cs:16` 的 Postfix 安装玩家字段和 scoped can_modify rule。仍需在真实 metadata 冻结前安装，`:172` VerifyJsonMetadata 的失败不允许静默忽略。 |
| can_modify=false 丢失风险 | **PASS（风险仍存在）**：`N/src/Core/Saves/Runs/SerializableMapPoint.cs:19` 同时 WhenWritingDefault 与初值 true，官方属性特性元数据与源码一致；false 被省略后会恢复 true。`M/src/Events/ShinGetterEventMapSaveScope.cs:95` 仍需仅对拥有的 DTO 强制保留。不能删 rule，也不能全局改其他存档默认行为。 |
| 包序列化 | SerializablePlayer 与 H 字节相同；事件扩展只对 SHIN_GETTER 读写附加字段（`M/src/Patches/ShinGetterPlayerEventStatePatch.cs:47`）。SerializableRun 113 在 NumReloads 后新增 bool（`N/src/Core/Saves/SerializableRun.cs:162`），不承诺跨 111/113 网络协议兼容。 |

## 新角色与随机/轮换身份

**PASS / 适用事实**：113 新可玩角色为 `Fool`，不是 RandomCharacter 的新名字。官方类型元数据确认 `MegaCrit.Sts2.Core.Models.Characters.Fool` 存在；`N/src/Core/Models/ModelDb.cs:146` 的原生列表为六名，包含 Fool。`M/src/Patches/CharacterListPatch.cs:9` 追加已注册真盖塔并 Distinct，不重建旧五人列表，因此不删 Fool。

- Standard/Blitz 的 `N/src/Core/Multiplayer/Game/Lobby/StartRunLobby.cs:553` 在开局先解析 Random/Rotating 占位对象为 `ModelDb.AllCharacters` 中实际角色，`:1050` 才 GeneratePlayers；`N/src/Core/Entities/Players/Player.cs:299` 拒绝未解析占位对象。仅装真盖塔且其注册成功时是六原生 + 真盖塔；存在其他模组时不硬编码人数/概率。
- `ShouldInject`、羁绊资格、开局开关初始化使用实际 `Character is ShinGetter`；Fool 不会因为“新角色”而被误认真盖塔。`IsShinGetter(ModelId)` 的 `SHIN_GETTER` entry 也与 Fool 不同。没有依据给 Fool 自动补真盖塔事件 journal。
- 良好市民证按 Owner 和持有 relic 判断，不按角色硬编码；Fool 若经合法共享/其他机制持有，在 Standard/Dread 的证件逻辑也适用。这不意味着 Fool 的默认个人遗物池会自然掉落真盖塔证件。
- Dread UI 初始化遍历 AllCharacters（`N/src/Core/Nodes/Screens/Dread/NDreadScreen.cs:187`）；轮换由 `N/src/Core/Helpers/AscensionHelper.cs:289` 按列表计算，`:259` 取下一角色，`NDreadScreen:523` 先传真实角色并保留 rotating flag。原生 `Player.ExtraFields.IsPlayingRotatingCharacter`（StartRunLobby `:1057`）应由 Continue 保留，不能把 rotating flag 当 CharacterId。
- UI 独立边界：真盖塔现有选角/随机背景补丁只 target `NCharacterSelectScreen`（`M/src/Patches/ShinGetterVisualsPatch.cs:24`、`:34`）；Blitz/Dread 是独立 screen。背景资源 getter 和 NCharacterSelectScreenBg resize patch 仍通用，但不能据此前两个 target 宣称新 screen 视觉已验收。只需实测新 screen 的选角/随机落地，不据此禁用玩法。
- 诊断范围提醒：`M/src/Diagnostics/CardExport/ShinGetterCardPngExporter.cs:76`、`:251` 的手工角色枚举尚无 FOOL，FOOL 导出会被拒绝。这不是游戏选角/Continue 身份错误；若本次要求该诊断命令覆盖六原生角色，最小追加 FOOL 校验项、pool 分支和相应帮助/测试。**非本次模式/存档冻结 P1/P2 阻断**，不擅自扩任务。

## 有限原生实测清单

本轮没有游戏启动授权，以下没有勾选 PASS；不要求逐事件、逐模式穷举，也不将纯 fixture 当原生。

| 优先场景 | 必须观察的不变量 | 为什么不能只靠编译/fixture |
| --- | --- | --- |
| 一次 113 原生初始化 | reload MoveNext / serializer / Grid 新 target 实际绑定；VerifyJsonMetadata 成功，ModelIdSerializationCache 含模组私有 saved properties。 | 私有字段 IL 与源码存在不等于 Harmony 已安装或 metadata cache 时序正确。 |
| Standard 第四批事件：pending 与 completed 各一次 Continue | 保存前后 journal、候选/目标、四 state、金币/HP/牌组/relic一致；未重复付费/发奖；连续 reload 每次 NumReloads 恰增一，can_modify=false 不变；history 不多一条。 | 检查真实磁盘写入与 UI 选择恢复，而非只看 Task 结束。 |
| Dread 一次相同事件 checkpoint，加一次入侵战斗 | 普通事件仍能入侵；重载只恢复当前 Dominion，不再发 Act curse；已进入普通事件不重放 room-entry；入侵 CombatRoom 正常收到 native Dominion，正式 Boss 清除规则不被改。 | 这是 Dread 新 hook 与模组 preFinished 抑制的具体交叉，不需全面 Dread 通关来确认此项。 |
| Blitz 真盖塔开局/一次 Continue | 原生 Neow 选卡和直线图继续前进；恢复 BlitzActMap、GameMode/IsCustomGame，不出现普通地图死路；没有虚构普通事件机会或写羁绊。 | 不从默认没有事件错误推断为“事件补丁没加载”。商店 guard 的实测交由主开发现有项，不重复另造实现。 |
| custom 三入口的窄回归 | 空 modifier + custom Standard 不能开羁绊、不能被历史筛选、不能通过 sgd clear/unlock 写进度；status 仍可读；非 custom Standard 仍可用。 | linked fixture 只证明筛选规则；profile 文件无变化需受控原生证据。 |
| 保存的证件/入侵开关 | Standard/Dread 本 Act 已用的 int[] Continue 后仍已用，下一 Act 恢复可用；显式 false 入侵开关不因配置当前为 true 而翻转；移除 starter 不丢玩家 journal。 | native cache、relic owner 绑定、serializer 扩展三者共同作用。 |
| 角色解析最小样本 | Standard/Blitz 随机落到真盖塔与 Fool 时均保存真实 CharacterId；Fool 不接真盖塔 owner-only 功能；Dread rotating 的真实角色 + flag Continue 不变；检查独立选角 screen 画面。 | 角色类型已正确的源码结论与 UI/实际保存不是同一层验收。 |

冻结建议：三项 custom guard 及商店/Grid 的现有源码修正保留；没有源码证据要求增加非 Standard 全局禁用。原生实测授权、运行结果与发布由主开发单独记录，本报告不替其勾选。

## 主开发后续补充：实际Grid声明类型

上文Grid描述反映子审计回传时的工作中源码，不应解读为原111基线在113必然失配。后续全量官方IL探针确认：`FromSimpleGrid.MoveNext`在`IL_031B`调用`0x06008470`，实际声明类型仍为`NCardGridSelectionScreen.CardsSelected`，即使界面实例是`NSimpleCardSelectScreen`。主开发一度改为派生类MethodInfo，因ReflectedType不同导致Equals/Calls计数失败；已恢复基类绑定/适配器，保留失败证据。这个失败由本次开发判断引入，不是游戏BUG或原基线Grid兼容缺陷。原生结果Task的检查点包装没有移除。

本文件中的“没有游戏启动授权”限定于该只读子审计的权限。Artanis已授权主开发在适配核对后启动独立113自动化验证；实际准入、运行和结论须另行记录，不能由本文件推定已通过。

## 2026-11-14 Dread原生短名读取补充

`M-dread-annotations-113-v1`原始stderr实际包含`LocException: SHIN_GETTER.shortTitle`，不能归入worker错误。113新增的`CharacterModel.ShortTitle`由`NDreadNameText.SetNameText(CharacterModel)`直接读取；三语`characters.json`均缺键。补齐与既有角色title相同的短名，不新增Harmony或改写原生Dread行为。新增三语静态门禁，但必须重建PCK并在新候选上复验真实选角；旧attempt保留FAIL，商店使用Ironclad不代表真盖塔Dread开局通过。
