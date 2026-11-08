# issue#238 保存恢复边界离线探针

先构建待审核DLL，再传入候选DLL与正式游戏的`Steamworks.NET.dll`路径运行本项目：

```powershell
dotnet run --project tests/Issue238SaveBoundary/Issue238SaveBoundary.csproj -p:ModAssemblyPath="候选/ShinGetterMod.dll" -p:SteamworksAssemblyPath="游戏/Steamworks.NET.dll"
```

使用正式109的原生序列化器、房间、RNG、事件初始/完成生命周期，以及实际生产恢复门和保存验证器。检查可选恢复戳的旧格式兼容、未知字段保留、身份隔离、完整run读回差异、缓存候选不重抽、RNG位置、巨石完成清理及Saved前后失败分界。新增地图保存域检查实际metadata缓存、引用归属、八路异步序列化并发、普通保存字节不变、嵌套异常、取消、捕获ExecutionContext/延迟Task撤销和地图/池/RNG/房间四路径诊断。

夹具提供无场景的run/player/model对象、共享服务初始化需要的5件原生遗物目录、选项/提示显示替身及注入式保存故障。新增探针实际运行`SetUpSavedSinglePlayer`的patched MoveNext → `IncrementNumReloads` → `RunSaveManager.SaveRun(SerializableRun)`，保存后在`InitializeShared`前主动中止。`PeerInputSynchronizer`构造被跳过，以免独立CLR访问Godot logger；后续网络/场景初始化不执行。存储为内存DispatchProxy，绝不读写玩家磁盘存档。ToSave捕获使用明确的DTO直调夹具，未声称执行场景相关的ToSave完整方法。未启动游戏、走地图或完成真实Continue，不构成生产自动保存恢复、画面、输入或发布验收。

现行默认静态门禁`shin-getter-mod-godot/tools/validate_issue_238.py`检查独立快照、明确v1戳、完整身份门、限域Hook、完整run读回与Saved分界。`--self-test-save-boundary`运行该源码合同和31种不安全变更负例，包括旧23种边界和本轮native history等待、缓存重入/Skip保存、展示保存、选择结果失败门及作用域撤销。旧“任何MarkPreFinished都禁止”的断言已由精确门禁替代，默认其他内容/原生ID/三语剧情门禁保留。

R6新增探针安装实际native history/选择任务补丁，真正执行原生`SaveEventOptionToHistory`。以明确候选发现夹具及保存/选择TCS检查history顺序、展示时保存而非等选完、保存未完成不放行结果、缓存重入、native取消、失败清理/不自动重试与捕获上下文撤销。未执行Godot候选界面或真实MarkCardAsSeen，未证明磁盘SaveAndQuit/Continue；完整限制见`art_sources/issue238-v140-20261101/review-r6-owned-interaction-boundary-20261102.md`。

`ShinGetterEventSaveVerification.DiagnosticSink`供隔离驱动接收`Comparison`：真正pre-write和direct readback的`NativeJson`/`StableJson`字节、显式`MapFlags`及完整`Differences`路径。驱动应只写自身evidence目录，日志/工单只给路径、长度及SHA，不内嵌原生编码字段。sink失败不改变事务裁定。

静态门禁和CLR探针各自有明确范围，均不改变旧候选真实生产自动保存FAIL结论，也不批准运行、合并或发布。
