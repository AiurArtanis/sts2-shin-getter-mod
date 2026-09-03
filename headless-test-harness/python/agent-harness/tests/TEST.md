# Test Plan and Results

This plan is the executable acceptance contract for the in-repository v0.2
headless-test harness. Tests are written before each D0-D6 implementation slice.
The reverse-engineered game trees are read-only inputs; every writable runtime
fixture must use a disposable directory outside the Git worktree.

## Imported 0.1.0 baseline

Verified before implementation on 2026-09-03:

- [x] The filtered import contains exactly 14 files.
- [x] Canonical aggregate SHA-256 is
  `E6452AB14B7978719A039C2E340188628431FB7EA20C0AC7E61C4FB91B64B91D`.
- [x] Existing command groups remain source-compatible.
- [x] Existing baseline result remains 10 passed before the v0.2 changes.

## D0-D6 active test matrix

| Test file | Minimum | Slice | Required coverage |
|---|---:|---|---|
| `test_core.py` | 24 | D0-D3 | Legacy commands, locked session writes, runtime-root guards, argv construction, package provenance |
| `test_protocol.py` | 36 | D0-D3 | Golden envelopes, framing limits, bidirectional HMAC transcripts, idempotency, duplex in-flight requests, mutation lane, overflow latch, error codes |
| `test_process.py` | 24 | D1-D3 | Session layout, broker identity, lifecycle transitions, exact PID checks, monotonic deadlines, reconnect window, broker-exit no-adoption, isolation sentinels |
| `test_broker.py` | 10 | D3-D6 | Long-lived lease, secret-free control pipe, component/game ownership, runtime multiplexing, Job Object cleanup, evidence finalization |
| `test_state_diff.py` | 30 | D4 | Epoch-scoped handles, canonical JSON/hash, ordered versus set-like arrays, tolerance, redaction, RNG-side-effect detection, bounded JSON Pointer diff |
| `test_full_e2e.py` | 18 | D3-D6 | Installed CLI from arbitrary cwd, broker/component test host, PoC 0 handshake, PoC 1 no-choice action, PoC 1b choice continuation, evidence tamper RED |

The following reviewed-plan target remains recorded but is explicitly outside
this v0.2 branch: `test_preview.py` (16 tests, D9). D7 save/load/replay, D8
multiplayer issue #191, D9 animation/preview, D10 release/H1 expansion, and D11
the 0.109 adapter must not be pulled into D0-D6 merely to increase test counts.

## Required RED/GREEN cases

- [ ] Unknown command, unknown enum, protocol-major mismatch, malformed JSONL,
  BOM/NUL, line/depth/string/array limit breaches fail deterministically.
- [ ] Client proof, server proof, acknowledgement hash, nonce replay, identity,
  and transcript tampering fail without exposing token material.
- [ ] Same request ID and same canonical payload replays one terminal result;
  same ID and different payload returns `E_IDEMPOTENCY_CONFLICT`.
- [ ] A parent gameplay mutation retains its lane while a matching
  `choice.select` continuation and snapshot-safe query pass through; unrelated
  mutations return `E_MUTATION_BUSY`.
- [ ] Critical-channel overflow never blocks the main-thread producer, freezes
  mutation, preserves `E_OBSERVER_OVERFLOW`, and permanently invalidates the case.
- [ ] Telemetry overflow is counted separately and never masks critical loss.
- [ ] Timeout budgeting uses monotonic time and does not reset after choice or
  reconnect; unsafe cancellation returns `E_CANCEL_UNSAFE`.
- [ ] Exact process identity uses PID, start time, and executable path; mismatch
  returns `E_PROCESS_IDENTITY_MISMATCH` and never kills by process name.
- [ ] A dead broker returns `E_BROKER_EXIT`; a replacement broker cannot adopt
  an existing game process or recover the in-memory companion token.
- [ ] Runtime and output paths reject the repository, game source, Steam,
  Workshop, production-mod, symlink, and reparse-point boundaries.
- [ ] State snapshots are canonical, redact secrets and account paths, preserve
  semantic array order, sort set-like arrays, and do not consume gameplay RNG.
- [ ] Handles survive unrelated state revisions but expire at their declared
  process/run/room/combat or per-player choice epoch.
- [ ] No-choice card completion binds the adapter-issued action reference and
  reaches the full `queue_settled` predicate without fixed sleep or
  nearest-action fallback.
- [ ] Choice flow is `dispatch -> choice_required -> choice.select -> parent
  terminal`; stale owner/generation/handle/candidate fixtures are RED and the
  server-issued-handle path is GREEN.
- [ ] Unexpected process exit synthesizes `E_PROCESS_EXIT` for every in-flight
  request.
- [ ] Evidence finalization validates schema/path/hash/redaction, publishes
  atomically, and a one-byte artifact mutation returns `E_EVIDENCE_TAMPERED`.
- [ ] Starting at D2, the production ShinGetterMod DLL/PCK/ZIP and build inputs
  are reverse-scanned for bridge/protocol/test-only signatures with zero hits.

## Real-runtime policy

- Unit and contract tests use deterministic DTOs, fake pipe bytes, fake process
  identities, and a companion component host; they must never invent gameplay
  success evidence.
- Real PoC tests require an explicit runtime profile and TEST-ONLY companion.
  A missing game or bridge may be excluded only with an explicit local marker;
  release-gate mode treats it as failure and prints the required package path.
- `test_full_e2e.py` must resolve the installed command with `_resolve_cli()`.
  Release validation sets `CLI_ANYTHING_FORCE_INSTALLED=1` and runs from a cwd
  outside both the repository and package source tree.
- Runtime results must report package location, harness version, Git commit,
  game/adapter fingerprints, actual user-data path, display/audio driver, and
  tri-state capabilities.

## Historical 0.1.0 result

Verified on 2026-08-23 with Python 3.14.5:

- `CLI_ANYTHING_FORCE_INSTALLED=1 python -m pytest tests -v -s --tb=no`
- Result: 10 passed.
- Installed command: `D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.exe`.
- CodeGraph: synced and up to date, 3,552 indexed files; real explore query passed.
- Build: real Debug `sts2.csproj --no-restore` build passed and produced `sts2.dll`.
- Godot: discovered the 4.5.1 Mono console executable; import and launch dry-runs produced valid vectors.
- Real Godot `--headless --import`: exit 0; no files outside `.godot`, `.codegraph`, and the harness deliverables were rewritten. Godot reported its existing shutdown RID/resource-use diagnostics.
- Session dry-run: reported the proposed change and wrote no state directory.

## v0.2 execution log

Populate this section after each slice with exact commands, pass counts,
production reverse-scan evidence, and any explicitly unavailable real-runtime
capability. A unit/component substitute must never be reported as a real game
PoC.
