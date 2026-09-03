# Contributing to Shin Getter Mod

[简体中文](#简体中文) | [English](#english) | [日本語](#日本語)

Thank you for helping improve Shin Getter Mod. This repository contains an
unofficial fan-made mod, so contributions must be technically reviewable and
must also respect the rights attached to game and third-party assets.

## 简体中文

### 开始之前

- 先搜索现有 Issue，避免重复提交。
- 修复明确的小问题可以直接提交 PR；新增功能、平衡性调整、大规模重构或资源替换，请先创建 Issue 并等待维护者确认方向。
- 安全漏洞不要公开披露，请按 [安全政策](SECURITY.md) 私密报告。
- 每个 PR 聚焦一个 Issue 或一个可独立审阅的目标，避免夹带无关重构、格式化或生成文件变化。

### 分支与 Issue 流程

- `main` 是正式目标线；兼容测试分支只在维护者明确要求时提交到对应的 `patch/support-*-beta`。
- 从目标线创建独立功能分支，不直接向受保护分支推送。
- `main` 和长期支持分支通过 PR、squash merge 和线性历史集成。
- PR 中使用 `Refs #123` 关联 Issue。除非维护者明确要求，不要使用 `Closes`、`Fixes` 或 `Resolves`：本项目会在合并、构建、部署并由 Artanis 完成游戏内验收后才关闭 Issue。

### 本地环境

需要 Godot `4.5.1 Mono`、.NET SDK `9`，以及与目标游戏版本匹配的本地依赖。以下文件来自本地游戏或开发环境，不得提交：

- `shin-getter-mod-godot/lib/sts2.dll`
- `shin-getter-mod-godot/lib/0Harmony.dll`
- `shin-getter-mod-godot/addons/`
- `.godot/`、`Godot/`、`build/`、`bin/`、`obj/`、PCK、日志、存档及个人部署脚本

游戏源码镜像只用于阅读和兼容性核对，不得在本仓库的贡献中修改或重新分发。

### 实现要求

- 保持改动最小、边界清晰，并沿用仓库现有 C#、Godot 和资源组织方式。
- 玩法改动应覆盖所有适用玩家、Owner、多人同步、保存/读取和异步完成边界。
- 玩家可见文本应同步更新简体中文、英文和日文，并保留占位符、富文本标记和 JSON 结构。
- 二进制或视听资源必须说明来源、授权和生成过程。不要提交从游戏或其他作品中提取且无权再分发的内容。
- 不要提交本机绝对路径、凭据、私人信息、未经脱敏的日志或存档。

### 验证

在 `shin-getter-mod-godot` 目录准备好本地依赖后，至少运行与改动相关的构建和回归脚本：

```powershell
dotnet build .\ShinGetterMod.csproj --no-restore -v:minimal

Get-ChildItem .\tools\validate_*.py | ForEach-Object {
    python $_.FullName
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

python .\tools\build_character_sprite_sheets.py --check
git diff --check
```

首次构建尚未还原依赖时，可先执行 `dotnet restore`。资源、场景、动画或导入设置发生变化时，还应导出 PCK、运行资源门禁，并完成隔离加载验证。无法执行某项验证时，请在 PR 中明确写出原因，不要把“未运行”写成“通过”。

### PR 应包含

- 问题、预期行为和根因说明。
- 具体改动及刻意未改动的边界。
- 实际运行的命令与结果；必要时提供脱敏日志或截图。
- 目标游戏版本、正式/Beta适用范围、多人和存档影响。
- 新增资源的来源与再分发依据。

维护者可能在最终目标线上补做组合构建、PCK、初始化和部署。PR 被合并不代表 Issue 已完成验收。

## English

### Before you start

- Search existing issues first.
- Small, well-scoped fixes may go directly to a PR. Open an issue before new features, balance changes, broad refactors, or asset replacements.
- Report vulnerabilities privately under the [Security Policy](SECURITY.md).
- Keep each PR focused on one issue or one independently reviewable goal.

### Branch and issue workflow

- `main` is the production target. Target a `patch/support-*-beta` branch only when a maintainer explicitly requests it.
- Work on a separate branch. Protected target branches accept changes through PRs with squash merging and linear history.
- Reference issues with `Refs #123`. Do not use `Closes`, `Fixes`, or `Resolves` unless a maintainer asks you to: issues remain open until integration, deployment, and in-game acceptance are complete.

### Local setup and implementation

- Use Godot `4.5.1 Mono`, .NET SDK `9`, and local assemblies matching the target game version.
- Never commit game assemblies, `addons/`, generated Godot state, build output, PCKs, saves, logs, credentials, personal paths, or deployment scripts.
- Treat game source mirrors as read-only references; do not modify or redistribute them.
- Follow existing C# and Godot patterns, keep scope narrow, and account for ownership, multiplayer synchronization, persistence, and async completion where relevant.
- Update Simplified Chinese, English, and Japanese together for player-facing text.
- Document the source and redistribution rights for every new binary or audiovisual asset.

### Validation and PR evidence

Run the build, relevant `tools/validate_*.py` scripts, sprite-sheet checks, and `git diff --check` as applicable. Resource changes also require PCK/resource validation and an isolated load check. List exactly what you ran and clearly mark anything you could not run.

Explain the problem, root cause, behavior change, intentional non-goals, target game branch, multiplayer/save impact, and asset provenance in the PR. A merge does not by itself close the related issue.

## 日本語

### 作業を始める前に

- 既存のIssueを検索し、重複を避けてください。
- 小規模で明確な修正は直接PRにできます。新機能、バランス変更、大規模なリファクタリング、素材の差し替えは、先にIssueで方針を確認してください。
- セキュリティ上の問題は [セキュリティポリシー](SECURITY.md) に従って非公開で報告してください。
- 1つのPRは、1つのIssueまたは独立してレビューできる目的に限定してください。

### ブランチとIssue

- `main` は正式版向けです。`patch/support-*-beta` はメンテナーから明示的な指示がある場合のみ対象にしてください。
- 保護された対象ブランチへ直接pushせず、作業ブランチからPRを作成します。統合はsquash mergeと線形履歴を使用します。
- Issueの関連付けには `Refs #123` を使ってください。メンテナーの指示がない限り `Closes`、`Fixes`、`Resolves` は使わないでください。Issueは統合、配置、ゲーム内確認が完了した後に閉じます。

### 実装と検証

- Godot `4.5.1 Mono`、.NET SDK `9`、対象ゲーム版に対応するローカルDLLが必要です。
- ゲームDLL、`addons/`、Godot生成状態、ビルド成果物、PCK、セーブ、ログ、認証情報、個人用パスや配置スクリプトはコミットしないでください。
- ゲームソースのミラーは読み取り専用です。変更や再配布をしないでください。
- 既存のC#/Godot構成に合わせ、必要に応じてOwner、マルチプレイ同期、保存、非同期完了を確認してください。
- プレイヤー向け文言は簡体字中国語・英語・日本語を同時に更新してください。
- バイナリ、画像、音声を追加する場合は、出典と再配布権限を明記してください。

該当するビルド、`tools/validate_*.py`、スプライトシート確認、`git diff --check`を実行し、実行できなかった項目はPRに明記してください。リソース変更にはPCK/リソース検証と隔離ロード確認も必要です。PRのマージだけではIssueの受け入れ完了にはなりません。
