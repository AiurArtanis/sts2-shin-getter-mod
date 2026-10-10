# issue#206 原版对话UI复用审计（2026-10-10）

## 范围与基线

- 分支：`fix/issue-206-native-dialogue-20261010`。
- 基线：`origin/main@40d55ead366c3260c19d15c27ae8b4c49dc4676a`（正式v1.3.0）。
- 隔离工作目录：`E:/Work/StS2 Mods/_worktrees/ShinGetterMod-issue-206-native-ui-20261010`。
- 修改场景内UI/桥接、三语通用文本模板，并按Artanis本轮追加要求加入一次性v1.3.0发布起点。119套对白正文、63段羁绊、sgd参数和游戏数值均不改。
- 两个游戏源码目录只读；未写根工作区、原用户存档、共享部署或其他工单代码。

## 原版109调用点

先使用正式源码的CodeGraph定位，再定向核对源码与实际场景。

| 引用点 | 正式109定义 | 用途 |
|---|---|---|
| `NAncientEventLayout.SetDialogue(IReadOnlyList<AncientDialogueLine>)` | `src/Core/Nodes/Events/NAncientEventLayout.cs` | 复用原场景的对白气泡、头像、尾巴、字体、主题与历史淡出 |
| `SetDialogueLineAndAnimate(int lineIndex)` | 同上，private | 原版滚动/下一句提示/末句选项；保留已保存句位置 |
| `OnDialogueHitboxClicked(NClickableControl)` | 同上，private | 原生鼠标/键盘/手柄继续入口；拦截后仅由Session保存成功推进 |
| `OnDialogueLineFocused/Unfocused(NClickableControl)` | 同上，private | 仅在替换/清空子节点时跳过索引；正常悬停照常执行 |
| `NEventOptionButton.Create(EventModel, EventOption, int)` | `src/Core/Nodes/Events/NEventOptionButton.cs` | 原版先古/普通事件选项场景，非自制NSettingsButton |
| `NEventRoom.OptionButtonClicked(EventOption, int)` | `src/Core/Nodes/Rooms/NEventRoom.cs` | 按精确临时EventOption实例本地路由，绝不进入领奖/投票同步 |
| `NBackButton` | `scenes/ui/back_button.tscn` | 原生返回箭头作为本次跳过；保留三语提示与输入上下文守卫 |
| `TalkCmd.Play(LocString, Creature, VfxColor, VfxDuration)` | `src/Core/Commands/TalkCmd.cs` | 建筑师复用原版角色气泡（Forever）；不制造攻击演出 |
| `TheArchitect._architectCreature` | `src/Core/Models/Events/TheArchitect.cs` | 原版NPC说话者，驾驶员句则使用Owner.Creature |

三语`ancients.json`只增加`SHIN_GETTER_BOND_UI_TEXT = "{text}"`模板；正文仍由本次存档的三语快照传入。不会往普通对白池加入羁绊句，也不修改对白文本版本。

## v1.3.0发布起点（本轮用户追加）

- 所有九位NPC的发布关系从0开始，包括共同初见；取消旧原版遭遇历史的“已认识”导入。该要求取代此前未发布的保守旧认识迁移方案。
- schema仍为1，增加可选`ProgressEpoch`。字段缺失/空的旧测试文件读成全空发布起点，但保留原Revision作原子并发比对；读取/status不会写文件。正式初始化在首个遭遇或显式进度编辑的成功事务中提交。
- 固定功能epoch为`v1.3.0`，并非当前发布版本源，不跟manifest版本升级反复清零；正式/Beta共用这个发布起点。已带当前epoch的真实进度沿用；未知未来epoch、未知schema、损坏JSON拒绝覆盖并允许UI错误/跳过。
- 同时清除旧Completed、LegacyAcquaintances、LastMetRun、RespondedResults、Encounters/ConsumedCues和DebugNextDialogues；本次新遇见仍从FIRST_01生成，不从旧闭合快照或指定熟人主题开始。
- 在事务锁及Revision比对后、原子替换前，将旧测试文件原样复制到同目录`.pre-v1.3.0.<unique>.backup`，不覆盖旧备份。失败不发布内存/epoch、不改原文件；重试后只推进一次。后续新完成段、换语言、重启和后续版本不会再触发发布清零。
- 保留原版run history、其他档位、奖励/游戏进程不变。sgd仍可在发布初始化后显式设置测试进度；不是自动解锁。

## 输入、保存与释放边界

- 移除ConversationPanel、StyleBoxFlat、自制矩形按钮。现有先古背景和原生ContentContainer保持可见。
- 原奖励按钮保留原实例并留在树内，仅隐藏及暂停；临时选项不改EventModel.CurrentOptions，不投票、不授奖、不写原事件选择历史。
- 完成/跳过前移除临时按钮和原生对白、清理角色气泡、禁用返回箭头并断开信号，恢复原按钮可见性；Bridge只恢复暂停前已启用且仍属于当前布局的有效实例，再调用原resume。原锁定与原resume的禁用状态优先。
- 末句仍需明确确认且保存成功才完成；失败显示原生错误气泡与重试，可跳过；不争抢模态槽。恢复同房与语言切换沿用原存档快照。
- 原生容器从选人/错误末句页回到长对白时恢复阅读高度，避免盖住原继续箭头。延迟刷新有generation检查，关闭或替换后不运行旧回调。
- 原生继续热键经Session守卫；暂停/设置时不推进或跳过底层事件。选项/继续/返回间配置循环焦点；原版延迟焦点调用仍通过当前UI的DefaultFocusedControl。
- 010/035仍先消费后播放，静音不重播；显示成功及MarkDisplayed成功后才触发本句语音；下一句受播放等待/超时保护，跳过/退出停止。
- Static仅保留FieldRef/MethodInfo与计数，不缓存节点/角色。原生按钮自身拥有NButton生命周期，包装层只管理其场景引用和本次信号。

## 已运行的开发验证

- RED：新门禁在旧代码上报告`custom dialogue panel remains: ConversationPanel`。
- 发布清零RED：旧DTO未包含持久化epoch；修正后`validate_issue_206_release_reset.py` PASS。
- GREEN：正式109 `dotnet build`，0 warning/0 error；引用DLL与只读正式源码工程的DLL SHA-256一致。
- `validate_issue_206_native_ui.py --source-root E:/Work/SlaytheSpare2`：PASS；8个反向静态突变均被拒绝。
- 原issue#206完整/console/event-return门禁及priority正反门禁：PASS。
- `Issue206EventReturn`：52条完整生产Bridge链接夹具断言PASS（环境/UI为stand-in，不是真实输入）。
- `Issue206Console`：901条链接生产源码断言PASS（使用新临时目录，不访问玩家存档）；包括九位NPC清零、只读不写、备份逐字节一致、失败重试、初始化仅一次、新进度重启保留及未知未来epoch保全。
- 56份tracked JSON，BAD=0；`git diff --check`：PASS。

## 明确尚未验证

本轮不启动Godot/游戏自动化，不导出PCK、不初始化、不部署、不创建PR/合并或发布。以上不代表原生Harmony加载、实际气泡渲染、裁切、键鼠/手柄、语音听感或人工领奖验收通过。需主开发独立白盒审核后安排限定原生/人工验收，不扩大为整局回归结论。
