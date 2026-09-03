# Slay the Spire 2 Headless-Test Harness

This top-level tool is versioned on a ShinGetterMod feature branch while
remaining logically isolated from the production mod dependency graph. The
implemented v0.2 scope is D0-D6: authenticated process control, canonical state
capture, exact single-player card completion, and explicit card-choice
continuation against the real 0.111 runtime. The release gate also proves
in-flight reconnect, production idempotency, evidence sealing, and graceful
process cleanup against that runtime.

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

Live control is fail-closed unless the caller repeats `--protected-root` for
every source, Steam, Workshop, and production deployment tree that must remain
read-only. Every declared root must already exist and be an absolute directory.
The guard rejects symlink and Windows junction ancestors both before and after
path resolution, including newly created runtime paths.

The companion is a separate test-only mod. It is never referenced by
`shin-getter-mod-godot/ShinGetterMod.csproj`, never included in the production
four-file package, and requires both `STS2_TEST_ENABLE=1` and an authenticated
per-instance broker handshake before accepting commands.

The active connection owns a bounded non-blocking critical queue; replay uses a
separate rolling store. Reconnect inside the retained window resumes the same
process epoch, while an older cursor fails with `E_RESUME_WINDOW_EXPIRED`.
Request IDs are keyed by the complete canonical request payload (apart from
volatile transport metadata): an in-flight duplicate does not execute twice, a
finished duplicate replays its first terminal while that payload remains in the
256-entry LRU, and different content returns `E_IDEMPOTENCY_CONFLICT` without
replacing the original ledger entry. Request-ID/digest tombstones remain for the
whole companion process epoch. Once a terminal payload leaves the LRU, an exact
retry fails with `E_IDEMPOTENCY_WINDOW_EXPIRED`; it is never executed again.

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

The real-runtime pytest gate is intentionally opt-in. It requires
`STS2_HEADLESS_RUNTIME_RELEASE_GATE=1`, an absolute external profile through
`STS2_HEADLESS_RUNTIME_PROFILE`, and `CLI_ANYTHING_FORCE_INSTALLED=1`. The gate
builds and stages a fresh TEST-ONLY companion, runs PoC 0/1/1b, verifies the
immutable evidence manifest, and treats a missing profile or cleanup failure as
a hard failure. Ordinary unit-test runs skip it explicitly.

Save/load/replay, multiplayer control, issue #191 automation, animation or
pixel output, virtual time, H1 rendering, and the 0.109 adapter are explicitly
outside this branch.

See `python/agent-harness/tests/TEST.md` for the D0-D6 acceptance contract and
`docs/implementation-v0.2.md` for build, staging, and PoC instructions.
