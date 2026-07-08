$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Read-RepoFile([string]$relativePath) {
    $path = Join-Path $root $relativePath
    if (!(Test-Path -LiteralPath $path)) {
        return $null
    }

    return Get-Content -Raw -Encoding UTF8 -LiteralPath $path
}

function Require-Pattern([string]$name, [string]$relativePath, [string]$pattern) {
    $text = Read-RepoFile $relativePath
    if ($null -eq $text -or $text -notmatch $pattern) {
        $failures.Add($name)
    }
}

function Require-AbsentPattern([string]$name, [string]$relativePath, [string]$pattern) {
    $text = Read-RepoFile $relativePath
    if ($null -ne $text -and $text -match $pattern) {
        $failures.Add($name)
    }
}

function Require-FileCount([string]$name, [string]$relativePath, [string]$filter, [int]$expected) {
    $path = Join-Path $root $relativePath
    if (!(Test-Path -LiteralPath $path)) {
        $failures.Add($name)
        return
    }

    $count = (Get-ChildItem -LiteralPath $path -File -Filter $filter | Measure-Object).Count
    if ($count -ne $expected) {
        $failures.Add("$name (expected $expected, got $count)")
    }
}

$sequencePath = 'src\Nodes\Combat\NShinGetterSpriteSequence.cs'
$scenePath = 'scenes\creature_visuals\shin_getter.tscn'
$framesPath = 'scenes\creature_visuals\shin_getter_two_idle_frames.tres'
$idleDir = 'images\characters\shin_getter\forms\getter_two_idle'

Require-Pattern 'Root manifest version is v0.9.21+' 'ShinGetterMod.json' '"version":\s*"v0\.9\.(2[1-9]|[3-9][0-9])"'

Require-FileCount 'Getter Two idle exports only 24 png source frames' $idleDir '*.png' 24
Require-Pattern 'Getter Two idle keeps first imported frame' $framesPath 'getter_two_idle/sprite_000001\.png'
Require-Pattern 'Getter Two idle keeps twenty-fourth imported frame' $framesPath 'getter_two_idle/sprite_000024\.png'
Require-AbsentPattern 'Getter Two idle no longer references old sparse forty-eight frame tail' $framesPath 'sprite_000118|sprite_000124|sprite_000241'
Require-Pattern 'Getter Two baked SpriteFrames uses 24 source resources' $framesPath '\[gd_resource type="SpriteFrames" load_steps=25 format=3\]'
Require-Pattern 'Getter Two baked idle plays forward then reverses from frame 24' $framesPath 'ExtResource\("23_f23"\)[\s\S]*ExtResource\("24_f24"\)[\s\S]*ExtResource\("24_f24"\)[\s\S]*ExtResource\("23_f23"\)'
Require-Pattern 'Getter Two baked idle reverses back to first frame' $framesPath 'ExtResource\("2_f02"\)[\s\S]*ExtResource\("1_f01"\)[\s\S]*"loop": true[\s\S]*"name": &"idle"[\s\S]*"speed": 24\.0'

Require-Pattern 'Getter Two idle source frame count is 24' $sequencePath 'GetterTwoIdleMaxFrames\s*=\s*24'
Require-Pattern 'Getter Two runtime idle uses ping-pong loader' $sequencePath 'EnsureGetterTwoLoaded\(AnimatedSprite2D sprite\)[\s\S]*LoadPingPongAnimation\(frames,\s*IdleAnimationName,\s*GetterTwoIdleFrameDirectory,\s*GetterTwoIdleMaxFrames'
Require-AbsentPattern 'Getter Two runtime idle no longer uses linear 48-frame loader' $sequencePath 'LoadLinearAnimation\(frames,\s*IdleAnimationName,\s*GetterTwoIdleFrameDirectory,\s*GetterTwoIdleMaxFrames'
Require-Pattern 'Getter Two scene metadata reports 24 source frames' $scenePath 'GetterTwo[\s\S]*metadata/max_frames = 24'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.21 / Getter Two idle ping-pong checks failing:'
    $failures | Select-Object -First 120
    exit 1
}

Write-Host 'GREEN: v0.9.21 / Getter Two idle ping-pong checks passed.'
