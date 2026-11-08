# issue#238 R4：原生事件ID接入修正

基于4527631b的R3最终裁定及主开发明确解冻，仅修3个ID/4条路线；不改R3选择实现、批注/C/D、journal v1、目标/候选、SaveFourth/CommitFourth或剧情正文。旧1a9/R2/R3产品与raw保留，未合并/共享部署/发布。

## 改动

- FORGOTTEN_GRAVE → GRAVE_OF_THE_FORGOTTEN（GraveOfTheForgotten）：联动、龙马两条路线。
- WATERLOGGED_VAULT → SUNKEN_TREASURY（SunkenTreasury）：龙马路线。不是WaterloggedScriptorium。
- STONE_OF_ETERNITY → STONE_OF_ALL_TIME（StoneOfAllTime）：弁庆路线。

事件实现仅20个ID字面量变化；源JSON仅4条routes及4条authority.routes的event字段变化，input_sha256、note、route及三语正文保留。三语各29个本批键改前缀，共87个；值和其他命名空间逐项与4527比较一致，卡牌/遗物/药水本地化不变。

同步清理受清单限定：只接受声明的旧前缀/后缀，旧值须等于新生成值，新键已存在时也必须相同；未知键、不同正文或冲突会拒绝。所有语言预检完成才写表，未变化的表不写。--check拒绝残留旧键，清理后再次--check通过。已删除的死键不能由下一次同步复活。

## 独立原生门禁

`tools/issue238-native-events.json`列的是15个已核定的原生类型，不从ROUTES反向拼类型名。`read_issue238_native_event_ids.ps1`在独立pwsh进程读取实际正式109 DLL，SHA固定为C2D3E15310259957BA312F9D2362CBA193512EBE9819456A062366E6AF38B9B0，调用实际公开ModelDb.GetId(Type)，不Init/Inject、不构造模型、不加载mod DLL、不启动游戏。需要pwsh与本机GodotSharp4.5.1，缺引用/版本变化时明确失败。

validate_issue_238.py默认执行此门禁，将实际15个原生ID与生产FourthEvents、Python ROUTES、源routes/authority交叉，并检查三语旧键已清零；废弃键清理目标也必须等于对应原生类型的实际ID。每个历史错ID分别构造负向配置，必须被真实原生ID集合拒绝。它不依赖第二份抄写的事件ID预期来判通过。

生产ID修改前，新门禁已经对4527旧配置实际失败：unknown为FORGOTTEN_GRAVE/STONE_OF_ETERNITY/WATERLOGGED_VAULT，missing为GRAVE_OF_THE_FORGOTTEN/STONE_OF_ALL_TIME/SUNKEN_TREASURY。源ID改正但旧表未清理时，sync --check也实际拒绝WATERLOGGED_VAULT残留。历史错误记录没有改名或覆盖。

这证明的是实际身份计算与当前源码/文本映射，不是初始化后的ModelDb注册lookup、正式模组编译或游戏入口。主开发仍须绑定新SHA/DLL后独立编译，并用真实3个事件入口补4条路线，不能给driver加旧别名掩盖接入失败。

## 保留边界

R3中Grave资格满足的真实入口FAIL保留；Sunken/Stone当时仅源码/GetId失败，不改写为旧版已实测。四条Deck规则、芳香自然候选Skip/重入及其他指定局部通过保留，不重跑全矩阵。

芳香显式EventRoom checkpoint恢复通过不能替代生产SaveFourth(null)自动保存恢复，后者仍NOT_VERIFIED。直接console换房45秒超时、原生EnterRoomDebug/ReturnToMainMenu局部通过分别保留。R2 CompletionTask遗留、A缺exit收据、Godot/stderr资源ERROR、旧ShaderMaterial P1、9图/2篇人工待审均未解除。

journal键由实际model.Id生成，未发现正常入口写入这3个旧别名键的路径；本轮不猜测迁移、不清除未知Visits、不全payload替换，schema/route键不动，也没有读取玩家实际存档。芳香生产保存合同另做外部只读方案，不夹入本次ID提交。

功能会话自检限结构/原生ID/文本同步、负向配置、87键值与其他代码保留、C#/Python语法及diff。没有编模组DLL、开游戏、导PCK或改旧沙箱。整个issue仍未验收。
