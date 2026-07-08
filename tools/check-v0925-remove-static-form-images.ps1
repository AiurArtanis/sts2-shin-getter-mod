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

function Require-RequiredResourcesAbsentPattern {
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
    $match = [regex]::Match($content, 'const REQUIRED_RESOURCES := \{(?<body>[\s\S]*?)\}\s*const EXISTS_ONLY_RESOURCES')
    if (-not $match.Success) {
        $failures.Add("$Description (unable to isolate REQUIRED_RESOURCES)")
        return
    }

    if ($match.Groups['body'].Value -match $Pattern) {
        $failures.Add($Description)
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
        $failures.Add("$Description (missing directory: $RelativePath)")
        return
    }

    $count = @(Get-ChildItem -LiteralPath $path -File -Filter $Filter).Count
    if ($count -gt 0) {
        $failures.Add("$Description (found $count)")
    }
}

Require-Pattern 'Manifest version is v0.9.25 for static form image cleanup' 'ShinGetterMod.json' '"version"\s*:\s*"v0\.9\.25"'

$formsDir = 'images\characters\shin_getter\forms'
$scenePath = 'scenes\creature_visuals\shin_getter.tscn'
$validationScript = 'tools\validate-mod-resources.gd'

Require-NoFiles 'Form image directory no longer keeps static PNG assets' $formsDir '*_static.png'
Require-NoFiles 'Form image directory no longer keeps static PNG import metadata' $formsDir '*_static.png.import'

Require-AbsentPattern 'Combat creature scene no longer references static form textures' $scenePath '_static\.png'
Require-Pattern 'Getter One scene still uses baked idle SpriteFrames' $scenePath 'GetterOne[\s\S]*sprite_frames = ExtResource\("6_idle_frames"\)'
Require-Pattern 'Getter Two scene still uses baked idle SpriteFrames' $scenePath 'GetterTwo[\s\S]*sprite_frames = ExtResource\("7_two_idle_frames"\)'
Require-Pattern 'Getter Three scene still uses baked idle SpriteFrames' $scenePath 'GetterThree[\s\S]*sprite_frames = ExtResource\("5_three_idle_frames"\)'
Require-Pattern 'Shin Dragon scene still uses baked idle SpriteFrames' $scenePath 'ShinDragon[\s\S]*sprite_frames = ExtResource\("8_dragon_idle_frames"\)'

Require-RequiredResourcesAbsentPattern 'PCK validator no longer requires static form textures' $validationScript '_static\.png'
Require-Pattern 'PCK validator forbids Getter One static image' $validationScript 'shin_getter_one_static\.png'
Require-Pattern 'PCK validator forbids Getter Two static image' $validationScript 'shin_getter_two_static\.png'
Require-Pattern 'PCK validator forbids Getter Three static image' $validationScript 'shin_getter_three_static\.png'
Require-Pattern 'PCK validator forbids Shin Dragon static image' $validationScript 'shin_getter_dragon_static\.png'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.25 / static form image cleanup checks failing:'
    $failures | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host 'GREEN: v0.9.25 / static form image cleanup checks passed.'
