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
| `test_companion.py` | 20 | D2-D6 | Real C# ComponentHost authentication, reconnect/replay, production idempotency gate, action/choice continuation, overflow and shutdown |
| `test_evidence.py` | 12 | D2-D6 | Evidence schema, confinement, redaction, immutable hashes, production reverse-scan inputs and artifacts |
| `test_state_diff.py` | 30 | D4 | Epoch-scoped handles, canonical JSON/hash, ordered versus set-like arrays, tolerance, redaction, RNG-side-effect detection, bounded JSON Pointer diff |
| `test_full_e2e.py` | 18 | D3-D6 | Installed CLI from arbitrary cwd, broker/component test host, PoC 0 handshake, PoC 1 no-choice action, PoC 1b choice continuation, evidence tamper RED |
| `test_runtime_release.py` | 1 | D6 release | Explicit-profile real 0.111 PoC 0/1/1b, reconnect, production idempotency, graceful exit, stderr classification, evidence sealing, cleanup |

The following reviewed-plan target remains recorded but is explicitly outside
this v0.2 branch: `test_preview.py` (16 tests, D9). D7 save/load/replay, D8
multiplayer issue #191, D9 animation/preview, D10 release/H1 expansion, and D11
the 0.109 adapter must not be pulled into D0-D6 merely to increase test counts.

## Required RED/GREEN cases

- [x] Unknown command, unknown enum, protocol-major mismatch, malformed JSONL,
  BOM/NUL, line/depth/string/array limit breaches fail deterministically.
- [x] Client proof, server proof, acknowledgement hash, nonce replay, identity,
  and transcript tampering fail without exposing token material.
- [x] Same request ID and same canonical payload replays one retained terminal
  result. Completed request-ID/digest tombstones outlive the 256-payload LRU:
  an exact retry outside the window returns `E_IDEMPOTENCY_WINDOW_EXPIRED`
  without re-execution, and different payload returns
  `E_IDEMPOTENCY_CONFLICT` before or after eviction.
- [x] Each Python send uses a sequence floor, so a replayed terminal already in
  `_pending` cannot satisfy a later attempt that reused the request ID.
- [x] A parent gameplay mutation retains its lane while a matching
  `choice.select` continuation and snapshot-safe query pass through; unrelated
  mutations return `E_MUTATION_BUSY`.
- [x] Critical-channel overflow never blocks the main-thread producer, freezes
  mutation, preserves `E_OBSERVER_OVERFLOW`, and permanently invalidates the case.
- [x] Telemetry overflow is counted separately and never masks critical loss.
- [x] Timeout budgeting uses monotonic time and does not reset after choice or
  reconnect; unsafe cancellation returns `E_CANCEL_UNSAFE`.
- [x] Exact process identity uses PID, start time, and executable path; mismatch
  returns `E_PROCESS_IDENTITY_MISMATCH` and never kills by process name.
- [x] A dead broker returns `E_BROKER_EXIT`; a replacement broker cannot adopt
  an existing game process or recover the in-memory companion token.
- [x] Runtime and output paths reject the repository, game source, Steam,
  Workshop, production-mod, symlink, and reparse-point boundaries, including a
  real Windows junction ancestor. Live CLI use without an explicit existing
  absolute `--protected-root` set fails closed with `E_ISOLATION_BREACH`.
- [x] State snapshots are canonical, redact secrets and account paths, preserve
  semantic array order, sort set-like arrays, and do not consume gameplay RNG.
- [x] Handles survive unrelated state revisions but expire at their declared
  process/run/room/combat or per-player choice epoch.
- [x] No-choice card completion binds the adapter-issued action reference and
  reaches the full `queue_settled` predicate without fixed sleep or
  nearest-action fallback.
- [x] Choice flow is `dispatch -> choice_required -> choice.select -> parent
  terminal`; stale owner/generation/handle/candidate fixtures are RED and the
  server-issued-handle path is GREEN.
- [x] Unexpected process exit synthesizes `E_PROCESS_EXIT` for every in-flight
  request.
- [x] Rolling replay eviction advances a resume floor without invalidating the
  case; a cursor below that floor returns `E_RESUME_WINDOW_EXPIRED`.
- [x] Malformed and oversized frames isolate the offending connection, and a
  new authenticated connection can continue against the same server epoch.
