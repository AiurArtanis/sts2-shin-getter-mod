# issue#124 managed regression harness

Run after building the isolated mod project against the formal109 reference:

```powershell
dotnet build shin-getter-mod-godot/ShinGetterMod.csproj --no-restore
dotnet run --project tests/Issue124VoiceMask -- `
  shin-getter-mod-godot/images/characters/shin_getter/forms/getter_one_star_slash/animation.json `
  shin-getter-mod-godot/images/characters/shin_getter/forms/shin_getter_dragon_star_slash/animation.json
```

The harness reflects the actual built production `VoiceHistoryMasks` and
`NShinGetterStarSlashData` code. It covers all64 cues against every other cue,
the released31+31 mapping, new signed bits, both out-of-range APIs, weighted
frame lookup, coverage/foreground polygon self-intersections, production weapon
geometry at frame start/midpoint/end/hold, and invalid metadata rejection. The two delivered JSON arguments
are needed to cover actual147-frame data; omitting them tests only synthetic
fixtures and must not be reported as delivered-data validation.

No Godot scene, native rendering, audio playback, game initialization, PCK or
deployment is started. The signed-mask assertions validate integer values,
not a native `SavedProperties` save/load roundtrip. Real selection controls,
voice timing, held pose and weapon/hand pixels remain separate acceptance work.
