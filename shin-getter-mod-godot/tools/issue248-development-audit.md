# issue#248 v1.3.0 -> 111 Beta 开发交付审计

## 来源与边界

- Beta基线：`e4dffa1e417c9af7c1269b331d9f768b8b22f023`。
- 正式来源：`main@40d55ead366c3260c19d15c27ae8b4c49dc4676a`，加主开发明确批准的issue#206修正`aef6a9aebac8cd9bdbbf54f908c9b12af5389dd9`。后者仍是正式支线，未声称已合入main。
- 分支：`feat/issue-248-v130-beta111-20261010`；独立目录`E:/Work/StS2 Mods/_worktrees/ShinGetterMod-issue-248-v130-beta111-20261010`。
- 先完成源码/程序集审计，再用Git三方补丁同步已批准内容，保留旧Beta兼容层；没有合并main/Beta目标线，不吸入issue#238/243或未审核分支。
- 正式109/111源码、根工作区、旧Beta dirty工作区、根build/日常Godot/Steam四件套只读并有保护快照。没有PCK、原生初始化、游戏启动/自动化、共享部署、PR、Tag、Release或关闭Issue。

## 审计覆盖

- 开发前：339份正式C#源码、145个动态调用、395类型/795成员/1025虚方法；35组交叉差异全部复核。已批准aef修正追加后重审339份/152动态调用/398类型/797成员/1024虚方法，交叉34组。两个CodeGraph全部src/addons实际哈希与索引一致，bad=0，索引未修改。
- 最终Beta：338份产品C#源码，加5份兼容probe，343份完整遍历；148个Harmony/AccessTools源码调用；397个游戏类型、796成员（761 direct + 35 generic）、1024虚方法继承；30组实际差异全部compatible/adapted。
- `.github/issue-93-109-vs-111-codegraph-diff.json`完整记录3864组全局差异、文件/实现/关系差异、全部源文件哈希、全部实际引用、1193条引用->正式/111定义清单。生成类型/泛型回链源类型声明，全部Beta定义有定位；实际CLR token另做验证。
- 正式DLL：`C2D3E15310259957BA312F9D2362CBA193512EBE9819456A062366E6AF38B9B0`；111 DLL：`6896BBA91CEDDC661B3F789749E9F0AAC338F5DDBBB92C598FC344DEC822DC19`。两个工程同Godot4.5.1/.NET9/C#13；111 Sentry/SharpGen变化不成为模组直接依赖。
- 新增原生羁绊SetDialogue、逐句/悬停/焦点Harmony目标、ContentTween与建筑师说话者FieldRef、TalkCmd/原生返回按钮全部按实际111定义重新核对。固定ProgressEpoch保持v1.3.0，不改成Beta manifest版本。

## 兼容处理与语义核对

- 全部64处FromCard继续传当前CardPlay；8个伤害override保留新上下文；card/enchantment Damage传cardPlay，power无卡上下文明确null；LoseBlock保留选择上下文与正确remover。
- StartRunLobbyPlayer、GenerateAnimator(MegaSprite,Creature)、SignalPlayerChoiceBegun(Player,options)、PotionFactory plural/IEnumerable保持Beta契约，不整块覆盖成正式签名。
- WaitForUnpause公有无参数重载仍在；新私有turn-state重载不被误调用。ulong Seed不截断地进入羁绊持久身份；MapCoord保留col/row；Vigor伤害Hook的named subset在新CardPlay参数后仍能绑定。
- 对EventModel.EnterCombatWithoutExitingEvent发现“签名类型未变、语义改变”：111经EventCombatSynchronizer接canonical encounter并创建mutable clone。保留旧Beta两个canonical输入和两处CanonicalInstance比较，新增门禁锁定；修正旧审计仅称“参数名改变”的结论。没有改变玩法/奖励/回流规则。
- 不带109专用SavedPropertiesTypeCache补丁：111原生ModelDb.Init -> ModelIdSerializationCache.Init -> ModelDb.InitIds遍历注册模型并缓存属性和numeric net IDs/width/hash。不是删除保存功能，也不是改用纯字符串网络包。
- `validate_issue_248.py`核对338份产品源码：除逐项API转换/原生缓存替代外与aef正式候选相同，全部图像/音频/scene/tres/UID/对白/卡牌三语资源精确一致，只有明确渠道版本/更新说明不同。

## 开发验证

- 新issue#248 gate先RED（旧v1.2.2-beta.111/缺少v1.3.0功能），同步后GREEN。源码等价gate曾揭露Event canonical语义差异并完成定向核对，不把它隐藏为一般文本差异。
- 111 Beta正式引用build：0 warning/0 error。
- 55份validate脚本中52组适用，通过；3项不适用保留原检查：issue#181固定v1.2.1、issue#234固定已发布v1.2.2源码集、issue#246仅正式109发布数据。没有将N/A计为PASS。
- B1.3.0 core对109/111均497源码断言PASS；Fighting Spirit十组对两版本源码检查PASS（不是运行时伤害模拟）。
- 实际模块1193个CLR引用token解析PASS；99个Harmony目标/199个参数（含类/方法拆分声明）与44个有类型的动态成员存在性PASS，未应用补丁或启动游戏。3个无直接类型的记录也有确定落点：NCard.Reload由合并类/方法Harmony元数据验证，两个Vigor.Data字段由固定程序集probe验证。
- 111原生保存缓存受控metadata夹具发现44属性，并用生产模型验证14属性往返；测试中只在独立进程开启debug cache资格，明确不等于原生Init、网络包width、玩家存档或游戏通过。Sentry GDExtension在非Godot进程跳过是probe环境说明。
- issue#206生产Session/控制台901断言及完整生产Bridge52断言PASS；均用新临时目录，不触碰玩家存档。
- 35套精灵图集和4份idle SpriteFrames --check PASS；576份tracked UID/import/0重复；tracked JSON解析全部通过；diff-check PASS。
- 门禁适配修正过正式写法/参数匹配与审计工具缺少泛型/虚重载处理的误报；旧FAIL保留在会话/证据，不归为游戏故障，不声称本轮游戏/原生输入/美术/通关通过。

## 交接

主开发独立审核后再做目标线集成、PCK/原生初始化，并协调本轮已授权的正常真盖塔单人通关或累计75去重真实楼层。必须使用本轮精确候选和四件套，不复用正式109/旧Beta数据；正式人工环境保持不动。当前候选未发布，README/Workshop三语只指向已有公开v1.2.2包并标明v1.3.0-beta.111计划包/Tag。
