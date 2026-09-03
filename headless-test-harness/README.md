# Slay the Spire 2 Headless-Test Harness

This top-level tool is versioned on a ShinGetterMod feature branch while
remaining logically isolated from the production mod dependency graph. The
implemented v0.2 scope is D0-D6: authenticated process control, canonical state
capture, exact single-player card completion, and explicit card-choice
continuation against the real 0.111 runtime.

It contains:

- the imported CLI-Anything Python harness under `python/agent-harness`;
- the TEST-ONLY companion source under `bridge/Sts2HeadlessTestBridge`;
- shared JSON Schemas and cross-language golden fixtures;
- small, redacted fixtures and implementation documentation.

The harness does not modify either reverse-engineered game tree. Runtime
sessions, logs, user data, saves, replays, screenshots, package staging, and
evidence are created under an external runtime root (by default under
`%LOCALAPPDATA%/cli-anything/slaythespare2-111-beta/sessions`). Paths inside this
Git repository, either game source tree, Steam, Workshop, and the production mod
directory are rejected as writable runtime roots.

The companion is a separate test-only mod. It is never referenced by
`shin-getter-mod-godot/ShinGetterMod.csproj`, never included in the production
four-file package, and requires both `STS2_TEST_ENABLE=1` and an authenticated
per-instance broker handshake before accepting commands.

The live command path is:

```text
installed CLI -> per-session broker -> authenticated per-instance pipe
              -> TEST-ONLY companion -> Godot main thread -> real game API
```

v0.2 supports broker-owned process start/status/stop, canonical snapshots,
`run.new`, `run.status`, allowlisted `console.exec` for `fight <encounter-id>`,
`combat.status`, `combat.add_card`, typed `combat.play_card`, `choice.list`, and
`choice.select`. Card completion is correlated to the exact `PlayCardAction`
reference and may be awaited through `queue_settled`; no fixed sleep or nearest
action heuristic is used.

Save/load/replay, multiplayer control, issue #191 automation, animation or
pixel output, virtual time, H1 rendering, and the 0.109 adapter are explicitly
outside this branch.

See `python/agent-harness/tests/TEST.md` for the D0-D6 acceptance contract and
`docs/implementation-v0.2.md` for build, staging, and PoC instructions.
