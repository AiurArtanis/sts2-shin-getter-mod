# issue#255 render-wait lifecycle regression

This managed fixture links both **current production files**, not copies of their
logic. Godot draw/logic events and scene objects are stubs, with a single-thread
SynchronizationContext and thread assertions. It does not load the game or validate
native GPU cost, graphics, audio, inputs, or player acceptance.

Run from the repository root:

```powershell
dotnet run --project tests/Issue255RenderWait/Issue255RenderWait.csproj --nologo
```

The cutoff test uses the actual production Stopwatch and 10-second shared budget.
It pumps logic events without draw events; no virtual clock or shortened timeout.
Expected exit code: 0; 86 assertions. The test also covers:

- successful original loading plus a still-live host with no draws;
- same-run shared Pending, timeout cleanup, cleared resources, retry;
- render-loop disabled at entry, or after one draw with no subsequent draw;
- host exit and retry, all draw/logic/exit subscriptions and temporary nodes released;
- non-Getter/headless no-op, original fault/cancellation propagation;
- optional cache failure preserves original success and permits retry;
- a real 3.4-second delay followed by draws remains within budget and succeeds;
- successful same-run no-op, and all stubbed Godot operations on the captured thread.

## Old-source RED, current-source GREEN (2026-10-13)

The old 13-assertion review fixture is preserved unmodified in:
`E:/Work/StS2 Mods/_issue-evidence/combat-stutter-20261012/review-issue255-render-wait/`.
It documents the original indefinite-wait defect; it is not a GREEN result for this fix.

An exact source snapshot obtained with
`git show 6c9df1c30e80161602e3534009a9f73728b76d4e:shin-getter-mod-godot/src/Services/ShinGetterCombatRenderPreparation.cs`
is at `E:/Work/StS2 Mods/_issue-evidence/combat-stutter-20261012/issue255-p1-legacy-Service.cs`.
Override the linked service to reproduce RED:

```powershell
dotnet run --project tests/Issue255RenderWait/Issue255RenderWait.csproj --nologo -p:ProductionServicePath="E:/Work/StS2 Mods/_issue-evidence/combat-stutter-20261012/issue255-p1-legacy-Service.cs" -- --legacy
```

`--legacy` permits the old implementation's missing logic-frame subscription, but
does NOT bypass or shorten the no-draw deadline assertion. The old wrapper remained
incomplete with the host live after **12017.1171 ms**: expected exit 1 at that assertion.
After recording it, the fixture explicitly frees its own host to terminate the wait.
The current source completed at **10020.2736 ms**, cleaned up, and passed all **86**
assertions (1955 main-thread checks), exit 0.

Raw outputs: `issue255-p1-red-output.txt` and `issue255-p1-green-output.txt` in that
same owned evidence directory. The current static contract rejects 15 mutations.

The 10-second budget is observed at main-thread logic/draw callbacks; this prevents
an event wait hanging while engine logic continues. It cannot preempt a synchronous
native driver call or a completely stopped engine loop, and does not diagnose the
historical device-removal issue. Existing v19/v20 native measurements are preserved;
no performance matrix, whole-run, multiplayer or Beta rerun was performed.
