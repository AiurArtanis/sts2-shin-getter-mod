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

Require-Pattern 'Manifest version is v0.9.24 or later' 'ShinGetterMod.json' '"version"\s*:\s*"v0\.9\.(?:2[4-9]|[3-9][0-9])"'

$idleDir = 'images\characters\shin_getter\forms\getter_three_idle'
$framesPath = 'scenes\creature_visuals\shin_getter_three_idle_frames.tres'
$scenePath = 'scenes\creature_visuals\shin_getter.tscn'
$sequencePath = 'src\Nodes\Combat\NShinGetterSpriteSequence.cs'
$visualsPath = 'src\Nodes\Combat\NShinGetterStaticVisuals.cs'

Require-FileCount 'Getter Three idle exports exactly 24 png source frames' $idleDir 'sprite_*.png' 24
Require-NoFiles 'Getter Three idle directory does not keep a 25th frame' $idleDir 'sprite_000025.png'

Require-Pattern 'Getter Three baked SpriteFrames resource exists' $framesPath '\[gd_resource type="SpriteFrames" load_steps=25 format=3\]'
Require-Pattern 'Getter Three baked idle references first source frame' $framesPath 'getter_three_idle/sprite_000001\.png'
Require-Pattern 'Getter Three baked idle references twenty-fourth source frame' $framesPath 'getter_three_idle/sprite_000024\.png'
Require-Pattern 'Getter Three baked idle plays forward then reverses from frame 24' $framesPath 'ExtResource\("23_f23"\)[\s\S]*ExtResource\("24_f24"\)[\s\S]*ExtResource\("24_f24"\)[\s\S]*ExtResource\("23_f23"\)'
Require-Pattern 'Getter Three baked idle reverses back to first frame and loops at idle speed' $framesPath 'ExtResource\("2_f02"\)[\s\S]*ExtResource\("1_f01"\)[\s\S]*"loop": true[\s\S]*"name": &"idle"[\s\S]*"speed": 24\.0'

Require-Pattern 'Creature scene loads Getter Three baked idle SpriteFrames' $scenePath 'shin_getter_three_idle_frames\.tres'
Require-Pattern 'GetterThree is AnimatedSprite2D in creature scene' $scenePath '\[node name="GetterThree" type="AnimatedSprite2D"'
Require-Pattern 'GetterThree assigns baked idle SpriteFrames' $scenePath 'GetterThree[\s\S]*sprite_frames = ExtResource\("5_three_idle_frames"\)'
Require-Pattern 'GetterThree starts on idle animation' $scenePath 'GetterThree[\s\S]*animation = &"idle"[\s\S]*autoplay = "idle"'
Require-Pattern 'GetterThree scene metadata points at 24 source idle frames' $scenePath 'GetterThree[\s\S]*metadata/frame_directory = "res://images/characters/shin_getter/forms/getter_three_idle"[\s\S]*metadata/max_frames = 24'
Require-AbsentPattern 'GetterThree scene no longer uses the static Sprite2D node' $scenePath '\[node name="GetterThree" type="Sprite2D"'

Require-Pattern 'Sprite sequence declares Getter Three idle frame directory' $sequencePath 'GetterThreeIdleFrameDirectory\s*=.*getter_three_idle'
Require-Pattern 'Sprite sequence declares Getter Three idle source frame cap' $sequencePath 'GetterThreeIdleMaxFrames\s*=\s*24'
Require-Pattern 'Sprite sequence exposes Getter Three loader' $sequencePath 'EnsureGetterThreeLoaded'
Require-Pattern 'Sprite sequence loads Getter Three looping ping-pong idle animation from 24 frames' $sequencePath 'EnsureGetterThreeLoaded[\s\S]*LoadPingPongAnimation\(frames,\s*IdleAnimationName,\s*GetterThreeIdleFrameDirectory,\s*GetterThreeIdleMaxFrames,\s*IdleFramesPerSecond,\s*loop:\s*true\)'

Require-Pattern 'Static visuals initializes Getter Three idle frames during form lookup' $visualsPath 'getterThreeAnimation[\s\S]*EnsureGetterThreeLoaded'
Require-Pattern 'Static visuals activates Getter Three with its own idle loader' $visualsPath 'animation\.Name == "GetterThree"[\s\S]*EnsureGetterThreeLoaded'

Require-Pattern 'PCK validator checks Getter Three first idle frame' 'tools\validate-mod-resources.gd' 'getter_three_idle/sprite_000001\.png'
Require-Pattern 'PCK validator checks Getter Three last source idle frame' 'tools\validate-mod-resources.gd' 'getter_three_idle/sprite_000024\.png'
Require-Pattern 'PCK validator checks Getter Three baked SpriteFrames resource' 'tools\validate-mod-resources.gd' 'shin_getter_three_idle_frames\.tres'
Require-Pattern 'PCK validator rejects stale Getter Three 25th idle frame' 'tools\validate-mod-resources.gd' 'getter_three_idle/sprite_000025\.png'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.24+ / Getter Three idle ping-pong checks failing:'
    $failures | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host 'GREEN: v0.9.24+ / Getter Three idle ping-pong checks passed.'
