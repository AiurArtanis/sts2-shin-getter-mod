# issue#238 开发交付审计（2026-10-01）

## 范围与裁决

- 基线：origin/main@b0f76260baba42d33952c28dff5fb66980e2efbc。隔离分支feat/issue-238-event-invasions-20261001；未切换或修改根工作区。
- 权威：Obsidian《功能开发总工单》issue#238、《事件入侵第四批设计工单》、15篇当前事件设计、9篇新增内容笔记；32篇旧事件的当前段落。存在「完整场景」时只取其至「历史设计与反馈」之前，不复活历史路线。
- 主会话裁决：三重旋风全形态全敌3伤害×3＋3格挡×3，只有实际真盖塔龙额外全敌3衰退一次；3费升级2费。初始遗物可牺牲，本局开关／完成状态不依赖奖励或遗物。
- 主会话裁决：两条旧非结束路线可在原有回流页追加剧情，保留原版文本／参数／原options，不新增页面、继续按钮或RNG抽取。
- 经授权cherry-pick正式编译前置1006222807afa627afd930900891bed5056d80d7为6f1137ab9c96d6637a57db02bda7eb5b594d13ba；仅RNG歧义与非空局部别名，不是新增运行时空引用修复。
- 经授权仅删除issue#206基线jpn.json最后一行多余闭括号，独立提交eb174b823fa9d9e479b8c6b8c8cba6986af9de9e。zhs／eng原始字节不变，jpn原始前缀逐字节保留；三语各119条／119唯一ID。RED→最小数据修正→解析GREEN；随后严格维护SelectEncounter局部save别名门禁，保留优先级与调换顺序的负向夹具，无其他206运行时代码改动。

## 第四批路线对照

| 事件 | 入侵路线 | 结算 |
|---|---|---|
| 低语空谷 | BENKEI | 自选牺牲1件可移除遗物，获得三重旋风；starter不按类型排除，取消不扣不发 |
| 共生体 | BENKEI／HAYATO | -5最大生命并给攻击牌适应／获得共生滤膜 |
| 混沌芳香 | HAYATO | 固定选择目标及最多3个不同合法变化结果，确认后-6生命并变化 |
| 蓝宝石种子 | RYOMA | 获得活化蓝宝石；旧播种路线不回流 |
| 蘑菇饥渴 | BENKEI | -15生命→+5最大生命（保留原生命治疗语义）→固定随机目标升级 |
| 脑蛭 | TRIPLE_COORDINATION | -5最大生命，3张不同已升级合法无色牌中选1张 |
| 人形洞穴之地 | HAYATO | -7生命，攻击／技能／能力中合法单牌附魔适应；旧三形态路线删除 |
| 玩偶室 | HAYATO | 固定原版3件玩偶先选后付80金币，非可取消覆盖层 |
| 修禅织网者 | BENKEI／HAYATO | -4最大生命精确删1牌／-10生命获得钻头导弹 |
| 淹水金库 | RYOMA | 气魄门槛，-8生命、+150金币，不加入贪婪 |
| 药水快递员 | RYOMA／HAYATO | 60金币、空槽、合法罕见药水三选一／70金币购买机动催化剂，满槽先可取消选择弃药 |
| 遗忘之墓 | TRIPLE_COORDINATION／RYOMA | -6最大生命，对合法消耗牌附魔灵魂之力／获得余火 |
| 永恒之石 | BENKEI | +10最大生命，当前生命收敛为选择前-12；不消耗药水，不绕过原版事件入口 |
| 真理石板 | HAYATO／BENKEI | 首次解读前-5最大生命精确升级1牌／获得真理石板 |
| 自助指南 | RYOMA | 获得批注；已删除的旧6生命练习附魔路线不实装 |

全批次仅单人、非shared真盖塔所有者与本局入侵启用时注入；初始页／TextKey去重；完成后原版继续按钮结束。明确规定具体缺项的锁定提示分别落地，不只给综合条件。

## 新内容与旧文本

- 新增4张事件奖励卡、1张战斗内状态牌、3件事件遗物、1瓶事件药水；注册卡牌82=原77完整ID集合＋本批5唯一ID。新卡／药水排除普通奖励池，新增遗物为Event。
- 真理石板2费能力，治疗6／9取降级前值；本场Owner现存所有combat牌降级，自身消耗时升级合法牌。三重旋风依照裁决。钻头导弹2费25／30，二号机兼容真龙变形，完整OnPlayWrapper含重放／结果牌堆／AfterCardPlayed结束后一次替换为破碎钻头，不改永久卡。
- 破碎钻头-1费状态、Unplayable、不升级、不含消耗／虚无；自身非手牌→手牌获得2衰退。批注1费技能、附魔2／3、对应类型且CanEnchant，一号机兼容真龙额外5活力；取消不附魔、不活力、不消耗且退实际手动耗能；成功后重放取消不撤销已成功收益／消耗。
- 活化蓝宝石统计全部减免与Osty转移后的Owner最终整数HP损失，第4次归零、每场一次。共生滤膜首次任意状态进手先消耗再进化1；余火仅首次抽到带消耗攻击／技能时移除本场消耗。机动催化剂无战机分离时才施加1→能量1→抽2，不变形。
- 32篇旧事件／46页三语选择后剧情按当前母稿同步；原有选项、数字、奖励不改。两条非结束回流仅追加文本。三语标签经issue#58门禁验证；既有卡／遗物／药水本地化值逐键不变。
- 美术全部复用已发布资源占位：卡牌复用改签券，遗物复用研究笔记（含大图／描边），药水复用相位冷却液图集区块；等待Artanis最终素材，不生成新图。新增C# UID23份；AtlasTexture沿现有资源格式，无伪造.tres.uid。

## 验证与边界

- 正式109引用dotnet build --no-restore --verbosity quiet：0 warning／0 error。
- 32份validate_*.py全部PASS；validate_issue_181保持已发布v1.2.1历史不变，仅明确校验获批dev阶段／协议；89和命名门禁验证原77全部ID＋5新卡，而非仅替换数字。
- build_character_sprite_sheets.py --check：32套图集＋4份SpriteFrames全部PASS。
- validate_issue_238.py --probe：实际109序列化／packet／mod方法与限定Harmony补丁10组CLR输出PASS；完整边界与限制见[存储接入审计](issue238-persistence-audit.md)。没有用重复的Python行为模型冒称运行时验证。
- 暂存清单逐份校验tracked JSON：47份／BAD=0；仅声明（.uid／import／tres／tscn头部）Godot UID594个／重复0；git diff --cached --check PASS。
- 发布前仍需独立白盒审核与实机验收，特别是完整库存故障回滚、真实磁盘保存／SL、卡牌重放、战斗结束还原和选择界面控制器。未执行PCK、游戏启动、模组初始化、共享部署、Tag／Release、PR／合并或关闭issue。

## 复现

```text
dotnet build ShinGetterMod.csproj --no-restore --verbosity quiet
python tools/validate_issue_238.py --probe
python tools/validate_issue_206.py --source-root E:\Work\SlaytheSpare2
python tools/validate_issue_181.py --manifest-version v1.3.0-dev.issue238
python tools/sync_issue238_localization.py --check
python tools/sync_issue238_legacy_text.py --check
python tools/build_character_sprite_sheets.py --check
git diff --check
```
