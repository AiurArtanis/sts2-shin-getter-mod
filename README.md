# 真盖塔模组

[English](README_EN.md) | [日本語](README_JP.md)

![真盖塔模组角色选择界面](animations/character_select/shin_getter/character_select_shin_getter_bg.png)

**三颗心脏，一台盖塔，无限进化。**

让真盖塔穿越时空登上高塔。切换一号机的爆发、二号机的高速战术与三号机的钢铁防线，最终唤醒真盖塔龙，用盖塔线吞没高塔。这里不是换一张角色皮肤，而是一套围绕变形、卡组与演出共同运转的玩法型角色 Mod。

> 当前版本 `v0.9.41` · 最低游戏版本 `0.106.1` · Godot `4.5.1 Mono` · .NET `9` · 简体中文 / English / 日本語

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

`v0.9.41` 当前注册内容包括：

- **72 张卡牌**，覆盖四种形态与多套核心机制
- **10 个遗物**、**3 瓶药水**、**2 个附魔**
- **1 个专属事件**与专属先古对话
- 中、英、日三语本地化
- DLL、PCK 与 JSON 组合加载的完整角色 Mod

## 🃏 代表性卡面

<p align="center">
  <img src="images/packed/card_single/shin_getter/s_g_c_shin_form_card.png" width="30%" alt="真形态卡牌成品图" />
  <img src="images/packed/card_single/shin_getter/s_g_c_stoner_sunshine_card.png" width="30%" alt="闪光爆裂卡牌成品图" />
  <img src="images/packed/card_single/shin_getter/s_g_c_saint_dragon_roar_card.png" width="30%" alt="圣龙咆哮卡牌成品图" />
</p>

## 🚀 快速上手

创意工坊版本发布后：订阅 Mod，在游戏的 Mod 菜单中启用它，然后新开一局并选择**真盖塔**角色即可开始。首次游玩时，优先观察形态专属卡的高亮提示，围绕当前形态建立节奏，再逐步尝试上面的构筑路线。

当前仍处于公开发布前开发阶段。如需参与测试或研究实现，可按下方方式从源码构建；版本、数值、内容数量和与后续游戏版本的兼容性仍可能调整，暂不提供稳定安装包或存档兼容承诺。

## 🔭 后续方向

- 更多事件与同世界观敌人
- 新 BD 流派「宿命流」
- 持续优化角色动画与战斗演出

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
dotnet build .\ShinGetterMod.csproj -c Debug
```

编译后的 DLL 与 PDB 会复制到被忽略的 `build/` 目录。

### 构建、验证并部署测试包

仓库提供的 Godot/PowerShell 构建脚本会依次编译 DLL、导出 PCK、验证资源、复制 DLL/PCK/JSON，并进行一次无界面的游戏加载验证：

```powershell
.\build-and-deploy.ps1 `
  -Configuration Debug `
  -GodotExe "D:\Godot\Godot_v4.5.1-stable_mono_win64_console.exe" `
  -GameProject "D:\Games\SlayTheSpire2" `
  -DeployDirectory "D:\Games\SlayTheSpire2\mods\ShinGetterMod"
```

路径应替换为你的本机环境。脚本成功后，部署目录会包含：

- `ShinGetterMod.dll`
- `ShinGetterMod.pck`
- `ShinGetterMod.json`

## 项目结构

- `src/`：卡牌、能力、遗物、药水、附魔、事件、补丁与运行时代码
- `scenes/`、`animations/`、`images/`、`materials/`、`shaders/`、`audio/`：Godot 场景与视听资源
- `ShinGetterMod/`：模组数据与本地化资源
- `tools/validate-mod-resources.gd`：导出包资源完整性检查
- `ShinGetterMod.json`：模组清单、版本与最低游戏版本

## 参与开发与反馈

公开发布前，仓库仍以集中开发和测试为主。提交问题时，请尽量附上游戏版本、Mod 版本、复现步骤和相关日志；提交代码前，请保持改动范围明确，并至少运行对应的 C# 构建。涉及 Godot 资源的改动应再通过完整的构建、资源验证与加载验证。

请勿提交本地游戏依赖、`addons/`、`build/` 产物或个人测试脚本。

## 许可与素材说明

本项目是非官方同人 Mod，与《杀戮尖塔 2》及“真盖塔”相关的名称、角色和原作素材归各自权利人所有。

仓库目前尚未附带开源许可证。在正式开放再分发与贡献前，将补齐代码许可证、第三方素材归属及使用边界；在此之前，请勿默认仓库内容已获得复制、修改或再分发授权。
