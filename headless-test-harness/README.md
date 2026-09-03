# Slay the Spire 2 Headless-Test Harness

This top-level tool is versioned with ShinGetterMod while remaining logically
and physically isolated from the production mod dependency graph. It contains:

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

See `python/agent-harness/tests/TEST.md` for the D0-D6 acceptance contract and
`docs/implementation-v0.2.md` for build, staging, and PoC instructions.
