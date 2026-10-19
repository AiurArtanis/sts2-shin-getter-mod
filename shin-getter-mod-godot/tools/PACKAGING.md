# Runtime-only mod packaging

Use a clean, isolated checkout/archive with the intended `lib/sts2.dll` and
`lib/0Harmony.dll`. The scripts never select a game line or deploy automatically.

```powershell
.\tools\build_mod_artifacts.ps1 -GodotPath '<Godot mono console executable>' -OutputDirectory '<outside-project output directory>' -SourceRevisionId '<final commit>'
python -B .\tools\test_mod_pck.py --godot '<Godot mono console executable>'
```

`build_mod_artifacts.ps1` imports resources, compiles the DLL, creates the PCK and
manifest, then verifies the PCK in an empty project. Check its logs before separate
game-load validation and authorized deployment. It creates no ZIP, tag or release.

The packer keeps `.import` metadata plus its explicit runtime destinations for
textures and audio. Redundant PNG/MP3/WAV source copies and unreferenced import
cache/editor `.md5` files are not packed. This changes neither compression nor
image/audio quality. Direct-read JSON (including animation geometry), runtime
scenes/materials/shaders and the small project icon remain available. AtlasTexture
files are exported to binary `.res` with their original `.tres` logical remaps.

Missing/mismatched imports are errors; run Godot import instead of silently packing
raw media. Output must be outside scanned resources, or under the excluded `build/`
directory. `pck-report.json` records every packed file's bytes/SHA-256 and the full
PCK hash. `validate_mod_pck.gd` rejects inventory/hash mismatches and actually loads
every texture, audio stream and binary atlas without falling back to source media.

Standalone commands after importing:

```powershell
godot --headless --path '<imported project>' --script '<tools/build_mod_pck.gd>' -- '<output.pck>' '<report.json>'
godot --headless --path '<empty project with project.godot>' --script '<tools/validate_mod_pck.gd>' -- '<output.pck>' '<report.json>'
```
