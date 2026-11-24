# issue#238 保存恢复边界离线探针

先构建待审核 DLL，再传入候选 DLL 路径和正式 `0.111.1` 的程序集目录运行本项目：

```powershell
dotnet run --project tests/Issue238SaveBoundary/Issue238SaveBoundary.csproj "-p:ModAssemblyPath=$modDll" "-p:Sts2ReferenceDir=$gameAssemblies"
$env:STS2_REFERENCE_DIR = $gameAssemblies
python shin-getter-mod-godot/tools/validate_issue_238.py
```

使用正式 `0.111.1` 的原生序列化器、房间、RNG、事件初始/完成生命周期，以及实际恢复门和保存验证器。检查无恢复戳旧格式、未知字段、身份隔离、完整 run 读回、缓存候选不重抽、巨石完成清理及 Saved 前后失败分界。新恢复戳保留 RNG 的四个状态字及计数；109 已写入恢复戳但缺少完整状态的第四批事件检查点会明确拒绝恢复，不能用重抽替代。地图保存域继续验证实际 metadata 缓存、对象归属、八路异步并发、普通保存字节不变、嵌套异常、取消和已捕获上下文的撤销。

夹具提供无场景的run/player/model对象、共享服务初始化需要的5件原生遗物目录、选项/提示显示替身及注入式保存故障。新增探针实际运行`SetUpSavedSinglePlayer`的patched MoveNext → `IncrementNumReloads` → `RunSaveManager.SaveRun(SerializableRun)`，保存后在`InitializeShared`前主动中止。`PeerInputSynchronizer`构造被跳过，以免独立CLR访问Godot logger；后续网络/场景初始化不执行。存储为内存DispatchProxy，绝不读写玩家磁盘存档。ToSave捕获使用明确的DTO直调夹具，未声称执行场景相关的ToSave完整方法。未启动游戏、走地图或完成真实Continue，不构成生产自动保存恢复、画面、输入或发布验收。

现行默认静态门禁检查独立快照、完整身份门、限域 Hook、完整 run 读回与 Saved 分界，并以实际 `ModelDb.GetId(Type)` 核对 15 个原生事件 ID。`--self-test-save-boundary`单独检查保存合同及 33 个负例。旧的可选 `--probe` 是历史 109 CLR 探针，未作为 `0.111.1` 的验证结果。

R6新增探针安装实际native history/选择任务补丁，真正执行原生`SaveEventOptionToHistory`。以明确候选发现夹具及保存/选择TCS检查history顺序、展示时保存而非等选完、保存未完成不放行结果、缓存重入、native取消、失败清理/不自动重试与捕获上下文撤销。未执行Godot候选界面或真实MarkCardAsSeen，未证明磁盘SaveAndQuit/Continue；完整限制见`art_sources/issue238-v140-20261101/review-r6-owned-interaction-boundary-20261102.md`。

`ShinGetterEventSaveVerification.DiagnosticSink`供隔离驱动接收`Comparison`：真正pre-write和direct readback的`NativeJson`/`StableJson`字节、显式`MapFlags`及完整`Differences`路径。驱动应只写自身evidence目录，日志/工单只给路径、长度及SHA，不内嵌原生编码字段。sink失败不改变事务裁定。

静态门禁和CLR探针各自有明确范围，均不改变旧候选真实生产自动保存FAIL结论，也不批准运行、合并或发布。
