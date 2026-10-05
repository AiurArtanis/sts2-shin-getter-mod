# issue#206 羁绊对白

2026-09-12确认范围：正常对白，以及以下两个固定句位置的既有语音。**不制作动作演出，不将其作为待补交付项**；故事中的切磋由台词表达，不操作人物、机体或原事件背景。

| 对话ID | 句位置（从1开始） | 音频 |
| --- | --- | --- |
| TANX_RYOMA_BOND_03 | 第5句 | 010：ryoma_getter_tomahawk.wav |
| TANX_BENKEI_BOND_02 | 第4句 | 035：musashi_avalanche.wav（历史文件名，实际角色为弁庆） |

音频位于`res://audio/sfx/characters/shin_getter/voices/`。035中的“秘技”只为正常对白文字，不新增或拼接录音。

默认／燃起来了档均每个真实遭遇的cue最多尝试一次；静音档不发声且仍消费cue。同房恢复不补播已消费的句子；新的真实遭遇可重新尝试未完成故事的cue。保存失败或缺少音频时无声继续。跳过、退出、静音切换会清理播放；正常推进有播放状态及有限超时兜底，不依赖动画回调。

目录包含三语各119套：七位羁绊先古各14套、涅奥13套、建筑师8套。简中还保留Scene分镜元数据作为文本连续性说明，**不是要求运行时播放这些动作**；显示正文使用Lines，选项使用Option。

对应实现：`ShinGetterDialogueCatalog`（嵌入目录）、`ShinGetterBondSession`（档位事务）、`NShinGetterBondDialogue`（阅读／选择／声音）、`ShinGetterBondDialoguePatch`（原事件桥接）。旧非羁绊对白继续用于多人及不适用模式。

本次交给独立白盒审核；用户要求不启动自动化测试，因此未执行编译、门禁、游戏或Godot，也没有PCK／初始化／部署。`tools/validate_issue_206.py`已写入结构约束但尚未运行，不构成PASS证据。

## 2026-09-12 白盒修正：身份与旧档迁移

- 遭遇键为`encounter-v2:runStart:seed:act:mapCoord:pointHistoryIndex:roomHistoryIndex:eventModelId`。不使用`AbstractRoom.Id`或`RunLocation.roomId`；两者在重建时重新分配。正式109的`RunManager.ToSave`／`RunState.FromSerializable`保留地图历史和坐标；正常进图在进入房间前追加历史，读档从原位置重建；合法子房间在进入前追加Rooms。
- 基础事件固定使用该地图历史条目的Rooms[0]，即使此前进入过子房间后返回也不换键；非基础事件使用最后一个同ModelId事件槽位。不同地图位置、历史条目或同格新子事件槽位会得到不同键。无可靠槽位时显示错误并允许跳过，不回退到临时编号。尚未发布的旧roomId快照在当前run被发现时保留原文件并提示跳过，不猜测对应槽位。
- sidecar第一次创建时，只从成功读取的、开始时间早于本局的普通单人真盖塔历史中，提取具有完整EVENT类别／NPC ID的事件记录。其他角色、多人、每日、自定义、当前局、损坏／修复丢失数据均不作为证据。聚合AncientStats不能区分模式，因此不单凭其次数迁移。
- 迁移写入独立`LegacyAcquaintances`和`LegacyMigrationVersion`，仅放行已认识NPC的三条第一段，不写入`Completed`、任一角色段、胜负回应或虚构最近相遇时间。原历史只读。首次迁移与首个遭遇同事务落盘；失败不发布进度，重试沿用候选。已有sidecar绝不再次导入后来的历史，避免把本功能中跳过的初见误作旧版认识。
- 白盒反向检查清单：变更roomId不得改变身份；基础事件经历子房间后SL不得重复交谈；不同地图位置／新子事件必须独立；无可靠历史不可猜身份；旧档多次访问只能认识而不能兑换第2／3段；仅有跨模式聚合计数、损坏历史、当前局访问时仍初见；已有sidecar的未完成初见不得被后续history覆盖。

这些是源码修正与待核验契约，未执行编译、静态门禁或实机测试。

## 2026-10-05 测试控制台：sgd

`sgd`（Shin Getter dialogue）沿用语音测试指令`sgs`的单词格式。
这是**修改当前存档档位**的开发工具，不是无副作用的剧情预览器。
查询只读；解锁可向前或向后设置精确进度，清空会删除指定对象进度。
先确认使用测试档位；不写原版run history、奖励、卡牌、形态或其他账号。

