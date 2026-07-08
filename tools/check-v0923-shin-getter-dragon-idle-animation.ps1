$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Require-Pattern {
    param(
        [string]$Description,
        [string]$RelativePath,
        [string]$Pattern
    )

    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $failures.Add("$Description (missing file: $RelativePath)")
        return
    }

    $content = Get-Content -LiteralPath $path -Raw
    if ($content -notmatch $Pattern) {
        $failures.Add($Description)
    }
}

function Require-AbsentPattern {
    param(
        [string]$Description,
        [string]$RelativePath,
        [string]$Pattern
    )

    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return
    }

    $content = Get-Content -LiteralPath $path -Raw
    if ($content -match $Pattern) {
        $failures.Add($Description)
    }
}

function Require-FileCount {
    param(
        [string]$Description,
        [string]$RelativePath,
        [string]$Filter,
        [int]$ExpectedCount
    )

    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        $failures.Add("$Description (missing directory: $RelativePath)")
        return
    }

    $count = @(Get-ChildItem -LiteralPath $path -File -Filter $Filter).Count
    if ($count -ne $ExpectedCount) {
        $failures.Add("$Description (expected $ExpectedCount, found $count)")
    }
}

function Require-NoFiles {
    param(
        [string]$Description,
        [string]$RelativePath,
        [string]$Filter
    )

    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        return
    }

    $count = @(Get-ChildItem -LiteralPath $path -File -Filter $Filter).Count
    if ($count -gt 0) {
        $failures.Add("$Description (found $count)")
    }
}

Require-Pattern 'Manifest version is v0.9.23 or later' 'ShinGetterMod.json' '"version"\s*:\s*"v0\.9\.(?:2[3-9]|[3-9][0-9])"'

$idleDir = 'images\characters\shin_getter\forms\shin_getter_dragon_idle'
$framesPath = 'scenes\creature_visuals\shin_getter_dragon_idle_frames.tres'
$scenePath = 'scenes\creature_visuals\shin_getter.tscn'
$sequencePath = 'src\Nodes\Combat\NShinGetterSpriteSequence.cs'
$visualsPath = 'src\Nodes\Combat\NShinGetterStaticVisuals.cs'

Require-FileCount 'Shin Getter Dragon idle exports exactly 36 png source frames' $idleDir 'sprite_*.png' 36
Require-NoFiles 'Shin Getter Dragon idle directory does not keep a 37th frame' $idleDir 'sprite_000037.png'

Require-Pattern 'Shin Getter Dragon baked SpriteFrames resource exists' $framesPath '\[gd_resource type="SpriteFrames" load_steps=37 format=3\]'
Require-Pattern 'Shin Getter Dragon baked idle references first source frame' $framesPath 'shin_getter_dragon_idle/sprite_000001\.png'
Require-Pattern 'Shin Getter Dragon baked idle references thirty-sixth source frame' $framesPath 'shin_getter_dragon_idle/sprite_000036\.png'
Require-Pattern 'Shin Getter Dragon baked idle plays forward then reverses from frame 36' $framesPath 'ExtResource\("35_f35"\)[\s\S]*ExtResource\("36_f36"\)[\s\S]*ExtResource\("36_f36"\)[\s\S]*ExtResource\("35_f35"\)'
Require-Pattern 'Shin Getter Dragon baked idle reverses back to first frame and loops at idle speed' $framesPath 'ExtResource\("2_f02"\)[\s\S]*ExtResource\("1_f01"\)[\s\S]*"loop": true[\s\S]*"name": &"idle"[\s\S]*"speed": 24\.0'

Require-Pattern 'Creature scene loads Shin Dragon baked idle SpriteFrames' $scenePath 'shin_getter_dragon_idle_frames\.tres'
Require-Pattern 'ShinDragon is AnimatedSprite2D in creature scene' $scenePath '\[node name="ShinDragon" type="AnimatedSprite2D"'
Require-Pattern 'ShinDragon assigns baked idle SpriteFrames' $scenePath 'ShinDragon[\s\S]*sprite_frames = ExtResource\("8_dragon_idle_frames"\)'
Require-Pattern 'ShinDragon starts on idle animation' $scenePath 'ShinDragon[\s\S]*animation = &"idle"[\s\S]*autoplay = "idle"'
Require-Pattern 'ShinDragon scene metadata points at 36 source idle frames' $scenePath 'ShinDragon[\s\S]*metadata/frame_directory = "res://images/characters/shin_getter/forms/shin_getter_dragon_idle"[\s\S]*metadata/max_frames = 36'
Require-AbsentPattern 'ShinDragon scene no longer uses the static Sprite2D node' $scenePath '\[node name="ShinDragon" type="Sprite2D"'

Require-Pattern 'Sprite sequence declares Shin Dragon idle frame directory' $sequencePath 'ShinDragonIdleFrameDirectory\s*=.*shin_getter_dragon_idle'
Require-Pattern 'Sprite sequence declares Shin Dragon idle source frame cap' $sequencePath 'ShinDragonIdleMaxFrames\s*=\s*36'
Require-Pattern 'Sprite sequence exposes Shin Dragon loader' $sequencePath 'EnsureShinDragonLoaded'
Require-Pattern 'Sprite sequence loads Shin Dragon looping ping-pong idle animation from 36 frames' $sequencePath 'EnsureShinDragonLoaded[\s\S]*LoadPingPongAnimation\(frames,\s*IdleAnimationName,\s*ShinDragonIdleFrameDirectory,\s*ShinDragonIdleMaxFrames,\s*IdleFramesPerSecond,\s*loop:\s*true\)'

Require-Pattern 'Static visuals initializes Shin Dragon idle frames during form lookup' $visualsPath 'shinDragonAnimation[\s\S]*EnsureShinDragonLoaded'
Require-Pattern 'Static visuals activates Shin Dragon with its own idle loader' $visualsPath 'animation\.Name == "ShinDragon"[\s\S]*EnsureShinDragonLoaded'

Require-Pattern 'PCK validator checks Shin Dragon first idle frame' 'tools\validate-mod-resources.gd' 'shin_getter_dragon_idle/sprite_000001\.png'
Require-Pattern 'PCK validator checks Shin Dragon last source idle frame' 'tools\validate-mod-resources.gd' 'shin_getter_dragon_idle/sprite_000036\.png'
Require-Pattern 'PCK validator checks Shin Dragon baked SpriteFrames resource' 'tools\validate-mod-resources.gd' 'shin_getter_dragon_idle_frames\.tres'
Require-Pattern 'PCK validator rejects stale Shin Dragon 37th idle frame' 'tools\validate-mod-resources.gd' 'shin_getter_dragon_idle/sprite_000037\.png'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.23+ / Shin Getter Dragon idle animation checks failing:'
    $failures | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host 'GREEN: v0.9.23+ / Shin Getter Dragon idle animation checks passed.'
