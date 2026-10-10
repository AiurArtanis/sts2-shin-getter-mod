# B1.3.0 Star Slash Kill-Stall Runtime Test Plan

Baseline: d93674843b1cb5070cdf2a1e8c88fe4e926c902f, official 0.109 DLL
SHA-256 C2D3E15310259957BA312F9D2362CBA193512EBE9819456A062366E6AF38B9B0.

## Real Backend

Use the existing isolated official-109 game project and a new engine/mod/profile
directory. Enable only the production candidate and this test-only controller.
Start the actual NGame single-player run; use DevConsole fight/power/card/energy
commands and PlayCardAction.RequestEnqueue. Do not call card OnPlay directly,
replace attack/death/victory tasks, or enable NonInteractiveMode/Instant.
Select a real combat-pile card by invoking the same native screen click handler
as the grid. Keep CardSelectCmd.Selector unset, so preparation voice and raise
run through the production selection barrier. HP fixture uses native setters.
For the last-enemy topology, eliminate other enemies through CreatureCmd.Kill
before observation and wait for their native death cleanup; keep the same
Bowlbugs encounter/target across all three comparisons.

## Planned Inventory

- Native baseline: two forms x three outcomes x two repeated plays, silent.
- Voice boundaries: Always and OncePerCombat first/repeated, both forms.
- Turn-two execution BGM on/off controls if end-combat timing implicates music.
- Run the same cases and instrumentation against the final optimized candidate.
- Unit/static guards scale with the implementation actually changed.

## Outcomes

1. Non-lethal: target starts at 1000 HP; all native enemies remain alive.
2. Kill, survivor: target starts at 1 HP; at least one enemy remains alive.
3. Last kill: one target at 1 HP; actual native death/end/victory/reward hooks run.

Assert exactly one target HP reduction, one attack Execute, one selected card
exhaust, three energy consumed once, no action exception, owner metadata and
sequence released, idle restored when the combat continues, and original
victory/cleanup reached on the last kill. Verify preparation audio completion
precedes the real selection screen and keep the approved 1.5x cleave timing.

## Instrumentation

Record monotonic timestamps, Engine frame, wall-clock frame gaps and delta,
GC counts/allocated bytes, main-thread synchronous spans and task completion.
Trace card/raise/selection/impact/recover/idle/release, HeavyCleave, real attack
and damage, HP change, death hooks and animation, combat end/victory, rewards
and action completion. Harmony observation always invokes the production
method; task observation never substitutes or delays the original task.
Retain baseline artifacts and logs with exact loaded DLL SHA/MVID and PCK SHA.
Flush trace files outside the observed action, not on every frame.

## Rendering And Isolation

Use background native OpenGL Compatibility rendering to exercise production
_Draw and real Godot frames. It is not a full Vulkan/D3D12 artistic acceptance.
APPDATA and LOCALAPPDATA must point into the new sandbox; Steam is disabled,
audio uses Dummy (real playback signals still execute). Stop only test-owned
processes. Never change the game source, existing saves, Steam/Beta/main,
Tag/Release, or manual checkboxes. Record failures and test-fixture repairs
truthfully; do not convert NOT_RUN/static tests into runtime passes.

## Results

Measured on 2026-10-05. Evidence root:
`E:/StS2-Automation-Sandboxes/b130-star-slash-kill-d9367484-20261005`.

- `baseline-detail/baseline2`: 24/24 logical checks pass, but seven draw spans
  exceed 100ms. Dragon frame 60 blocks MergeOutline for 2586-2839ms;
  native geometry comparison counts 243959 calls for its 387 input polygons.
- `final/geometry2`: 1971 native cases pass exact contour count, vertex/order
  equality and input non-mutation. Includes all 147 frames at start/mid/end,
  four mirrors, boundary gaps, concave/containment/union-update fixtures and
  192 deterministic rectangle sets. Same worst input takes 54.72ms/1032 native
  calls, with managed allocation 29.87MB -> 2.96MB. No mask/metadata changed.
- `final/fixed2`: 24/24 real card actions pass logical and performance guards.
  Maximum production draw is 91.7852ms; maximum warm animation frame is
  96.0349ms. The formerly blocked dragon plays now draw in 59.83-91.79ms,
  compared with 2590.75-2842.98ms. Silent surviving-target action completes
  in 4212.42ms instead of 6925.93ms.
- Metadata is validated once per form, not twice per action. Repeated dragon
  loads allocate 1136 bytes including instrumentation instead of parsing
  21.33MB again. Initial native cold frames remain recorded: one 507.87ms,
  dragon 438.28ms; they are not repeatable multi-second draw stalls.
- Preparation completion is asserted before the actual selection screen.
  Original three-second turn-two execution-music fade, native death cleanup
  and reward waits are retained; these async waits are not main-thread freezes.

Product DLL tested: `ED9D8DF04068DB65E9AE1418119F1D15CFF9379F4E745F1A363F1BD4B89C6BCF`.
Unchanged validated PCK: `6BFF05CF87B285B6531B4B9FA917FC034DFE66F3F453491337A6976701D6969E`.
`final/evidence/fixed2-comparison.json` links both matrices and artifact receipts.

The first AABB-only candidate remained slow. Failed-pair memoization and an
ordered queue alone still left hundreds of milliseconds. A native bulk-union
experiment changed contours and was rejected, not shipped. `fixed-relation/
fixed1` passed all logic but failed one draw budget at 107.42ms (GC during
foreground work); final code removes repeated union-result marshaling and
disposes its native backing array. A temporary profiler's ambiguous-overload
failure and a typed-array IDisposable compile error were repaired before the
final builds. All prior evidence is retained; no failure is relabeled as pass.

Successful metadata is process-cached and treated as read-only by current
consumers; same-path resource replacement requires restart. Tests do not
exhaust every possible continuously interpolated pose or future near-contact
asset. OpenGL/Dummy-audio automation is not subjective GPU/audio acceptance.

## Reproduction

Build the production project using the official-109 references, then stage a
new runtime root with the same validated four-file product directory:

The PowerShell helpers below are local development checks excluded by the
repository's `*.ps1` rule; they remain in this test workspace, not the commit.
The controller C# sources/project/manifest are versioned. Restaging elsewhere
requires equivalent isolated paths, official references and launch settings.

```powershell
./tests/B130StarSlashRuntime/stage.ps1 -RuntimeRoot <new-isolated-root> -ProductDirectory <four-file-directory>
./tests/B130StarSlashRuntime/launch.ps1 -RuntimeRoot <new-isolated-root> -Attempt geometry -Set geometry
./tests/B130StarSlashRuntime/launch.ps1 -RuntimeRoot <new-isolated-root> -Attempt matrix -Set matrix -CheckPerformance
```

Performance failure thresholds are draw >=100ms or warm animation frame
>=250ms. First-resource cold gaps are recorded separately, not hidden.
`compare.ps1` compares two completed 24-case matrices without overwriting them.
