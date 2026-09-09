# issue#192: native 109 saved-property component regression

Build the isolated production project against the official 109 `sts2.dll` and
its normal Harmony dependency, then run this console test against that output:

```powershell
dotnet build shin-getter-mod-godot/ShinGetterMod.csproj --nologo -v:q
dotnet build tests/Issue192SavedProperties --nologo -v:q
dotnet tests/Issue192SavedProperties/bin/Debug/net9.0/Issue192SavedProperties.dll standalone
dotnet tests/Issue192SavedProperties/bin/Debug/net9.0/Issue192SavedProperties.dll pre-registered
dotnet tests/Issue192SavedProperties/bin/Debug/net9.0/Issue192SavedProperties.dll post-registered
python shin-getter-mod-godot/tools/validate_issue_192.py
```

`ModOutput` can override the test project's reference directory. Do not point
build output at a shared deployment or frozen acceptance runtime.

The test uses the actual product assembly, 109 cache, `SavedProperties.From`,
JSON serialization/restoration, packet writer/reader, and Harmony startup hook.
It starts no game, broker, or test bridge. Model fixtures supply component input;
the test does not create or edit a run save. A passing result does not establish
native `SaveRun`, new-process loading, merchant transactions, or event navigation.

Coverage:

- Every concrete product `AbstractModel`, including inherited saved properties.
- Native attribute/property ordering, existing IDs, repeated initialization.
- A relic without its own saved attributes retains `IsWax`/`IsMelted` coverage.
- Empty and nonempty Good Citizen purchase history; independent restored lists.
- Non-voice positive save controls alongside nonzero local voice history.
- Shared evolution, card progress, and disabled event invasion persistence.
- Actual `SavedProperties` packet round trip and property ID field width.
- Pre-registered native cache entries and a width boundary (66 names, 7 bits).
- Later repeated registration in reverse model order preserves assigned IDs.

The coexistence modes simulate BaseLib's calls to the native registration API;
they are not a claim of full integration with a running BaseLib installation.
The production postfix runs after BaseLib's `ModelDb.InitIds` prefix, leaves
existing property IDs untouched, and recalculates width using the complete map.
Different peers must still use the same game/mod configuration.

Recorded development results (2026-09-07):

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
