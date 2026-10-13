# issue#255 — formal109 cold-render scheduling candidate

## Scope and source

Baseline: main `53012aee53083d61780e4fd1bd5ad4515f353a60`, v1.3.0. Independent
branch `fix/formal109-render-cold-20261013`. This is NOT issue#254, NOT an issue#93
child, and does not reopen issue#10/issue#159/issue#215. The custom energy scene is
from the original v1.0.0 implementation. No Beta source/branch is modified.

Formal109 reference SHA-256:
`C2D3E15310259957BA312F9D2362CBA193512EBE9819456A062366E6AF38B9B0`.

## Why resource preload is not actual first-use preparation

Formal source `PreloadManager.LoadRoomCombatAssets` awaits resource loading.
`AssetLoadingSession.ProcessVfxQueue` caches threaded-loaded scenes; it does not
instantiate or draw them. Native `NPowerAppliedBuffVfx._Ready` and
`NHitSparkVfx._Ready` restart their particles, with no expensive managed setup in
the observed Ready timings. GPU first-use work is not represented by those times.

Godot 4.5.1 upstream source confirms generated shader variants and compute pipelines:

- https://github.com/godotengine/godot/blob/4.5.1-stable/scene/resources/particle_process_material.cpp
  — `_update_shader`, `_queue_shader_change`, `flush_changes`, shader-key reuse and
  release when users reach zero.
- https://github.com/godotengine/godot/blob/4.5.1-stable/servers/rendering/renderer_rd/storage_rd/particles_storage.cpp
  — `ParticlesShaderData::set_code`, shader version and `compute_pipeline_create`.

This is a triggered game/Godot cold path, not proof of a defective mod GPU shader,
nor a separately measured texture-upload diagnosis. D3D12 reports no PSO caching;
the historical device-removal cause remains unknown.

## Minimal component localization (external diagnostic only)

Do not rerun v7–v13. New v14/v15 changed materials at `NEnergyCounter._Ready` and
left opening peaks around 1.02/1.01s: that is too late to remove resource-created
shader work. At the resource boundary, v16 omits only the custom scatter material
and its override, retaining native inherited scatter; opening drops to 348.8983ms.
v17 omits only custom glow and still peaks at 925.8394ms. These are diagnostic
ablations, never product effect replacements. Native buff was suppressed in these
four diagnostic cases to isolate energy cost; product candidate does not suppress it.

## Implementation and lifecycle

- Exact formal109 Harmony target:
  `PreloadManager.LoadRoomCombatAssets(EncounterModel, IRunState) -> Task`.
  Postfix awaits the original Task before the optional preparation. Original errors
  propagate; no player hooks, damage, power or form code is changed.
- Only runs containing ShinGetter; TestMode/headless/disabled render loop/no live game
  return immediately.
- A weak `IRunState` key owns one pending/completed preparation. Same-run requests
  share pending work and completed requests are no-ops. `Task.Yield` installs Pending
  before synchronous failure can clear it.
- Fixed three scene budget: custom energy, native buff, native hit. No all-assets
  traversal, sprite-cache limit change or global render-mode setting. A private 256x256
  SubViewport matches main HDR2D/MSAA2D and is not exposed by a Control/ViewportTexture.
- Detached scene templates never enter tree/native Ready. Only visible GPUParticles2D
  are duplicated without scripts/signals, preserving original process/draw materials
  and textures. Warm counts: 6 energy + 3 buff + 4 native hit; unused hidden native
  alternate sparks are not drawn.
- Each stage awaits two actual FramePostDraw events and a cleanup draw, within one
  shared 10-second monotonic preparation budget. ProcessFrame checks that budget and
  render-loop availability even without draws. Draw/ProcessFrame/TreeExiting
  subscriptions detach in finally; tree exit cancels the wait. Template nodes are freed,
  particle clones/viewport QueueFree'd. Only bounded Resource references, never Nodes,
  are retained for the weak run lifetime to keep prepared shader variants alive.
