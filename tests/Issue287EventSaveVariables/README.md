# issue#287 Event Save Variables Contracts

Run against the compiled candidate and the unchanged official 113 dependency directory:

```powershell
dotnet run --project tests/Issue287EventSaveVariables -p:ModAssemblyPath=<absolute-candidate-dll> -p:GameRuntimeDirectory=<official-data_sts2_windows_x86_64>
```

Uses the real official serializer and production capture, plus focused JSON negative controls.
The old candidate fails the filtered-empty native roundtrip. The only additional equivalence
is an empty object versus an omitted `variables` at the exact event-history DTO path.
Nonempty values, unrelated properties, arrays, types, titles, map flags and gameplay values
remain strict. Original native snapshot bytes remain available unchanged.

Optionally append `-- <captured-expected-native-json> <captured-readback-native-json>`
to compare a preserved diagnostic pair read-only. This is not a replacement for replaying
the actual event with the new candidate.

This is an offline CLR contract check, not a native write/Continue, event reward or gameplay pass.
