# issue#192: native saved-property component regression

Build the isolated production project against the official stable `0.111.1`
`sts2.dll` and Harmony dependency, then run this console test against that output:

```powershell
dotnet build shin-getter-mod-godot/ShinGetterMod.csproj --nologo -v:q
dotnet run --project tests/Issue192SavedProperties/Issue192SavedProperties.csproj "-p:ModOutput=$modOutput" "-p:Sts2ReferenceDir=$gameAssemblies"
python shin-getter-mod-godot/tools/validate_issue_192.py
```

`$modOutput` is the isolated directory containing `ShinGetterMod.dll`;
`$gameAssemblies` is the matching official game assembly directory. Do not point
build output at a shared deployment or frozen acceptance runtime.

The test uses the actual product assembly, the native `SavedPropertiesTypeCache`
pipeline, `SavedProperties.From`, JSON restoration and packet encoding/decoding.
The stable API now registers properties through the native pipeline; the old
109 mod postfix is not installed. The console fixture stubs Godot environment
queries and output, starts no game, and does not create or edit a run save.

Coverage:

- Every concrete product `AbstractModel`, including inherited saved properties.
- Native registration, inherited properties, deterministic cache hash in reverse model order.
- A relic without its own saved attributes retains `IsWax`/`IsMelted` coverage.
- Empty and nonempty Good Citizen purchase history; independent restored lists.
- Non-voice positive save controls alongside nonzero local voice history.
- Shared evolution, card progress, and disabled event invasion persistence.
- Actual `SavedProperties` packet round trip.

This does not verify BaseLib coexistence, native `SaveRun`, a new process loading
the save, merchant transactions, or event navigation. Different peers must use
the same game/mod configuration.

Historical 109 development results (2026-09-07, old implementation):

- Baseline `bcd321a7`: the component test fails with
  `cache missing: SGC_InfiniteEvolution`; the expanded static check fails with
  `109 production saved-property registration is missing`.
- Fixed: standalone 204 assertions; pre-registered 225; post-registered 205.
  All pass, with 236 concrete models and 14 types containing saved properties.
- Normal production and component builds: 0 warnings, 0 errors.

Original acceptance failure remains recorded separately: real 109 RYOMA reward
followed by native save omitted all Good Citizen `props`. Its result SHA-256 is
`956F59CD0D809618F2F9DC795DE0CD2AA0F0118F5106DFB53320BE358789D573`.
The dedicated automation executor must repeat the complete failed behavior on
the reviewed candidate. Beta runtime testing is outside this change's scope.