- Optional preparation failures warn/fail open and release temporary resources. No
  saves/settings, voice cues, RNG, Power/shape/Ki/Vigor, frame/fusion or live particle
  materials are mutated. All product assets and PCK are unchanged.

## RED / GREEN and formal native evidence

`python -B tools/validate_issue_255.py` first failed with
`render preparation production files missing (RED)` before implementation.
The initial candidate source contract passed with 8 rejected ownership/gate/cleanup/order
mutations; the P1 follow-up extends this to 15 (see below).
Formal109 build: 0 warning / 0 error. Target binding is also exercised in the native
candidate run (preparation logs appear before NCombatRoom creation).

Only one native rendered diagnostic battle per process, serial, private settings with
legal defaults. D3D12/Forward+, 1280x720, 120 FPS/VSync Disabled in the TEST PROCESS
only. 8-point unblockable native damage reaches HP/Hit; native form transform used.
No physical-input, normal paid-card, audio, full-run, multiplayer or Beta acceptance.

| Measurement | baseline v19 | final candidate v20 |
|---|---:|---:|
| Process launch to main menu ms | 16377.6604 | 16261.4440 |
| Combat-entry command ms | 2652.5706 | 4289.3807 |
| First-hit command ms | 2536.0878 | 850.9435 |
| Entry + first-hit command ms | 5188.6584 | 5140.3242 |
| Combat-loading max frame ms | 1866.7380 | 1633.5584 |
| Operational hit/transform max frame ms | 1813.9873 | 106.0228 |
| First-hit max frame ms | 1813.9873 | 102.8034 |
| Second-form cold-hit max frame ms | 81.4043 | 98.2848 |
| Godot static memory peak bytes (NOT process RSS) | 1133463300 | 1129274192 |
| VRAM peak bytes | 1916338176 | 1916338176 |

Exact preparation stage times: energy822.0ms, buff806.2ms, hit1645.5ms; total3311.5ms.
The approximate total cold work is still present. This is SCHEDULING improvement,
not elimination of compilation/startup cost. Main-menu time is not a controlled
statistical speedup. Warm first-hit max80.1776ms / p958.8042ms remains recorded;
the separate first/two-form sprite cold loads around60ms are NOT fixed.

The older raw `interactivePeakMs` metric requires end-turn enabled and can exclude
a stall while native hit handling temporarily disables it. Do NOT use it as the
complete operational-battle maximum. The table uses all hit/transform phase samples,
including temporarily disabled controls, after the settled opening; raw flags stay.

Native Ready counts are identical: CombatRoom1, DamageNum5, HitSpark5, Buff8.
Final lifecycle checks: completed repeat0.0194ms; non-getter NullRunState no-op;
detached wait=false; synchronous TreeExiting cancellation=false; no temporary host
at end. Only native lifecycle probes, not manual perception or all visual equivalence.

Both v19/v20 runtime ERROR0, missing InputMap0, device-loss0. Both retain12 shutdown
cleanup ERRORs and existing UID fallback warnings; same native particle/mesh/material
RID exit inventory, no added preparation viewport at the end. No blanket zero-log claim.

Original v20 native candidate DLL SHA-256 (before the P1 follow-up):
`0DF98E4ED0B371993B5E84711F6F8370E72C8D35E2BAB9E0ACB23787B3F34111`.

## P1 follow-up: bounded no-draw lifecycle (2026-10-13)

Independent first review of `6c9df1c30e80161602e3534009a9f73728b76d4e`
failed: a successful original load could remain pending indefinitely while the host
stayed in the tree and no draw occurred. Review report:
`E:/Work/StS2 Mods/_issue-evidence/combat-stutter-20261012/review-issue255-6c9df1c.md`.
That review result is preserved; developer self-tests below do not replace a second
independent review.

The follow-up skips disabled rendering at entry and checks it again after Task.Yield
and during preparation. All stage/draw waits share the original Stopwatch and
10-second budget; logic-frame callbacks complete a stopped/no-draw attempt on the
captured Godot thread. No WhenAny-abandoned task, worker-thread timer, global setting
change or new resource scope is introduced. Failure releases temporary nodes, all
three subscriptions, resources and Pending; it does not set Completed and permits
retry when rendering returns. Original loader faults/cancellation still propagate.

