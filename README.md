# 真盖塔模组

[English](README_EN.md) | [日本語](README_JP.md)

![真盖塔模组角色选择界面](shin-getter-mod-godot/animations/character_select/shin_getter/character_select_shin_getter_bg.png)

**三颗心脏，一台盖塔，无限进化。**

让真盖塔穿越时空登上高塔。切换一号机的爆发、二号机的高速战术与三号机的钢铁防线，最终唤醒真盖塔龙，用盖塔射线吞没高塔。这里不是换一张角色皮肤，而是一套围绕变形、卡组与演出共同运转的玩法型角色 Mod。

> 当前版本 `v1.0.2` · 最低游戏版本 `0.106.1` · Godot `4.5.1 Mono` · .NET `9` · 简体中文 / English / 日本語

[下载 v1.0.2](https://github.com/AiurArtanis/sts2-shin-getter-mod/releases/tag/mod-v1.0.2) · [提交问题](https://github.com/AiurArtanis/sts2-shin-getter-mod/issues)

## ⚡ 战斗核心

- **四种形态，一套角色。** 一号机专注活力爆发；二号机获得额外能量与抽牌，但格挡减半；三号机以覆甲和防杀反击守住阵线；真盖塔龙兼容三种形态奖励。
- **变形就是决策。** 形态专属卡会提示当前可用的强势动作。通过位移、变形牌和相关遗物，可以把换挡编入每回合的进攻节奏。
- **从卡牌到演出都围绕真盖塔打造。** 定制卡框、角色动画、VFX、语音、处刑曲与角色选择画面共同构成完整体验。
- **融入高塔，而不是停留在换皮。** 除完整卡池外，还包含专属遗物、药水、附魔、事件和先古对话。

## 🧭 新手构筑

- **活力爆发：** 累积活力后，以「热血」「俯冲打击」等牌打出高伤害终结。
- **消耗循环：** 用带消耗的卡牌触发「盖塔钩爪」的免费伤害；有「部件交换」后可回收关键组件，二号机能进一步放大输出。
- **钢铁反击：** 三号机叠覆甲、构筑格挡，在敌人回合用防杀反击压低风险。
- **变形连锁：** 围绕频繁变形、移动变形和「天选之子」组织卡组，让每次切换都转化为资源或防御。

气力会支撑部分强力效果；进化与辐射则让后期的成长路线继续分岔。先抓住一条主线，再让其他机制为它服务，通常会比平均铺开更可靠。

## 📦 内容一览

`v1.0.2` 当前注册内容包括：

- **72 张卡牌**，覆盖四种形态与多套核心机制
- **10 个遗物**、**3 瓶药水**、**2 个附魔**
- **1 个专属事件**与专属先古对话
- 中、英、日三语本地化
- DLL、PCK 与 JSON 组合加载的完整角色 Mod

## 🃏 全卡卡面

![真盖塔全卡卡面马赛克预览](art/shin_getter_face_card_mosaic_enhanced.png)

## 🚀 快速上手

### 安装发布版

1. 从 [GitHub Release](https://github.com/AiurArtanis/sts2-shin-getter-mod/releases/tag/mod-v1.0.2) 下载 `ShinGetterMod.pck`、`ShinGetterMod.dll` 与 `ShinGetterMod.json`。
2. 在游戏的模组加载目录中创建 `ShinGetterMod` 子目录，并将三个文件放在一起。
3. 启动游戏，从主菜单进入**设置 → 模组设置**，在**已下载的模组**中启用“真盖塔模组”。
4. 确认加载提示后完全退出并重新启动游戏。新开一局，选择**真盖塔**即可开始。

首次游玩时，优先观察形态专属卡的高亮提示，围绕当前形态建立节奏，再逐步尝试上面的构筑路线。

### 创意工坊

创意工坊版本发布后，也可以按下面步骤启用：

1. 订阅 Mod 后启动游戏；从主菜单进入**设置**，选择**模组设置**。
2. 在**已下载的模组**列表中找到“真盖塔模组”，勾选它的启用框。
3. 出现“是否要加载模组？”提示时，确认来源可信后选择**加载模组**，然后完全退出并重新启动游戏。启用或关闭模组都要在重启后才生效。
4. 新开一局，选择**真盖塔**角色即可开始。

`v1.0.0` 是首个公开发布版本。后续游戏更新可能影响兼容性；遇到问题时，请附上游戏版本、Mod 版本、复现步骤和相关日志。

## 🗺 后续路线图

- [ ] 更多事件
- [ ] 更多超级机器人大战元素
- [ ] 同世界观敌人
- [ ] 新 BD 流派「宿命流」
- [ ] 优化动画表现
- [ ] 多人模式

## 从源码构建

### 环境要求

- 《杀戮尖塔 2》`0.106.1` 或更高版本
- Godot `4.5.1 Mono`
- .NET SDK `9`
- 本机可供 Godot 加载验证的游戏工程目录

以下依赖来自本地游戏或开发环境，不纳入版本管理：

- `lib/sts2.dll`
- `lib/0Harmony.dll`
- `addons/` 中由游戏或编辑器提供的 FMOD、Spine、Sentry 等插件

准备依赖后，编译 C# 项目：

```powershell
cd .\shin-getter-mod-godot
dotnet build .\ShinGetterMod.csproj -c Debug
```

编译后的 DLL 与 PDB 会复制到被忽略的 `build/` 目录。

### 导出测试包

使用 Godot 的 `Windows Desktop` 导出预设生成 PCK：

```powershell
godot --headless --quit --path . --export-pack "Windows Desktop" .\build\ShinGetterMod.pck
```

将以下文件放入本地游戏的同一个 Mod 目录：

- `ShinGetterMod.dll`
- `ShinGetterMod.pck`
- `ShinGetterMod.json`

Godot 资源改动还应在本地游戏工程中运行 `tools/validate-mod-resources.gd`，并完成一次无界面加载验证。仓库不跟踪个人部署脚本或本机路径配置。

## 项目结构

- `shin-getter-mod-godot/`：完整 Godot/C# 模组工程
- `shin-getter-mod-godot/src/`：卡牌、能力、遗物、药水、附魔、事件、补丁与运行时代码
- `shin-getter-mod-godot/scenes/`、`animations/`、`images/`、`materials/`、`shaders/`、`audio/`：Godot 场景与视听资源
- `shin-getter-mod-godot/ShinGetterMod/`：模组数据与中英日本地化资源
- `art/`：完整卡面 PNG 与全卡马赛克画廊
- `workshop/`：创意工坊发布文案与相关材料

## 参与开发与反馈

提交问题时，请尽量附上游戏版本、Mod 版本、复现步骤和相关日志；提交代码前，请保持改动范围明确，并至少运行对应的 C# 构建。涉及 Godot 资源的改动应再通过完整的构建、资源验证与加载验证。

请勿提交本地游戏依赖、`addons/`、`build/` 产物或个人测试脚本。

## 许可与素材说明

本项目是非官方同人 Mod，与《杀戮尖塔 2》及“真盖塔”相关的名称、角色和原作素材归各自权利人所有。

本仓库中的原创代码以 [MIT 许可证](LICENSE) 发布。该许可证不授予《杀戮尖塔 2》、“真盖塔”或其他第三方素材的复制、改编、再分发权限；使用者仍须自行确认相关权利与素材来源。