- [x] The active live critical queue is independent of replay retention; only
  live-consumer backpressure can latch `E_OBSERVER_OVERFLOW`.
- [x] Broker tests assert their own child-process teardown on normal and failure
  paths; `session close` waits for confirmed broker exit. A broken graceful
  shutdown RPC is returned as structured `shutdown_error` while exact process
  reaping still completes.
- [x] Game children inherit only a minimal system allowlist plus explicit test
  variables, and existing sessions revalidate persisted protected roots.
- [x] Evidence finalization validates schema/path/hash/redaction, publishes
  atomically, and a one-byte artifact mutation returns `E_EVIDENCE_TAMPERED`.
- [x] Starting at D2, the production ShinGetterMod DLL/PCK/ZIP and build inputs
  are reverse-scanned for bridge/protocol/test-only signatures with zero hits.

## Real-runtime policy

- Unit and contract tests use deterministic DTOs, fake pipe bytes, fake process
  identities, and a companion component host; they must never invent gameplay
  success evidence.
- Real PoC tests require an explicit runtime profile and TEST-ONLY companion.
  A missing game or bridge may be excluded only with an explicit local marker;
  release-gate mode treats it as failure and prints the required package path.
- `test_runtime_release.py` skips only when release mode is absent. When
  `STS2_HEADLESS_RUNTIME_RELEASE_GATE=1`, it also requires an absolute
  `STS2_HEADLESS_RUNTIME_PROFILE`, `CLI_ANYTHING_FORCE_INSTALLED=1`, and
  `stage_companion=true`; any missing prerequisite is an error.
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

Verified on 2026-09-03 from branch
`feat/headless-test-harness-v0.2-20260903`, based on
`origin/main@90662124625d57fe042514b6f5a71b868fbbfcb2`.

- D0-D6 implementation commits before documentation:
  `37ecb75b`, `82adb54f`, `219f87f0`, `bb52fb24`, `8739b103`,
  `9263223b`, `c2527ed1`, and `919e6252`.
- The post-review hardening pass is based on the already pushed documentation
  checkpoint `7f5ff9ad` and is delivered as a new ordinary-push checkpoint; it
  adds production idempotency, reconnect/overflow hardening, broker teardown
  enforcement, minimal child environment, and the real-runtime release gate.
- The second-review remediation preserves completed request-ID/digest
  tombstones beyond the 256-terminal-payload LRU, rejects expired exact retries
  with `E_IDEMPOTENCY_WINDOW_EXPIRED`, isolates each Python send from buffered
  old terminals, rejects real Windows junction ancestors, requires explicit
  live protection roots, and reaps exact child processes after a broken
  graceful-shutdown RPC. The SOP limits `--dry-run` claims to commands that
  actually expose the option.
- Installed-CLI test from external cwd:
  `CLI_ANYTHING_FORCE_INSTALLED=1 python -m pytest <absolute-tests-path> -v -rs --tb=no`.
  Result: **249 passed, 2 skipped in 50.62s** across all **251 collected nodes**.
  The skips are the Windows unprivileged
  symlink-creation fixture (`WinError 1314`) and the intentionally opt-in real
  runtime release test; both corresponding enforcement paths are covered.
- Forced-installed subprocess resolution printed
  `D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE`; its focused
  external-cwd check passed.
- Release mode without `STS2_HEADLESS_RUNTIME_PROFILE` failed at fixture setup
  with the required explicit-profile diagnostic.
- Configured real-runtime gate:
  `STS2_HEADLESS_RUNTIME_RELEASE_GATE=1`, absolute external profile, and
  `CLI_ANYTHING_FORCE_INSTALLED=1`; result: **1 passed in 57.56s**.
- TEST-ONLY 0.111 companion Release build: **0 warnings, 0 errors**.
- ContractVerifier: `ok=true`, protocol `sts2-test/v1`, four golden protocol
  files, cross-language client proof matched.
- ComponentHost Release build: **0 warnings, 0 errors**; broker/component tests
  are included in the 249 passing tests.
- Final production reverse scan:
  `C:\Users\win\AppData\Local\cli-anything\slaythespare2-111-beta\gates\20260903-v02-second-review-production-scan-v2.json`.
  Result: `ok=true`, `hits=[]`; seven signatures (including
  `E_IDEMPOTENCY_WINDOW_EXPIRED`) cover the production input tree plus the
  existing production DLL, PCK, and release ZIP.