Existing developer evidence, retained without unnecessary reruns during recovery:

- `tests/Issue255RenderWait/` links the actual production service and postfix, with
  fake Godot scene/draw/logic events and a single-thread synchronization context.
  It tests managed async lifecycle, not native GPU or human visual/input acceptance.
- Exact old production service obtained from `git show 6c9df1c:...` remained pending
  after 12017.1171ms with original loading successful and the host still live;
  expected RED exit 1. The harness then removed its own host for cleanup.
- Current production-linked GREEN: no-draw wrapper finished at 10020.2736ms,
  86 assertions / 1955 main-thread checks, exit 0. Timeout cleanup, render-loop
  disable after one draw, host exit, resource/Pending clearing, retry, original
  failure/cancellation and a real 3.4-second successful delay are covered.
- Formal109 developer build: 0 warning / 0 error, as handed over by the original
  functional-development chat. The recovery chat did not rebuild or launch a game.
- Updated source contract rejects 15 negative mutations. Existing build/gate/GREEN
  results are developer evidence and remain subject to independent second review.

Owned raw evidence SHA-256:

| File in combat-stutter-20261012 | SHA-256 |
|---|---|
| issue255-p1-legacy-Service.cs | 69CCB15746AA5E7A4CE70AC6C8554FBE34B7F4A5A4B55FBA38572C1F7152A091 |
| issue255-p1-red-output.txt | 3269CC9C19F642F23B8669D072F7CB37B94622DEA9EC192FC5168FA3B526828D |
| issue255-p1-green-output.txt | 8C6B0D6E7B7DBA6F67D7756B7982AD46C1172D144A937E773065CF612D5E8CB9 |

Production service SHA-256 at recovery:
`8DF1B33C8CEFCAC12F88D656F8388C00810AA3BD86E79203FE1D4D47C119DCA1`.
The 10-second limit is checked at engine logic/draw callbacks; it cannot preempt a
synchronous native driver call or a completely stopped engine loop. The v19/v20
performance table remains evidence for the original rendered candidate, not a new
native benchmark of this P1 follow-up. No v7-v20 matrix, full run, multiplayer or
Beta rerun was made. The approximately 60ms sprite cold load and historical device
loss remain unresolved; 13 manual acceptance items remain unchecked.

## Reproduction and evidence

Owned sandbox:
`E:/StS2-Automation-Sandboxes/combat-stutter-53012aee-20261012`.
Driver source: `controller/Runner.cs`; launch safety script: `launch.ps1`.
No graphics instances may run concurrently. Driver rejects another Godot/game process,
uses unique APPDATA/LOCALAPPDATA, and exits naturally; a deadline stops only owned PID.

```powershell
dotnet build controller/StutterProbe.csproj --nologo
./launch.ps1 -Attempt <fresh-unique-name> -Driver d3d12 -Product render-candidate -LegalSettings -FixedSync
```

Requires candidate DLL in private product/render-candidate; baseline PCK/JSON/image are
the protected installed53012aee copies. Do not launch either original game source project.

Full samples/HP/powers/timings/PNG/raw logs:
`evidence/render-v20-fixed-final-candidate-d3d12-*` and
`evidence/baseline-v19-fixed-full-telemetry-d3d12-*`.
Summary with original JSON/log/PNG SHA-256:
`E:/Work/StS2 Mods/_issue-evidence/combat-stutter-20261012/issue255-candidate-comparison.json`.
Historical failed/diagnostic variants stay; not merged into acceptance totals.

## Handoff boundary

Main developer independently reviews this narrow candidate and decides integration.
No PR/merge/PCK export/shared deployment/release/issue closure by this branch.
Root workspace, two read-only game sources, saved user settings and Godot/Steam
deployment are protected. issue#254 stays a6a3b64c reviewed/pending acceptance,
not integrated. issue#255 stays OPEN until Artanis acceptance; no checklist auto-ticks.
