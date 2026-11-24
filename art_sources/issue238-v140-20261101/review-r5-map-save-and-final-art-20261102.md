# issue#238 R5 保存域修复与最终美术接入

2026-11-02：Artanis通过最后一张钻头飞弹ART-037，并明确恢复完整工单开发。当前支线包含原15事件/20路线、5卡/3遗物/1药水、32篇旧事件/46页三语更新，以及先前审核修复；本轮没有另改这些事件的数值或奖励设计。整个issue仍需独立审核和真实运行验收。

## 本轮实现

- `SaveFourth`不再额外调用一次ToSave制造expected。真实原生`RunManager.ToSave`后置边界按活动run与同一detached checkpoint引用登记这次DTO，在原生写入前立即冻结完整expected；direct `LoadRunSave`返回后立即冻结readback。
- `SerializableMapPoint.can_modify`仅在本次登记对象图内显式写出false。所有Acts/Points/Boss/Start/SecondBoss均按对象引用登记到FrozenSet；域外沿用原ShouldSerialize/default规则，不修改地图值或原生全局默认。AsyncLocal token在finally撤销，已捕获子ExecutionContext也失效；嵌套恢复外层。
- 正常Continue还会直接保存SerializableRun。正式109实际入口名称是`SetUpSavedSinglePlayer`，与旧源码快照的大小写不同，已通过实际DLL反射及原生MoveNext验证。本补丁只适配该方法内唯一`IncrementNumReloads`调用，携带原本的RunManager/RunState引用；检查单玩家、同一输入保存、seed/owner/坐标/history/visit/事件/v1戳/当前journal后，包住原生重存任务。不读尚未初始化的NetService，不改NumReloads、batch或异常顺序。
- 完整比较仍只容许save_time/run_time变化；地图/事件池/RNG/经济/历史等全部保留。比较器提供实际expected/readback的完整二进制JSON、稳定字节、显式地图值侧表与全部差异路径。字符串摘要仅给长度/SHA。诊断sink写入失败不改原比较裁定；Saved之后校验失败仍不得回滚内存并重开发奖路线。
- 九件最终批准美术已按原版资源入口接入，来源和导出SHA写入manifest。5个250×190固定卡牌区块及4个128图标/轮廓区块更新，其他卡牌图集像素不变。3个遗物大图为256×256。批准源没有覆盖；ART-033旧压缩失败保留，ART-037新源三档试压均成功并选75–80档。

## 自检与局限

- 正式109 DLL SHA-256：`C2D3E15310259957BA312F9D2362CBA193512EBE9819456A062366E6AF38B9B0`。本轮经主开发特许的功能支线编译0warning/0error；不替代主开发独立候选编译。
- `validate_issue_238.py`：15实际ModelDb IDs及3错负向、15事件/20路线、存档/三语/当前剧情结构通过；保存源码合同新增为23种不安全回归负例。`validate_issue_238_assets.py`核最终9件选择、SHA/尺寸/透明/轮廓/固定区块保留。
- 实际生产保存域的CLR检查覆盖缓存metadata、外来引用、八路异步+普通并发、嵌套异常、取消、捕获上下文/延迟Task撤销与四字段完整差异诊断。真实原生singleplayer MoveNext→IncrementNumReloads→SaveRun在内存存储中执行，owned false保留、unowned仍native省略、reload计数一次、调用者无域泄漏。
- ToSave登记的离线检查仍是明确DTO直调夹具，没有把场景相关ToSave完整执行说成已过。原生reload探针在共享初始化前主动中止，并跳过PeerInputSynchronizer构造以隔离Godot logger。未启动游戏、写磁盘存档或完成真实Continue。先前未隔离该构造而触发的CLR/Godot fatal原始log保留，属于探针环境失败，没有据此修改生产网络或日志。
- 实时Vault核对仍为15个v1.4.0事件，15份Nov1设计段与实施输入相符；旧32篇/46页剧情scene hash全部相符。三份内容笔记中的技术修订（钻头真龙分身、破碎钻头仅draw、批注提交前后取消）与既有R2代码一致，未复活旧效果。两篇打回重设计的完整候选在事件笔记单独展示，仍待Artanis内容批准。

## 交主开发后的最小运行链

1. 独立源码/正式109编译及完整补丁准入后，新隔离输入、仅ShinGetterMod和披露驱动。
2. 自然Unknown派生芳香，UI选Deck目标，生产SaveFourth候选固定；捕获真实pre-write/direct readback快照与所有diff。
3. 原生Skip/重入，正常Pause/SaveAndQuit/Continue，核同房间、visit/History/池/RNG/经济不变；真实确认一次6HP及变形，再Continue核完成页、费用/奖励仅一次。
4. 共享保存代表只补候选固定家族、目标/完成家族及Stone清理必要代表；既有R2/R3/R4已过子项保留，不重跑全矩阵。禁driver额外SaveRun、改/替换生产save、强制finished或Selector代答制造PASS。

29da原生产读回FAIL、6db错房、旧CompletionTask/console切房超时/缺exit收据、资源ERROR与ShaderMaterial P1均保留，不因本轮CLR或美术批准解除。协议版本隔离仍是对外分发前置。未PR、合并、共享部署、Tag或发布。
