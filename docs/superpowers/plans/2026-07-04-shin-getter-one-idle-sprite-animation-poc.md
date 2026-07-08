# Shin Getter One Idle Sprite Animation POC Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Shin Getter One's combat static visual with a looping 2D idle sprite sequence while leaving other forms unchanged.

**Architecture:** Keep the existing `fallback.tscn`-based creature visual scene. Convert only the `GetterOne` child from `Sprite2D` to `AnimatedSprite2D`, build its `SpriteFrames` from PNG files under the mod resource tree when the form-switch helper sees the animated node, and update the form-switch helper to work with generic `CanvasItem`/`Node2D` visual nodes. The scene should not directly attach mod runtime C# scripts.

**Tech Stack:** Godot 4.5.1 Mono, C#, `.tscn` scenes, PNG sprite frames, PowerShell regression checks.

---

### Task 1: Add Regression Guard

**Files:**
- Create: `tools/check-shin-getter-one-idle-animation-poc.ps1`

- [ ] **Step 1: Write the failing check**

Create a PowerShell script that checks the scene, loader class, imported frame directory, and form-switch compatibility:

```powershell
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Read-RepoFile([string]$relativePath) {
    $path = Join-Path $root $relativePath
    if (!(Test-Path -LiteralPath $path)) { return $null }
    return Get-Content -Raw -Encoding UTF8 -LiteralPath $path
}

function Require-Pattern([string]$name, [string]$relativePath, [string]$pattern) {
    $text = Read-RepoFile $relativePath
    if ($null -eq $text -or $text -notmatch $pattern) { $failures.Add($name) }
}

$frameDir = Join-Path $root 'images\characters\shin_getter\forms\getter_one_idle'
$frameCount = 0
if (Test-Path -LiteralPath $frameDir) {
    $frameCount = @(Get-ChildItem -LiteralPath $frameDir -File -Filter 'sprite_*.png').Count
}
if ($frameCount -lt 200) { $failures.Add("Getter One idle frame count is at least 200 (found $frameCount)") }

Require-Pattern 'GetterOne is AnimatedSprite2D in creature scene' 'scenes\creature_visuals\shin_getter.tscn' '\[node name="GetterOne" type="AnimatedSprite2D"'
Require-Pattern 'GetterOne animation path points at imported idle frames' 'scenes\creature_visuals\shin_getter.tscn' 'getter_one_idle'
Require-Pattern 'Runtime loader creates SpriteFrames' 'src\Nodes\Combat\NShinGetterSpriteSequence.cs' 'new\s+SpriteFrames\(\)'
Require-Pattern 'Runtime loader sorts frames by file name' 'src\Nodes\Combat\NShinGetterSpriteSequence.cs' 'OrderBy\([^)]*Path\.GetFileName'
Require-Pattern 'Runtime loader starts idle animation' 'src\Nodes\Combat\NShinGetterSpriteSequence.cs' 'Play\(AnimationName\)'
Require-Pattern 'Static visual helper configures GetterOne animation' 'src\Nodes\Combat\NShinGetterStaticVisuals.cs' 'NShinGetterSpriteSequence\.EnsureLoaded'
Require-Pattern 'Static visuals uses CanvasItem form nodes' 'src\Nodes\Combat\NShinGetterStaticVisuals.cs' 'CanvasItem'
Require-Pattern 'Static visuals still animates scale through Node2D' 'src\Nodes\Combat\NShinGetterStaticVisuals.cs' 'Node2D'

if ($failures.Count -gt 0) {
    Write-Host 'RED: Shin Getter One idle sprite animation POC checks failing:'
    $failures | Select-Object -First 80
    exit 1
}

Write-Host 'GREEN: Shin Getter One idle sprite animation POC checks passed.'
```

- [ ] **Step 2: Run the check to verify it fails**

Run: `.\tools\check-shin-getter-one-idle-animation-poc.ps1`

Expected: `RED` because the loader, frames, and scene conversion do not exist yet.

### Task 2: Import Frames And Runtime Loader

**Files:**
- Copy: `D:\Library\Pictures\杀戮尖塔2-素材\anim-sprite\sprites\一号机\待机\sprite_*.png` to `images/characters/shin_getter/forms/getter_one_idle/`
- Create: `src/Nodes/Combat/NShinGetterSpriteSequence.cs`

- [ ] **Step 1: Copy only runtime frame PNGs**

Run:

```powershell
New-Item -ItemType Directory -Force -Path 'images\characters\shin_getter\forms\getter_one_idle' | Out-Null
Copy-Item -LiteralPath 'D:\Library\Pictures\杀戮尖塔2-素材\anim-sprite\sprites\一号机\待机\sprite_*.png' -Destination 'images\characters\shin_getter\forms\getter_one_idle'
```

Expected: `241` `sprite_*.png` files in the destination directory.

- [ ] **Step 2: Add the loader script**

Create `NShinGetterSpriteSequence` as a small runtime helper that loads PNG textures from the Getter One idle folder, builds a looping `idle` animation, and starts playback on the supplied `AnimatedSprite2D`.

- [ ] **Step 3: Run the check**

Run: `.\tools\check-shin-getter-one-idle-animation-poc.ps1`

Expected: still `RED` until the scene and switch code are updated.

### Task 3: Convert GetterOne Scene Node

**Files:**
- Modify: `scenes/creature_visuals/shin_getter.tscn`
- Modify: `tools/validate-mod-resources.gd`

- [ ] **Step 1: Change the node type**

Change only `GetterOne` from `Sprite2D` to `AnimatedSprite2D`, keep the existing position/scale, and store `metadata/frame_directory = "res://images/characters/shin_getter/forms/getter_one_idle"` for scene readability. Do not attach the loader script directly to the scene.

- [ ] **Step 2: Keep resource validation aware of the loader and first frame**

Add the loader script and first idle PNG to required resource validation.

- [ ] **Step 3: Run the check**

Run: `.\tools\check-shin-getter-one-idle-animation-poc.ps1`

Expected: still `RED` until the switch helper is updated.

### Task 4: Update Form Switch Helper

**Files:**
- Modify: `src/Nodes/Combat/NShinGetterStaticVisuals.cs`

- [ ] **Step 1: Generalize visual node lookup**

Replace `Sprite2D`-specific fields with a small record carrying `CanvasItem Item` and `Node2D Node`, so static sprites and animated sprites can share visibility, alpha, scale, and rotation transitions.

- [ ] **Step 2: Run the POC check**

Run: `.\tools\check-shin-getter-one-idle-animation-poc.ps1`

Expected: `GREEN`.

### Task 5: Build And Deploy Verification

**Files:**
- No additional code files.

- [ ] **Step 1: Compile**

Run: `dotnet build .\ShinGetterMod.csproj`

Expected: `0 warning / 0 error`.

- [ ] **Step 2: Run focused and existing checks**

Run:

```powershell
.\tools\check-shin-getter-one-idle-animation-poc.ps1
.\tools\check-v0908-2026-07-02-feedback.ps1
```

Expected: both `GREEN`.

- [ ] **Step 3: Deploy**

Run: `.\build-and-deploy.ps1`

Expected: DLL/PCK/JSON deploy to `E:\Work\Godot\Godot_v4.5.1-stable_mono_win64\mods\ShinGetterMod`.
