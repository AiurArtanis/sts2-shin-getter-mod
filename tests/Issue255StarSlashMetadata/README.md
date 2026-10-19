# issue#255 Star Slash metadata / geometry regression

Run from the worktree root after the formal109 production build:

```powershell
python -B tests/Issue255StarSlashMetadata/prepare.py
dotnet run --project tests/Issue255StarSlashMetadata/Issue255StarSlashMetadata.csproj --nologo
```

An optional single argument writes the result JSON to an explicitly selected evidence
path. `prepare.py` extracts exact baseline `02d97982` data/sequence and both legacy
JSONs into excluded `obj/` regression inputs. Only class names are changed to compile
old and current production sources together. These inputs are generated, not hand-built
copies of the algorithm. The current files are directly linked by the project.

The fixture uses actual installed GodotSharp Vector2/Rect2/Color structs, without
starting an engine. It compares actual production parsing, frame weights/phase times,
float32 contours/cleaning under H/V/HV and offset, and continuous BuildWeapon output.
Within-frame start/middle/end and 1/8 subdivisions must remain exact; a frame-only
geometry cache cannot pass. Actual malformed runtime schemas/layers/references are
passed to the production constructor and must fail.

The linked sequence runs against explicitly fake native scene/triangulation/union/draw
APIs. It checks 9408 old/current draw submission cases across frames, continuous poses,
centering, offsets and flips, including atlas UVs, repeat-pose cache hits and exit cleanup.
Native union code is independently required to remain identical by the Python gate.
Fake triangulation returns fan indices and fake union keeps contours separate; these
results are instrumentation only, never evidence that concave polygons triangulate or
render correctly in the native engine. Actual submitted source arrays are compared.

Reported parse/BuildWeapon timings and allocations are managed measurements;
instrumented draw allocations include stub logging and exclude native marshaling/GPU.
Cache payload bytes count vector/index elements only, excluding object headers, texture
memory and dictionaries. Native performance and human visual/input acceptance remain
with the separate test chat. No game, graphics process, PCK or deployment is launched.
