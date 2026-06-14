# sts2-shin-getter-mod

《杀戮尖塔 2》真盖塔角色模组，当前处于开发阶段。

## 本地依赖

以下文件不会提交到版本库，需要从本机游戏环境恢复：

- `lib/sts2.dll`
- `lib/0Harmony.dll`
- `addons/` 下由游戏或编辑器提供的 FMOD、Spine、Sentry 等插件

## 构建

项目使用 Godot 4.5.1 Mono 和 .NET 9：

```powershell
dotnet build .\ShinGetterMod.csproj
```

构建后的 DLL、PDB 和导出的 PCK 位于 `build/`，该目录不纳入版本管理。

## 构建并部署测试包

本机开发环境可用脚本自动编译 DLL、通过 Godot 命令行导出 PCK，并将 DLL、PCK、JSON 部署到测试目录：

```powershell
.\build-and-deploy.ps1
```

默认测试目录为 `E:\Work\Godot\Godot_v4.5.1-stable_mono_win64\mods\ShinGetterMod`。Godot 导出日志写入 `build/godot-export.log`；路径不同时可通过脚本参数覆盖。

## 项目资料

设计文档、开发文档及卡牌/状态看板存放在作者的 Obsidian 库中，不随代码仓库发布。
