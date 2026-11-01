# issue#238 保存恢复边界离线探针

先构建待审核DLL，再传入候选DLL与正式游戏的`Steamworks.NET.dll`路径运行本项目：

```powershell
dotnet run --project tests/Issue238SaveBoundary/Issue238SaveBoundary.csproj -p:ModAssemblyPath="候选/ShinGetterMod.dll" -p:SteamworksAssemblyPath="游戏/Steamworks.NET.dll"
```

使用正式109的原生序列化器、房间、RNG、事件初始/完成生命周期，以及实际生产恢复门和保存验证器。检查可选恢复戳的旧格式兼容、未知字段保留、身份隔离、完整run读回差异、缓存候选不重抽、RNG位置、巨石完成清理及Saved前后失败分界。

夹具仅提供无场景的run/player/model对象、共享服务初始化需要的5件原生遗物目录、选项/提示的本地化显示替身和注入式保存故障。没有调用游戏启动、地图行走、实际SaveManager写盘或Continue；不构成生产自动保存恢复、画面、输入或发布验收。真实生产失败链须用新隔离输入另验，旧证据不改。

现行默认静态门禁`shin-getter-mod-godot/tools/validate_issue_238.py`检查独立快照、明确v1戳、完整身份门、限域Hook、完整run读回与Saved分界。`--self-test-save-boundary`仅运行该源码合同和13种不安全变更负例，包含改标活房间、null保存、缺失v1戳、owner/坐标/history/base/服务隔离削弱、全局Hook跳过、未知版本放行、部分读回或多排除字段、Saved分界倒置；这些保护仍必须拒绝。旧“任何MarkPreFinished都禁止”的断言已由这些精确门禁替代，默认其他内容/原生ID/三语剧情门禁保留。

静态门禁和CLR探针各自有明确范围，均不改变旧候选真实生产自动保存FAIL结论，也不批准运行、合并或发布。
