# issue#238 R6 owned交互保存边界

依据主开发对R5的最终归因修复：原生07保存内容为choice1/discovered5，后续展示及两次Skip活态达到choice2/discovered8却未产生新Saved，退出未重存，Continue读回旧内容。R5整体FAIL/NOT_ACCEPTED、自然exit1保留，6HP确认/第二Continue/代表NOT_RUN。两次真实write/readback各67 MapFlags/10 false完全一致只说明旧地图布尔值问题在该两写未复现。3个history统计的原生汇总转换交框架精确校验，本补丁不处理或排除这些值。

## 实际正式109时序与接入

只读反编译的是SHA `C2D3E15310259957BA312F9D2362CBA193512EBE9819456A062366E6AF38B9B0`的实际引用DLL，未使用旧源码快照推定大小写或声明类型。

1. `EventSynchronizer.ChooseOptionForEvent`先启动`eventOption.Chosen()`，再调用`SaveEventOptionToHistory`。Prefix只为单人真盖塔当前base事件中真正可用的本批选项建立绑定同一model/synchronizer/player/option引用的session；原生history方法Postfix完成信号。RunFourth等待这个真实方法完成，再保存并开始业务。没有Task.Yield、固定帧延时或手补history；没有声称已观察原生List.Add的内部时间戳。原生方法失败时保留原异常并撤销session。
2. `FromChooseACardScreen`展示原生屏幕后完成原生MarkCardAsSeen循环，随后调用screen.CardsSelected；`FromSimpleGrid`在Push原生屏幕后调用基类`NCardGridSelectionScreen.CardsSelected`。仅适配这两个MoveNext中的唯一结果任务调用：保持原调用和虚派发，将已有task交给owned保存边界。未改全局screen、原生discovery方法或SaveAndQuit。
3. AwaitFourthSelection只在本功能的待选阶段激活可撤销逻辑作用域；费用/奖励提交时该作用域已结束。调用点已到达后立即保存实际副作用，再放行原生选择结果。即使玩家还没选/Skip，也会启动保存。Brain仍使用原生不可取消网格；本轮仅纳入同一待选作用域。
4. 取消/Skip正常完成后，再次保存原生已完成副作用，之后才回INITIAL。缓存重入每次都经过开头的history完成保存，因此helper缓存返回不重抽也不会跳掉本次native history。固定目标/候选及事件RNG继续使用原v1数据，无手补发现列表、退回旧expected或测试驱动补存。

`NGame.ReturnToMainMenu`的原生`LoadMainMenu`已有等待CurrentRunSaveTask的逻辑，本补丁不改退出机制。候选展示调用点启动真实写入，不把写入延后到选择返回；原生退出等待仍负责在菜单加载前等当前写任务。真实candidate-open直接退出仍须新隔离运行证明，不能由这条源码路径推为已验收。

## 故障与清理

保存失败会立即报告并保留原异常，但不提前抛出破坏原生选择结束/overlay清理；原生command返回后，在费用/奖励前拒绝该结果并回原有错误处理。取消也检查这个失败，不自动重试。已确认Saved后的事务语义、完整expected/direct readback及仅两时钟容差均保持。

session结束时撤销共享active标志，已捕获ExecutionContext也失效；普通/其他model/owner/option/synchronizer不获得保存作用域。已离开的run/room不回开旧页或向新房间弹交互错误。

## 自检边界

- 特许正式109编译0warning/0error，源合同31种不安全负向及15实际ID/3错负向、15事件20路线与三语/旧剧情门禁通过。
- 定向CLR安装真实三组Harmony补丁（含两个原生MoveNext适配），实际执行原生SaveEventOptionToHistory及Postfix。检查同key异引用/其他player/synchronizer拒绝、history完成前不存、待保存结果不放行、缓存重入、native取消、保存失败后仍走清理且不重试、异常原样传播和session撤销。
- CLR中discovered5→8是明确的候选展示夹具，未执行Godot场景、真实MarkCardAsSeen、ToSave全体或磁盘Continue。不得把该结果记成真实游戏保存通过。原map scope/reload CLR保护仍通过。
- 第一次CLR因SimpleGrid实际IL调用基类而非派生声明的方法被门禁拒绝，保留原log；已按实际IL绑定基类且保留虚派发后通过，不放宽调用计数。
- 九件批准美术、已独立解除的3项P2及运行时资源无改动。不重跑R2–R4/整局/多人/Beta，不开新游戏；候选交主开发独立准入后才新实例。

## 新实例应核对

- 自然Aroma：初次选项history完成→目标固定→候选真实发现后生产保存；固定候选仍开着时正常退出/Continue，候选/目标/RNG/history/发现均保持。
- Skip回INITIAL、缓存重入再Skip后正常退出/Continue，保留新增choice与三张发现，不重抽、不重置派生计数。
- 通过后实际确认只扣6HP/变形一次，再Continue为完成页且不重复；最后仅必要共享家族与Stone清理代表。
- 框架保留完整差异，只精确校验已证原生当前history玩家统计汇总；产品比较仍只容许save_time/run_time。

Artanis已于2026-11-02明确回复“两篇通过”，自助指南和永恒之石重设计内容人工审核通过。未PR、合并、共享部署或发布；真实运行及旧资源/lifecycle边界继续独立。
