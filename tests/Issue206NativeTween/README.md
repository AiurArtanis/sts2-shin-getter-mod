# issue#206 native Tween API regression

An isolated Godot 4.5.1 / .NET 9 fixture. It links the exact production
`ShinGetterContentTweenGuard.cs` and creates real Godot `Control`/`Tween` objects.
No `sts2`, Harmony, mod DLL, game assets, player saves, PCK or deployment is loaded.
This is API behavior evidence, not rendered game UI or physical input acceptance.

From the repository root:

```powershell
dotnet build tests/Issue206NativeTween/Issue206NativeTween.csproj --nologo
& 'E:/Work/Godot/Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64_console.exe' --headless --path "$PWD/tests/Issue206NativeTween" -- --legacy
& 'E:/Work/Godot/Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64_console.exe' --headless --path "$PWD/tests/Issue206NativeTween"
```

`--legacy` deliberately applies the old `recenter -> native CustomStep -> new tween`
order and must exit 1. It reproduces stale X in fresh-resume, width growth and shrink.
The normal run calls the linked production guard before recentering, must exit 0,
and verifies the old field is null, old tween is killed, stale focus callback is not
fast-forwarded, and the new target/midpoint/end X all match the desired center.
Expected positive result: 19 assertions. All controls belong to the fixture scene.

`validate_issue_206_tween_order.py` locks both production call paths to
`Stop -> measure/recenter -> AnimateLine`, and six mutation cases reject regressions.
The fixture mirrors only official109's relevant native animation ordering; it does
not execute NAncientEventLayout itself. The production guard is executed unchanged.
