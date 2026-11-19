# issue#283 actual Harmony target-resolution gate

Run from the repository root, supplying the read-only v0.111.1 game data directory:

```powershell
dotnet run --project tests/Issue283HarmonyTargets/Issue283HarmonyTargets.csproj "-p:Sts2ReferenceDir=$gameData" --no-launch-profile
```

The probe compiles the unchanged production `ShinGetterTrialCombatSyncPatch.cs` into a separate test assembly. It references actual 0Harmony 2.4.2.0 and v0.111.1 sts2.dll, not fake Harmony attributes or native type doubles. GodotSharp 4.5.1 supplies managed type metadata only; the Godot engine is not started. Callback service bodies are throw-only placeholders and must never execute.

Pinned SHA-256 inputs:

- 0Harmony.dll: `EF1898322C9F5C86DC1B0758B272A9C440823B4A41CA9A0B82A3AA6B3D206387`.
- v0.111.1 sts2.dll: `3FB1FA1BF0249087B706A5D0F062A60579E2F5B08DF4EE477DDD6F2607F654D5`.

The actual Harmony PatchClassProcessor constructor merges each production class's attributes and supplies its normal-method default. The probe reads that processed HarmonyMethod and invokes the real PatchTools.GetOriginalMethod resolver. This includes the exact constructor attribute, rather than inferring a target from Postfix argument types. The pinned library's internal metadata APIs are intentionally checked strictly; a library change requires review, not an automatic fallback.

Coverage: all 7 production patch targets, all 9 Prefix/Postfix/Finalizer parameter contracts, and 2 actual-attribute negative controls. The missing constructor signature and a wrong final parameter type must both resolve to null. The production constructor must explicitly state `(RunLocationTargetedMessageBuffer, INetGameService, IPlayerCollection, IRunState, ulong, ulong)`; omitting localPlayerId from the Postfix itself is valid.

This is real-library metadata/target resolution, not execution of Patch/PatchAll, detours, mod Entry.Init, the game, PCK, multiplayer or save handling. It does not prove complete mod initialization. The separate Issue283TrialSync fixture tests modeled entry/controller behavior with Harmony attribute shims.

Candidate `4a70301b` logged a real initializer HarmonyException/Undefined target method. Its runtime admission is revoked/BLOCKED. The previous 31 business checks remain raw observations from partial initialization, not a candidate PASS; previous baseline RED remains valid. New runtime admission must be granted by the main development/automation flow using the newly reviewed product and explicit stderr/initialization gates, not merely ModManager's Loaded status. No old baseline or expanded gameplay matrix is run by this probe.
