# issue#238 formal109 empty event-variable contracts

This is a pure CLR executable linking only the production save verifier and
unchanged map-scope helper. It never builds/references the mod project, starts
Godot, installs Harmony, writes saves, or executes the event route. Supply an
unchanged formal109 dependency directory (`sts2.dll`, `GodotSharp.dll`,
`Sentry.dll`, `Steamworks.NET.dll`, `0Harmony.dll`); the native SHA and MVID are
checked at runtime. Keep build output and obj outside sealed inputs.

```powershell
dotnet run --project tests/Issue238EventSaveVariables -p:FormalGameDirectory=<formal109-directory> -- --evidence-manifest <preserved-Comparison001-manifest>
```

The optional manifest mode reads the four sealed byte arrays without editing
them, validates their hashes and all 51 recorded map flags, runs the actual
production Compare, and rejects true/missing mutations of every recorded false
map flag. Raw original NativeJson arrays remain in diagnostic comparisons.

The map-free DTO fixture uses the actual official109 serializer, production
Capture, and a test-only native DTO roundtrip to expose filtered-empty variables.
No DTO roundtrip is added to production. Other checks cover exact-path empty
object/missing in both directions, null/arrays/nonempty values, similar paths,
wrong container shapes, adjacent fields, whole-tree gameplay/RNG/economy,
numeric lexemes and duplicate-key rejection.

For an expected-RED comparison, build the same test project with
`-p:VerifierSource=<unchanged-f6266fa8-verifier.cs>` and separate external
output/obj paths, then run with `--baseline-red <same-manifest>`. It must reject
the real frozen pair at exactly the archived variables path. Exit zero means
the expected old failure was observed; it is not a gameplay PASS.

This proves only an offline source/CLR comparison repair. Production save,
candidate presentation, payment/reward, Owner handling and normal Continue
require independent review and a new native attempt. Preserve the original RED.
