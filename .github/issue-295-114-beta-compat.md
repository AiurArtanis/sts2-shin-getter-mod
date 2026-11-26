# issue#295: game 0.114 Beta compatibility

Development candidate only. The base is the released 113 Beta commit
`6a9356c47e1ffa5362e02547c9615b6516fa074b`, which includes the card-frame tint fix.
The candidate manifest targets game `0.114.0`; it is not a release announcement.

## Source and binding audit

The 113.1-to-114 source manifest has 192 changed, 13 added and 2 removed `src/**/*.cs` files.
The game project file, Harmony, and the Godot managed runtime did not change. The
new game reference is the Steam 0.114.0 `sts2.dll`, SHA-256
`DDCEB87905B6096DEB7BE975922E24A53992453675DF26DF545B1C46B1826C5D`.
The unmodified 113 product builds against it, which does not establish behavior compatibility.
The 114 mode of the existing binary and reflection probe inventories the product DLL's
type/member/override edges and resolves Harmony targets, parameter bindings and transpilers.
It writes its own report without updating the checked-in 113 baseline.

| Area | Compatibility decision |
| --- | --- |
| `CreatureCmd.Damage` | 114 computes `DamageResults` / `HpLossResult` before the normal `BeforeDamageReceived` dispatch. Only Fighting Spirit's counter is awaited at the earlier per-target modifier boundary. Native block, result application, the other listeners and counter damage modifiers stay in their original pipelines. |
| HP-loss preview | `DamageVar` and its variants now use `GetDamageResultsPreview`, which runs the normal HP-loss listeners without applying the results. Indomitable, Activated Sapphire and Musashi Clone predict their result without mutating state in this synchronous scope. |
| `AttackCommand` / `AbstractModel` | Tests exercise a real counter kill during a multihit attack and native attacker cleanup. Result consumers still receive grouped `DamageResult` entries and use actual HP loss. The new `BeforeActEntered` and the later `AfterActEntered` have no mod override or subscriber. |
| Events and multiplayer | The new `ChooseOrFinish` wrapper still starts the chosen option before the same history call. The mod's checkpoint/trial hooks bind to `ChooseOptionForEvent` and `SaveEventOptionToHistory`, not the replaced direct `EventOption.Chosen` call. Native faults now finish the event; cancellation remains distinct. |
| Treasure and Blitz shops | The original game owns the new two-round multiplayer Blitz vote, round-index wire data and late/future vote handling. The mod does not replace these types. Blitz slot hover now follows the owner, while the mod's shop reactions and Good Citizen Card remain excluded from Blitz. |
| Character selection and card frames | Normal/Blitz random-button setup moves to submenu open and native ascension focus changes. The mod's selection and background hooks still bind; none depends on the old random initialization point. `NCard` and the mod's atlas/tint assets are unchanged by this adaptation. |
| Progress, history and run saves | 24->25, 11->12 and 21->22 migrate `CARD.RAPID_SHOT` to `CARD.TWIN_SHOT`. The mod does not reference either ID. Player and mod event serializer hooks remain bound; cloud history selection by profile identity is native, not intercepted. |

## Development verification

Use the official game dependency directory, and put its `sts2.dll` and `0Harmony.dll`
in the candidate's ignored `shin-getter-mod-godot/lib` before building. In PowerShell
from the worktree:

```powershell
dotnet build shin-getter-mod-godot/ShinGetterMod.csproj -c ExportRelease
$mod = (Resolve-Path shin-getter-mod-godot/build/ShinGetterMod.dll).Path
dotnet run --project tools/Issue287CompatibilityProbe -- --official-114 --mod $mod --dependencies $gameDependencies --run-output $auditOutput
$env:STS2_114_GAME_DIRECTORY = $gameDependencies
$env:STS2_114_MOD_DLL = $mod
dotnet build tools/Issue295CompatibilityProbe/Issue295CompatibilityProbe.csproj
& $godot451MonoConsole --headless --path tools/Issue295CompatibilityProbe --quit-after 1800
```

The standalone Godot fixture runs official methods with synthetic inputs:
six production damage patch classes, lethal/nonlethal counters, Vigor and Strength,
multihit cancellation and cleanup, the three HP-loss protections, the three save
migrations, event fault versus cancellation, and relic choices/passes in both packet
rounds. It must print `ISSUE295 MANAGED BEHAVIOR PROBE PASS`. It loads no other mod,
does not invoke the mod's normal entry point or export a PCK, and does not read live saves.

Before acceptance, verify the loaded-mod inventory and complete initialization in the
actual game; check normal and Blitz character selection, UI and card borders including
selected attack/power cards; and run an actual multiplayer event/history/continue and
Blitz chest vote across both rounds. The developer fixture does not certify these
end-to-end paths.
