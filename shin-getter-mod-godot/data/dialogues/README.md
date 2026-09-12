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
