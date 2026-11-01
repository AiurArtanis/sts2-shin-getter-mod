# issue#238 保存恢复边界离线探针

先构建待审核DLL，再传入候选DLL与正式游戏的`Steamworks.NET.dll`路径运行本项目：

```powershell
dotnet run --project tests/Issue238SaveBoundary/Issue238SaveBoundary.csproj -p:ModAssemblyPath="候选/ShinGetterMod.dll" -p:SteamworksAssemblyPath="游戏/Steamworks.NET.dll"
```

使用正式109的原生序列化器、房间、RNG、事件初始/完成生命周期，以及实际生产恢复门和保存验证器。检查可选恢复戳的旧格式兼容、未知字段保留、身份隔离、完整run读回差异、缓存候选不重抽、RNG位置、巨石完成清理及Saved前后失败分界。

夹具仅提供无场景的run/player/model对象、共享服务初始化需要的5件原生遗物目录、选项/提示的本地化显示替身和注入式保存故障。没有调用游戏启动、地图行走、实际SaveManager写盘或Continue；不构成生产自动保存恢复、画面、输入或发布验收。真实生产失败链须用新隔离输入另验，旧证据不改。