The final release-gate session is
`E:\Work\StS2 Mods\_headless-runtime\release-gates\runtime-release-20260903-77811dd3`.
It used game `v0.111.0@41cef1ea`, companion SHA-256
`b8cc79cc070ef979a61e398a69c9c20d697049a37416dd5fedf382c7bb403bbf`,
`display_driver=headless` and `audio_driver=Dummy`. A ShinGetter run entered
`CULTISTS_NORMAL`, added `DEFEND_IRONCLAD`, and completed the exact typed card
action at `queue_settled`: energy `3 -> 2`, block `0 -> 5`, queue empty,
executor idle, no pending choice, and unchanged RNG fingerprint.

PoC 1b used the same live process. Playing `ARMAMENTS` produced five real
candidates. The client disconnected at `choice_required`, reconnected with the
same process epoch/new connection ID, and resumed from the retained sequence.
An exact in-flight duplicate did not execute again; different content failed
with `E_IDEMPOTENCY_CONFLICT`; a stale generation failed with
`E_STALE_HANDLE`; and an unrelated mutation failed with `E_MUTATION_BUSY`.
After the matching continuation, the parent ended at `queue_settled`; an exact
terminal duplicate returned the same result with `replayed=true`. Event
sequence: action enqueued `24`, choice required `26`, action finished `40`,
parent terminal `41`, terminal replay `42`. The selected
`CARD.S_G_C_DEFEND` changed upgrade level `0 -> 1`, energy changed `2 -> 1`,
queues were empty, the executor was idle, no choice was pending, and the RNG
fingerprint was unchanged.

The game stopped gracefully with exact `exit_code=0`; expected Godot shutdown
diagnostics were classified. Evidence finalized and reverified as 22 artifacts
with aggregate SHA-256
`9cb40b38ec5a219052bccf57ef964bf383728866bbec1900052678e20b3cfb92`.
The broker then closed, and process inspection found no game, broker, pytest, or
ComponentHost residue. Runtime staging and evidence remain outside the
repository; no Steam or normal Godot deployment was modified.

## Full verbose pytest node log

The following block is generated from the final external-cwd
`python -m pytest <absolute-tests-path> -v -rs --tb=no` run. It records every
collected node and status rather than only the aggregate count.

<!-- PYTEST_VERBOSE_START -->
<details>
<summary>Final external-cwd node/status output (249 passed, 2 skipped in 50.62s)</summary>

