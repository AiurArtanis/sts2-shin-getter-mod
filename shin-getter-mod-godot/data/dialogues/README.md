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