```text
sgd <NPC|ALL> status
sgd <NPC|ALL> clear
sgd <NPC|ALL> unlock <RYOMA|HAYATO|BENKEI|ALL> <0-3>
sgd <NPC|ALL> unlock <first|win|loss|return|chat> [1-N]
```

NPC支持官方ID（大小写不敏感）及中文名：
OROBAS／欧洛巴斯、TANX／坦克斯、VAKUU／瓦库、DARV／达弗、PAEL／佩尔、
TEZCATARA／特兹卡塔拉、NONUPEIPE／诺奴佩普、NEOW／涅奥、THE_ARCHITECT／建筑师。
`Architect`也是建筑师别名。驾驶员支持RYOMA／龙马／流龙马、HAYATO／隼人／神隼人、
BENKEI／弁庆／车弁庆。操作也接受查询／清空／解锁；主题接受初见／战胜／战败／久违／聊天。

| 参数 | 精确含义 |
| --- | --- |
| status | 输出共同初见是否完成、各人已完成段数和待触发请求；不创建文件、不改revision、不导入历史 |
| clear | 清除本NPC对白完成、旧档认识证据、久违／结果回应记录、旧遭遇快照／语音消费及待触发请求；下次回初见 |
| unlock 驾驶员 N | 标记本NPC初见完成，将该人第1至N段设为完成，删除N之后的完成标记，其他两人保持；N=0／1／2时下次直接播放其第1／2／3段，不经过选人菜单 |
| unlock ALL N | 将三人的已完成段数均设为N；N<3时下次仍显示正常选人菜单，各人都是第N+1段 |
| N=3 | 该人不产生第四段；下一次显示剩余人的选人菜单，三人全完则指定CHAT_01 |
| unlock first | 清空本NPC进度并指定下次共同初见 |
| unlock win／loss／return／chat | 对七位羁绊先古先补齐初见和九段前置，再指定下一次该主题；涅奥与建筑师仅补初见。不修改真实上一局结果，不创建虚构历史 |
| 可选1-N | 从该NPC／主题的稳定ID升序选择变体；默认1。涅奥win／loss各3、chat共6；其他NPC相应主题各1。越界整体拒绝，不部分修改 |
| NPC=ALL | 查询／清空覆盖9个对象；驾驶员进度只覆盖7位有羁绊的先古；return覆盖除涅奥之外8位。只处理支持该模式的对象，输出实际处理清单；指定涅奥return或其驾驶员进度会拒绝 |

例如：

```text
sgd ALL status
sgd 坦克斯 clear
sgd TANX unlock RYOMA 2
ancient TANX
sgd TANX unlock BENKEI 1
ancient TANX
sgd ALL unlock ALL 0
sgd OROBAS unlock win
sgd OROBAS unlock loss
sgd OROBAS unlock return
sgd NEOW unlock chat 6
sgd THE_ARCHITECT unlock win
event THE_ARCHITECT
```

`ancient`／`event`是原版的进房指令，不是`sgd`自动调用的命令；需要自行处于可进入事件的
普通真盖塔单人局。设置后再用合法地图遭遇或原生进房指令测试。`sgd`不强行在当前页面重开
已经结束的交谈，不重新发奖，也不改变先古出现概率。

“下次必定触发”指下次**符合羁绊系统资格的新遭遇／新创建对话快照**：请求持久保存到
本档位`shin_getter_bonds.json`的可选`DebugNextDialogues`字段，先于初见／羁绊／久违／50%
结果随机选择。创建快照与消费请求在同一原子事务内；保存失败保留请求，重试同一快照，
同房读档恢复快照而不重新抽取。打开或跳过指定对话不自动完成该段，仍须末句确认并保存。
旧schema-1文件可以没有此新增字段；损坏文件拒绝覆盖。
首次修改且尚无sidecar时，按既有保守规则只读迁移可靠旧历史中的认识事实，清空对象仍会
精确移除自己的认识事实；其他NPC不因此丢失旧认识。现有sidecar不再次导入，查询也不迁移。

正在读对话时仅允许查询，禁止解锁／清空；多人、每日、自定义、其他角色、回放／
非交互与不保存的局同样禁止修改单人进度。菜单中可操作当前档位，之后普通真盖塔单人遭遇
才会消费。不会在不适用模式静默消费测试请求，也不会伪造胜败、修改NPC奖励或扩展两句语音。

离线检查见`tests/Issue206Console/README.md`。这些离线结果不等于真实控制台、鼠标／键盘／
手柄或剧情听感验收通过；最终主开发审核与集成另行安排。
