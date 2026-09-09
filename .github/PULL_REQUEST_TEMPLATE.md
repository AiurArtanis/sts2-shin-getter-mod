## Summary / 改动摘要 / 変更概要

<!-- Explain the problem and the resulting behavior. 请说明问题与改动后的行为。 -->

Refs #

> Use `Refs #123` by default. Do not use `Closes`, `Fixes`, or `Resolves` unless a maintainer explicitly asks for automatic closure. / 默认使用 `Refs #123`；未经维护者明确要求，不要让 PR 自动关闭 Issue。

## Scope / 边界 / 対象範囲

- Target game version or branch / 目标游戏版本或分支：
- Intentional non-goals / 刻意未改动的内容：
- Multiplayer or save impact / 多人及存档影响：

## Validation / 验证 / 検証

<!-- List the exact commands and results. State why any required check was not run. -->

- [ ] Relevant C# build completed with result recorded
- [ ] Relevant `tools/validate_*.py` checks passed
- [ ] `python tools/build_character_sprite_sheets.py --check` passed, or not applicable
- [ ] `git diff --check` passed
- [ ] Resource/PCK and isolated-load checks completed, or explicitly marked for maintainer follow-up

## Contribution checklist / 提交检查 / チェックリスト

- [ ] This PR has one focused, reviewable purpose.
- [ ] Player-facing text is updated in Simplified Chinese, English, and Japanese where applicable.
- [ ] No game DLLs, `addons/`, generated state, build output, saves, private paths, credentials, or unredacted logs are included.
- [ ] Every new image, audio, binary, or third-party asset has documented provenance and redistribution permission.
- [ ] Screenshots/log excerpts are redacted and attached when they materially help review.
- [ ] The related Issue remains open for post-merge build, deployment, and in-game acceptance unless a maintainer says otherwise.
