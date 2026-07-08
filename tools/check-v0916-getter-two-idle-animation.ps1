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

Require-Pattern 'Root manifest version is v0.9.16+' 'ShinGetterMod.json' '"version":\s*"v0\.9\.(1[6-9]|[2-9]\d)"'

Require-FileCount 'Getter Two idle animation exports 24 png source frames' 'images\characters\shin_getter\forms\getter_two_idle' '*.png' 24
Require-FileCount 'Getter Two attack animation still exports 40 png frames' 'images\characters\shin_getter\forms\getter_two_attack' '*.png' 40

Require-Pattern 'GetterTwo scene metadata points at idle frames' 'scenes\creature_visuals\shin_getter.tscn' 'GetterTwo[\s\S]*metadata/frame_directory = "res://images/characters/shin_getter/forms/getter_two_idle"'
Require-Pattern 'GetterTwo scene metadata keeps idle source frame cap' 'scenes\creature_visuals\shin_getter.tscn' 'GetterTwo[\s\S]*metadata/max_frames = 24'

$sequencePath = 'src\Nodes\Combat\NShinGetterSpriteSequence.cs'
Require-Pattern 'Sprite sequence declares Getter Two idle frame directory' $sequencePath 'GetterTwoIdleFrameDirectory\s*=.*getter_two_idle'
Require-Pattern 'Sprite sequence declares Getter Two idle source frame cap' $sequencePath 'GetterTwoIdleMaxFrames\s*=\s*24'
Require-Pattern 'Sprite sequence loads Getter Two looping ping-pong idle animation from frames' $sequencePath 'EnsureGetterTwoLoaded[\s\S]*LoadPingPongAnimation\(frames,\s*IdleAnimationName,\s*GetterTwoIdleFrameDirectory,\s*GetterTwoIdleMaxFrames,\s*IdleFramesPerSecond,\s*loop:\s*true\)'
Require-Pattern 'Sprite sequence still loads Getter Two one-shot attack animation' $sequencePath 'EnsureGetterTwoLoaded[\s\S]*LoadLinearAnimation\(frames,\s*AttackAnimationName,\s*GetterTwoAttackFrameDirectory,\s*GetterTwoAttackMaxFrames,\s*ActionFramesPerSecond,\s*loop:\s*false\)'
Require-AbsentPattern 'Getter Two loader no longer uses static texture as idle' $sequencePath 'EnsureGetterTwoLoaded[\s\S]*AddStaticTextureAnimation'
Require-AbsentPattern 'Getter Two combat scene no longer keeps unused static texture dependency' 'scenes\creature_visuals\shin_getter.tscn' 'shin_getter_two_static\.png'

Require-Pattern 'PCK validator checks Getter Two first idle frame' 'tools\validate-mod-resources.gd' 'getter_two_idle/sprite_000001\.png'
Require-Pattern 'PCK validator checks Getter Two last source idle frame' 'tools\validate-mod-resources.gd' 'getter_two_idle/sprite_000024\.png'
Require-Pattern 'PCK validator still checks Getter Two attack frames' 'tools\validate-mod-resources.gd' 'getter_two_attack/sprite_000001\.png[\s\S]*getter_two_attack/sprite_000121\.png'
Require-AbsentPattern 'PCK validator no longer requires unused Getter Two static texture' 'tools\validate-mod-resources.gd' 'shin_getter_two_static\.png'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.16 / Getter Two idle animation checks failing:'
    $failures | Select-Object -First 120
    exit 1
}

Write-Host 'GREEN: v0.9.16 / Getter Two idle animation checks passed.'
