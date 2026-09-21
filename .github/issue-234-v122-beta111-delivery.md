# issue#234 / issue#93 — v1.2.2 111 Beta交付

## 范围与前置审计

- Beta基线`50aa4e23`，正式输入`7286ae7f59e12f266b9d008ac2509f0ade648e93`，正式输入基于mod-v1.2.1，仅包含issue#193／issue#214及发布材料。未合入main或issue#206。
- 分支`feat/issue-93-v1.2.2-beta111-20260921`；仅6个生产C#文件同步，318个其余生产C#文件与Beta基线一致。新门禁`validate_issue_234.py`检查全体324个生产C#、音频集合与内容、三语正式字段以及历史保留。
- 修改前通过CodeGraph定位NAudioManager／NTickbox／NSubmenu，再对比109与111源码。增量审计表见`issue-93-111-beta-api-audit.md`的新首节。既有CardPlay、Hook、Harmony和反射适配未改动。
- 语义清单刷新后：326个C#（含兼容探针），378个游戏类型、701个直接游戏成员、33个泛型成员；相较Beta基线仅新增NSubmenu._lastFocusedControl引用，字段签名仍为Godot.Control。26组变动符号候选不变，125处Harmony／反射调用不变。

## 验证证据（2026-09-21）

| 命令／核验 | 结果 |
| --- | --- |
| `dotnet build shin-getter-mod-godot/ShinGetterMod.csproj --configuration Debug --nologo` | 0 warning / 0 error，使用111 DLL |
| `python shin-getter-mod-godot/tools/validate_issue_234.py` | PASS，6正式一致＋318 Beta基线一致；23音频；三语／历史一致 |
| `python shin-getter-mod-godot/tools/validate_issue_193.py` | source contracts PASS，非运行验收 |
| `python shin-getter-mod-godot/tools/validate_issue_214.py --combined193` | source/asset contracts PASS |
| `python shin-getter-mod-godot/tools/validate_issue_88.py` | PASS |
| `python shin-getter-mod-godot/tools/validate_issue_57.py` | PASS |
| `python shin-getter-mod-godot/tools/validate_issue_93.py` | PASS，包含CodeGraph清单check、111 API/反射/Harmony目标探针；不启动游戏 |
| tracked JSON解析 | 43份，BAD=0 |
| `git diff --check`及cached检查 | PASS |

- RED→GREEN：版本门禁先因缺少v1.2.2 Beta三语历史而失败，补齐后通过。既有CodeGraph清单因6文件与新增成员引用过期而失败；由既有生成器只读读取两套游戏源码／数据库刷新后，check通过。
- 111引用sts2.dll SHA-256：`6896BBA91CEDDC661B3F789749E9F0AAC338F5DDBBB92C598FC344DEC822DC19`；0Harmony.dll：`EF1898322C9F5C86DC1B0758B272A9C440823B4A41CA9A0B82A3AA6B3D206387`。
- 本地编译ShinGetterMod.dll SHA-256：`C1833E62A0C7AFB6EAA0765E3FED0F17B0A89DF11E8BB674110E80611CD24382`。这是开发编译证据，不是最终发布资产。

## 发布边界与待主开发收口

- manifest为`v1.2.2-beta.111`，最低游戏版本`0.111.0`。三语README、Workshop材料和Release草稿指向同一个`mod-v1.2.2`页面，分别说明正式包与Beta包。
- Beta本地ZIP／展示名`shin-getter-mod-v1.2.2(111-beta).zip`；GitHub真实asset name会规范化，不猜测下载URL。`validate_issue_93.py`已纠正旧连字符包名。
- 游戏更新记录首项为本次Beta，其后是本次正式记录；全部旧Beta／正式历史不变。
- 旧曲的删除仅发生在新隔离分支，可从基线恢复；按正式候选移除8首旧曲及旧处刑音频／import，新增／重映射保持正式23曲库，用户素材库未动。
- 不运行Beta自动化玩法回归，人工8项待办均未验证。未PCK、初始化、部署、创建pr、合并、Tag、Release；没有覆盖根工作区、Steam或日常Godot。
- 请主开发独立审核、合入Beta目标线、构建PCK并完成授权的最终验证与同Release上传。拿到最终产物／发布证据后再回填父issue#93指定章节，当前不能标发布完成。