```text
============================= test session starts =============================
platform win32 -- Python 3.14.5, pytest-9.1.1, pluggy-1.6.0 -- D:\py\Python314\python.exe
cachedir: .pytest_cache
rootdir: E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness
configfile: pyproject.toml
plugins: anyio-4.13.0
collecting ... collected 251 items

E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_broker_publishes_verifiable_public_identity PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_broker_holds_exclusive_session_lease PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_session_close_waits_until_exact_broker_exits PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_broker_component_start_authenticates_and_records_runtime PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_broker_never_persists_companion_secret PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_broker_runtime_ping_uses_owned_long_connection PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_broker_client_different_payload_cannot_consume_replayed_old_terminal PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_broker_process_status_and_stop_are_exact PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_session_close_reaps_child_when_graceful_shutdown_rpc_breaks PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_broker_lists_only_its_session_instances PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_broker_finalizes_and_verifies_evidence_without_post_write PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_dead_broker_is_not_replaced_or_allowed_to_adopt_child PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_broker_rejects_second_process_for_same_instance PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_idle_control_client_cannot_block_other_cli_requests PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_broker.py::test_broker_response_read_has_monotonic_deadline PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_authenticated_ping PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_reports_tri_state_capabilities PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_rejects_wrong_client_token PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_rejects_unknown_command PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_choice_parent_remains_inflight PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_rejects_unrelated_mutation_while_choice_waits PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_action_parent_uses_exact_reference_and_full_queue_settle PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_wrong_action_reference_cannot_release_mutation_lane PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_game_bridge_action_completion_has_no_sleep_or_nearest_action_fallback PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_game_bridge_and_component_host_share_production_idempotency_gate PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_rejects_stale_choice PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_rejects_each_stale_choice_identity_field[owner_id-999] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_rejects_each_stale_choice_identity_field[choice_generation-999] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_rejects_each_stale_choice_identity_field[choice_handle-choice:stale] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_rejects_each_stale_choice_identity_field[candidates-replacement3] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_game_bridge_choice_broker_uses_local_selector_and_server_handles PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_snapshot_query_passes_mutation_lane PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_reconnect_replays_unread_critical_events PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_reconnect_routes_future_inflight_terminal_to_new_pipe PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_same_payload_duplicate_stays_single_inflight_execution PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_replayed_request_is_idempotent PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_same_id_different_payload_conflicts PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_completed_id_never_reexecutes_after_terminal_cache_capacity PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_wait_event_fails_when_request_is_already_terminal PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_bad_frame_isolated_and_next_authenticated_ping_succeeds[{not-json}\n] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_bad_frame_isolated_and_next_authenticated_ping_succeeds[{"oversized":"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"}\n] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_rejects_expired_resume_window PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_blocked_live_writer_latches_critical_overflow_and_freezes_mutation PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_companion.py::test_component_host_shutdown_is_explicit PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_project_status_reads_real_project PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_discovers_real_command_line_arguments PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_parses_real_console_commands PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_build_and_godot_commands_are_argument_vectors PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_session_dry_run_autosave_undo_redo PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_cli_json_output PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_reverse_scan_accepts_clean_binary PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_reverse_scan_finds_bridge_name PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_reverse_scan_finds_signature_across_chunk_boundary PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_reverse_scan_checks_zip_entry_names PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_reverse_scan_checks_zip_entry_content PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_reverse_scan_excludes_headless_harness_directory PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_reverse_scan_records_target_hash_and_size PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_core.py::test_reverse_scan_rejects_missing_target PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_finalize_writes_manifest_and_marker PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_manifest_uses_relative_paths PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_manifest_records_hash_and_size PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_excludes_raw_user_data_and_sentry_identity PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_verify_accepts_unchanged_bundle PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_verify_detects_single_byte_tamper PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_verify_detects_new_allowlisted_artifact PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_finalize_rejects_second_publication PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_finalize_rejects_pass_for_invalid_case PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_finalize_rejects_symlink_artifact SKIPPED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_finalize_rejects_secret_material[STS2_TEST_TOKEN=secret\n] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_finalize_rejects_secret_material[{"token":"secret"}\n] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_finalize_rejects_secret_material[{"client_proof":"abc"}\n] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_finalize_rejects_secret_material[{"server_proof":"abc"}\n] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_metadata_redacts_account_path PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_artifact_aggregate_is_order_stable PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_verify_rejects_manifest_edit PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_schema_rejects_incomplete_metadata PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py::test_evidence_catalog_includes_explicit_snapshots_but_excludes_user_data PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_full_e2e.py::test_live_cli_fails_closed_without_explicit_protected_root [_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_full_e2e.py::test_installed_cli_source_and_console [_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
[_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_full_e2e.py::test_real_codegraph_status_and_explore [_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
[_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_full_e2e.py::test_real_dotnet_build [_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_full_e2e.py::test_godot_discovery [_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_full_e2e.py::test_installed_cli_broker_component_poc0_from_arbitrary_cwd [_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
[_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
[_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
[_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
[_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
[_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
[_resolve_cli] Using installed command: D:\py\Python314\Scripts\cli-anything-slaythespare2-111-beta.EXE
PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_default_runtime_root_uses_local_app_data PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_default_runtime_root_falls_back_to_temp PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_default_state_root_never_uses_project_tree PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identifier_accepts_safe_values[session-1] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identifier_accepts_safe_values[host] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identifier_accepts_safe_values[client-1000] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identifier_accepts_safe_values[a.b_c] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identifier_rejects_unsafe_values[] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identifier_rejects_unsafe_values[../escape] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identifier_rejects_unsafe_values[a/b] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identifier_rejects_unsafe_values[a\\b] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identifier_rejects_unsafe_values[.hidden] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identifier_rejects_unsafe_values[xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_runtime_path_guard_allows_external_root PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_runtime_path_guard_rejects_repository_root PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_runtime_path_guard_rejects_repository_descendant PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_runtime_path_guard_rejects_protected_game_tree PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_runtime_path_guard_rejects_relative_path PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_runtime_path_guard_rejects_reparse_ancestor PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_runtime_path_guard_rejects_real_windows_junction_ancestor PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_runtime_path_guard_rejects_each_declared_protected_root[steam] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_runtime_path_guard_rejects_each_declared_protected_root[workshop] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_runtime_path_guard_rejects_each_declared_protected_root[production-mod] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_control_session_creates_frozen_layout PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_control_session_index_contains_no_secret PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_control_session_persists_and_revalidates_protected_roots PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_control_session_open_rejects_preexisting_session_under_new_protected_root PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_control_session_rejects_process_cwd_in_any_protected_tree PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_broker_record_rejects_secret_fields PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_broker_record_persists_only_public_identity PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_instance_state_machine_accepts_documented_path PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_instance_state_machine_rejects_illegal_transition PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_instance_layout_is_per_instance PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_atomic_write_json_is_lf_and_no_bom PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_atomic_write_json_remains_valid_under_threads PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_append_jsonl_is_stable PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_build_game_environment_keeps_token_in_child_block_only PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_redacted_environment_contains_names_not_values PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_capture_current_process_identity PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identity_match_binds_all_fields[pid] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identity_match_binds_all_fields[process_start_time_utc] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identity_match_binds_all_fields[executable_path] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_identity_match_binds_all_fields[executable_sha256] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_process_record_round_trip PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_exact_stop_rejects_wrong_identity_without_killing PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_exact_stop_terminates_owned_child PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_exact_stop_uses_successful_graceful_shutdown_callback PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_exact_stop_reaps_owned_child_after_shutdown_callback_error PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_assert_broker_alive_maps_missing_process_to_broker_exit PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_validate_isolated_user_data_accepts_descendant PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_validate_isolated_user_data_rejects_shared_path PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_write_sentinel_detects_protected_creation PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_write_sentinel_ignores_allowed_root PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_process_exit_is_reflected_in_status PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_spawn_records_argument_vector_without_shell PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_process.py::test_spawn_real_child_receives_only_system_allowlist_and_explicit_environment PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_golden_document_validates[protocol-v1-protocol/challenge.json] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_golden_document_validates[protocol-v1-protocol/hello.json] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_golden_document_validates[protocol-v1-protocol/request-play-card.json] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_golden_document_validates[protocol-v1-protocol/choice-required.json] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_golden_document_validates[state-v1-state/minimal-state.json] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_golden_document_validates[scenario-v1-scenario/poc-1b.json] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_golden_document_validates[evidence-v1-evidence/minimal-manifest.json] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_schema_rejects_protocol_major PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_schema_rejects_unknown_lifecycle_type PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_jsonl_round_trip PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_jsonl_encoder_is_stable_and_lf_terminated PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_jsonl_rejects_invalid_framing[\xef\xbb\xbf{}\n-E_INVALID_ARGUMENT] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_jsonl_rejects_invalid_framing[{"x":"a\x00b"}\n-E_INVALID_ARGUMENT] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_jsonl_rejects_invalid_framing[{}{}\n-E_INVALID_ARGUMENT] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_jsonl_rejects_invalid_framing[{}-E_INVALID_ARGUMENT] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_jsonl_rejects_invalid_framing[\xff\n-E_INVALID_ARGUMENT] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_jsonl_rejects_line_limit PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_jsonl_rejects_depth_limit PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_jsonl_rejects_string_limit PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_jsonl_rejects_array_limit PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_length_prefix_is_big_endian_utf8 PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_handshake_transcript_is_unambiguous PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_client_proof_is_deterministic PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_client_proof_changes_when_resume_seq_changes PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_server_proof_binds_ack_body PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_ack_hash_uses_canonical_key_order PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_constant_time_hex_equal_handles_invalid_hex PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_nonce_registry_rejects_replay PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_nonce_registry_evicts_oldest PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_request_digest_ignores_transport_seq PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_request_digest_binds_args PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_idempotency_first_request_is_new PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_idempotency_same_inflight_request_is_inflight PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_idempotency_replays_terminal PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_idempotency_conflict_is_error PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_idempotency_cache_is_bounded PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_mutation_lane_accepts_parent PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_mutation_lane_allows_safe_passthrough[snapshot-safe-query] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_mutation_lane_allows_safe_passthrough[control] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_mutation_lane_allows_matching_choice_continuation PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_mutation_lane_rejects_invalid_choice_continuation[other-True] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_mutation_lane_rejects_invalid_choice_continuation[parent-False] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_mutation_lane_rejects_unrelated_mutation PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_mutation_lane_release_requires_owner PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_mutation_lane_freeze_is_irreversible PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_critical_buffer_accepts_without_blocking PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_critical_buffer_overflow_latches_failure PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_critical_buffer_preserves_terminal_table PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_critical_buffer_remains_invalid_after_drain PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_telemetry_overflow_is_counted_separately PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_request_tracker_enforces_one_terminal PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_request_tracker_synthesizes_process_exit PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_monotonic_budget_counts_down PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_monotonic_budget_resume_keeps_deadline PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_capability_state_values[available-True] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_capability_state_values[unavailable-True] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_capability_state_values[unknown-True] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_capability_state_values[partial-True] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_capability_state_values[yes-False] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_command_descriptor_rejects_incompatible_wait_level PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_protocol.py::test_protocol_failure_serializes_without_traceback PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_runtime_release.py::test_real_runtime_release_gate SKIPPED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_finalize_snapshot_validates_schema PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_finalize_snapshot_does_not_mutate_input PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_canonical_state_hash_ignores_object_key_order PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_canonical_state_hash_ignores_its_own_field PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_canonical_state_hash_changes_for_hard_state PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_canonical_json_rejects_nan PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_normalization_redacts_home_path PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_normalization_removes_secret_fields[token] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_normalization_removes_secret_fields[client_proof] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_normalization_removes_secret_fields[server_proof] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_normalization_removes_secret_fields[credential] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_normalization_removes_secret_fields[password] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_normalization_removes_platform_identity[steam_id] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_normalization_removes_platform_identity[account_id] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_normalization_removes_platform_identity[sentry_installation_id] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_normalization_preserves_semantic_array_order PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_normalization_sorts_only_declared_set_array PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_normalization_keeps_integer_type PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_normalization_keeps_float_type PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_observer_accepts_unchanged_rng PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_observer_rejects_rng_side_effect PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_player_handle_survives_state_revision PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_handle_expires_at_declared_epoch[player-changed0] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_handle_expires_at_declared_epoch[player-changed1] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_handle_expires_at_declared_epoch[room-changed2] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_handle_expires_at_declared_epoch[creature-changed3] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_handle_expires_at_declared_epoch[combat-card-changed4] PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_same_object_gets_stable_handle_within_epoch PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_choice_generation_is_per_player PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_choice_end_invalidates_candidate PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_new_choice_invalidates_previous_choice_candidate PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_unknown_handle_is_stale PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_json_pointer_get_decodes_escape_sequences PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_json_pointer_get_rejects_missing_path PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_equal_snapshots PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_reports_snapshot_hashes PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_orders_identity_before_hard_state PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_orders_location_before_action_and_hard PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_orders_action_before_general_hard_state PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_orders_eventual_before_presentation PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_ordered_array_detects_reordering PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_set_array_ignores_reordering PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_absolute_tolerance_accepts_close_values PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_relative_tolerance_accepts_close_values PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_outside_tolerance_reports_rule PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_ignore_rule_is_reported PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_is_bounded_but_counts_all_differences PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_reports_missing_side PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_json_pointer_escapes_keys PASSED
E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_state_diff.py::test_diff_context_is_small_and_local PASSED

=========================== short test summary info ===========================
SKIPPED [1] E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_evidence.py:142: symlink creation unavailable: [WinError 1314] 客户端没有所需的特权。: 'C:\\Users\\win\\AppData\\Local\\Temp\\pytest-of-win\\pytest-302\\test_evidence_finalize_rejects2\\outside.txt' -> 'C:\\Users\\win\\AppData\\Local\\Temp\\pytest-of-win\\pytest-302\\test_evidence_finalize_rejects2\\runtime\\session-1\\instances\\solo\\stdout.log'
SKIPPED [1] E:\Work\StS2 Mods\_worktrees\ShinGetterMod-headless-test-harness-v0.2-20260903\headless-test-harness\python\agent-harness\tests\test_runtime_release.py:287: real runtime gate is opt-in; set STS2_HEADLESS_RUNTIME_RELEASE_GATE=1 and STS2_HEADLESS_RUNTIME_PROFILE=<absolute-profile.json>
======================= 249 passed, 2 skipped in 50.62s =======================
```

</details>
<!-- PYTEST_VERBOSE_END -->
