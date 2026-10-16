# issue#257 card timing regression

Run from the repository root:

```powershell
dotnet run --project tests/Issue257CardTiming/Issue257CardTiming.csproj
python -B tests/Issue257CardTiming/test_contracts.py
```

The executable links the real Expansion Strike and Shining Spark card sources.
Deterministic command, voice and VFX doubles verify conditional voice waits,
impact callback ordering, initial-hit/impact concurrency, unchanged damage and
Ki followups, visual fallback and failure cleanup. Three source gates cover the
actual voice routing and VFX/sequence implementations excluded from the doubles.

This is a focused CPU regression, not a new game run or a native rendering,
audio, GPU-performance or multiplayer acceptance result. Existing R3 native
recordings remain separate historical evidence. No game, OBS or shared deployment
is started or changed by these tests.
