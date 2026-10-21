# issue#248 v1.3.0正式Tag → 官方111 Beta候选

## 2026-10-20 issue#206 BS2统计标记兼容增量

- 以下原同步记录属于冻结823。新增量只处理运行证据确认的资格误拦：BS2 v2.3.0无条件在CreateForNewRun附加`GirlsNightmareEndStatsModifier`，其11个保存计数供结尾统计显示，不是玩家选择的挑战。原生标准局和冷续均携带它，不能靠关闭语音或移除标记来冒充兼容。
- 新产品共348份C#。增加`ShinGetterBondRunCompatibility`，在Session资格、可靠历史过滤、sgd原有资格守卫三处共用；只接受一个完整St2bs程序集／sealed完整类型／完整模型ID且当前ModelDb已登记为同一类型的被动标记。任意混合、未知或解析失败均拒绝；不主动加载可选程序集，不缓存注册前失败。角色、单人、Standard、保存、daily、Test／NonInteractive及清档守卫不变。正式分支未改。
- 独立语义例外列在白名单`betaBehaviorCompatibilityDifferences`，不伪装成API变换；helper指纹及三处精确替换由parity门禁锁定，其余API／资源规则沿用823。没有改结局消费者、ready、随机、visited、旧823产物或测试driver。
- 测试先以原`Modifiers.Count == 0`语义出现`BS2 passive standard marker blocked`，再通过链接生产源码的961项CPU断言（含36项精确兼容边界）与115项Bridge／111原方法体CPU断言。官方111 ExportRelease编译0警告0错误。测试里的动态类型仅模拟程序集／类型身份，不代表原生BS2加载、冷续或UI验收。
- 运行旧823的补证已证明原生3行对白可点击并进入唯一有效胜利；它不覆盖羁绊自定义故事。新候选仍须独立审核后，才能由唯一自动化执行会话验证正常羁绊和故障回退；不继承旧75层或正式版结论。

## 来源与范围

- 正式唯一来源：已发布`mod-v1.3.0`，解引用提交`26c8da9a907fd9e12d41bf5bf17d24ca5975bfa8`。
- Beta基线：远端`patch/support-111-beta@dce78bf472ca678b8bac042e878489d48014e83c`。独立支线`feat/issue-248-v130-beta-refresh-20261019`；根main、旧Beta worktree及用户存档不变。
- 三方同步正式已批准增量，只有膨胀打击／闪光爆裂冲突；采用正式最新时序，同时保留111的当前CardPlay。没有合并目标线、PR、Tag、发布或共享部署。
- 覆盖issue#206完整异常回退（含96ad P1修复）、issue#254/255打包与动画元数据／绘制准备、issue#257两卡时序、issue#260撤退同步、issue#259冷加载入口。不是旧b2339ece或dce78bf4包的回归。

## 正式Tag对照语义白名单

- `issue248_source_parity.py`严格核对347份产品C#，除下列API归一／原生替代外与正式Tag相同。没有移除Gameplay分支、放松结算或变更数值。
- 77项文件例外（含格式／using差异和109缓存补丁的明确缺省）逐文件SHA／归一指纹与理由见`.github/issue248-refresh-semantic-whitelist.json`。
- API例外：FromCard继续传当前CardPlay；伤害override／Damage／LoseBlock保留111上下文；StartRunLobbyPlayer；GenerateAnimator第二个Creature参数；SignalPlayerChoiceBegun的Player；plural PotionFactory及IEnumerable；事件战canonical输入及CanonicalInstance比较。
- 109专属SavedPropertiesTypeCache补丁不复制；111原生ModelIdSerializationCache负责注册模型属性、numeric IDs／width／hash。这不是删除保存功能。
- 新跨越InitializeRunLobby：111多出players参数；无参数Postfix不声明或消费原方法实参，仍可按唯一方法名绑定。新局／单人冷读档／多人冷读档6项源码接线检查通过。
- 资源仅5个渠道文件例外：manifest、update_history、三语settings_ui。manifest只允许version/min_game_version变更；共同settings键及正式history记录严格等于Tag，只保留额外Beta记录。所有其他tracked工程资源与Tag字节一致。
- README／Workshop三语保留未发布Beta身份及真实旧Beta下载入口；以af93材料为参考，Workshop玩法正文沿用正式Tag最新精简版并加Beta说明。旧记录不改成已发布。

## 官方111输入与只读兼容核对

- 只读官方输入：官方111导出包的`data_sts2_windows_x86_64`目录，由`SHIN_GETTER_STS2_111_BIN`指定；实际私有路径记录在外部回执。
- 官方sts2.dll SHA256 `0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`；MVID `73b63ee0-6c0a-47bb-b0d1-b21f6d94222e`。产品按ExportRelease配置、该官方DLL编译，不把反编译Debug当运行环境。
- 109／111现有CodeGraph定位后回读实际源码；机械审计31个changed-symbol交叉全部reviewed，无未知项。原审计历史保留。
- 实际CLR1209引用、102 Harmony目标／203命名参数、48 scoped动态成员通过；不应用补丁或启动游戏。44属性发现／14往返仅隔离CPU metadata夹具，不证明原生初始化、网络包或玩家存档。
- 当前BS2为Workshop v2.3.0，St2bs DLL SHA `B58AC78FE6CB7C1E771AF483450D7AFB402DB7E9AC7FB5A113970D3844BBC0B2`。CLR只读验证程序集名、SmokeForest继承、public instance ExploreEvents getter返回IReadOnlyList<EventModel>及PickExploreEvent签名；离线调用链仍是候选池→去访问项／强遭遇门槛→原RNG→访问记录→EventRoom。保留原适配代码，不改工坊，不用v2.2.1结果冒充新版本运行通过。

## 打包与门禁

- Beta渠道资源并非完全等同正式Tag，因此不复用正式A71 PCK。独立PCK按现有build_mod_pck／validate_mod_pck链生成；空工程加载946文件、197纹理、89音频、154图集、46 JSON全部通过。
- 首次完整headless导入在76%发生`0xc0000005`，原日志／Windows事件保留，未计为PASS。只补入66个source_md5与源文件相同的既有导入缓存后打包；空工程验包成功。没有重跑失败完整导入或改用户缓存。新生成未跟踪UID/import元数据已归档到证据目录后清理，不进入产品diff。
- 聚焦CPU：issue#206 Session925／Bridge+111原方法体115；issue#257 9；issue#260 33；issue#255元数据109966及渲染等待86；BS2冷加载6；羁绊故障门禁14反例通过。CPU模拟／数学结果不证明GPU、流畅性、鼠标、声音或完整游玩。
- `validate_issue_93.py`、`validate_issue_248.py`、语义白名单和diff-check通过。没有重复完整历史性能矩阵。

## 审核交接

最终提交及四件套大小／SHA／ProductVersion在外部`candidate-receipt.json`冻结；产品实际交接路径通过主开发会话和私有工单提供，不放入公开材料。
主开发先审核该身份及差异，之后自动化会话才可使用新候选进行官方111隔离回归。没有启动游戏、整局、GPU、Steam/Godot共享部署或公开发布，不能继承旧75层／旧Debug回归结论。

```powershell
$env:SHIN_GETTER_STS2_111_BIN='<official111-data_sts2_windows_x86_64>'
python shin-getter-mod-godot/tools/validate_issue_93.py
python shin-getter-mod-godot/tools/validate_issue_248.py
python shin-getter-mod-godot/tools/audit_issue248_refresh.py
dotnet run --project tests/Issue206EventReturn/Issue206EventReturn.csproj -p:NativeSourceRoot='<read-only-111-source>'
```
