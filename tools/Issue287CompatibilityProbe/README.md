# Issue 287 Compatibility Probe

Read-only offline audit of the compiled ShinGetterMod against the official 113
`lib/sts2.dll`. It does not build the product, launch a game, call `Entry.Init`,
apply Harmony patches, execute patched callbacks, install another mod or deploy.

Run from the worktree root with .NET 9 runtime / SDK:

```powershell
dotnet run --project tools/Issue287CompatibilityProbe -- --root . --dependencies 'E:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64'
```

Optional `--mod <absolute-dll-path>` selects another compiled product; its portable
PDB must be adjacent. The DLL SHA and source document checksums identify the exact
snapshot. Any source/PDB mismatch is a failed gate. Semantic outputs overwrite only
`.github/issue-287-dynamic-audit.json` and `.github/issue-287-dynamic-audit.md`.
Per-run SHA/MVID/token/path/time evidence goes to a unique timestamped directory
under `E:/Work/outputs/issue113-beta-20261113/dynamic-probe-run/`, configurable with
`--run-output <directory>`. No product SHA, MVID, timestamp or metadata token is a
semantic baseline equality field. Official game/Harmony pins remain strict.

`--check` compares only the normalized, sorted TypeRef/MemberRef/override/Harmony/
dynamic binding graph fingerprint with the saved semantic baseline. It does not
write the baseline. A legal SourceRevisionId rebuild is not a compatibility failure
just because its product SHA or MVID changes. Exit 2 means semantic drift/missing
baseline; exit 1 means actual audit failures. Runtime pending can coexist with exit 0.
Exit 1 means a failed gate; exit 0 may still have explicitly unresolved gates.

The probe reuses the retained Issue93 approach of actual CLR token resolution and
the Issue283 approach of real Harmony class-processor / original-target resolution.
It adds Cecil signature/MethodImpl checks, portable-PDB source checksums, semantic
source and compiled-IL reflection inventories, real bounded TargetMethod(s) helper
execution, hook argument checks and production transpiler enumeration against
original official IL. Negative controls cover missing members and wrong signatures
with both Cecil and actual Harmony.

Local build dependencies (no automatic installs):

- Actual product `lib/0Harmony.dll`, SHA pinned in `Program.cs`.
- Mono.Cecil 0.11.4 from the existing Microsoft.CodeCoverage 17.11.1 cache.
  Override `-p:CecilPath=<path>` if needed.
- Roslyn from the installed .NET SDK 9.0.321.
  Override `-p:ProbeRoslynDirectory=<sdk>/Roslyn/bincore` if needed.
- Steam's official dependency directory is opened read-only. It never supplies a
  substitute `sts2.dll`; the game's actual loaded path is the pinned worktree lib.

Method existence, overload binding, hook argument compatibility, offline IL
transformation, native Harmony wrapper application and gameplay are distinct gates.
Reflection of instance values, optional St2bs integration and Godot callbacks are
not claimed by offline success. All unresolved sites remain in the output.

`expectedHarmonyRegistry` is a machine-readable declaration contract, not an
installed-patch inventory. For each original it supplies canonical signature
and `prefix` / `postfix` / `transpiler` / `finalizer` callback lists under owner
`Artanis.ShinGetterMod`. A missing declaration target makes the contract explicitly
incomplete. Runtime observers match by signature, obtain the actual frozen mod SHA/
MVID from that run's external engineering report, and independently query
`Harmony.GetPatchInfo`; offline parameter/transpiler failures are not waived
by successful contract extraction.

The official game's managed module initializer prints a Sentry GDExtension-not-loaded
warning in this standalone CLR process. That warning is recorded as an environment
boundary, not a native game/mod initialization result. The before/after Harmony
registry remains unchanged by this probe.

The first preserved Grid failure was a temporary adaptation regression from a
derived ReflectedType lookup. The original base-type binding was correct; this is
not classified as an original 111 mod defect or official 113 break. History property
metadata and nested reflection return types are bounded separately from actual
native instance invocation and rollback acceptance.
