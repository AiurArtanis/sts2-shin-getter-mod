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

Require-Pattern 'Getter One scene references baked SpriteFrames for combat-start visibility' 'scenes\creature_visuals\shin_getter.tscn' 'type="SpriteFrames" path="res://scenes/creature_visuals/shin_getter_one_idle_frames\.tres"'
Require-Pattern 'Getter One scene starts idle animation without waiting for form helper' 'scenes\creature_visuals\shin_getter.tscn' 'autoplay = "idle"'
Require-Pattern 'Getter One scene assigns idle SpriteFrames to AnimatedSprite2D' 'scenes\creature_visuals\shin_getter.tscn' 'sprite_frames = ExtResource\("6_idle_frames"\)'
Require-Pattern 'Getter One sequence is scaled for 720px idle frames' 'scenes\creature_visuals\shin_getter.tscn' 'scale = Vector2\(0\.6,\s*0\.6\)'
Require-Pattern 'Getter One baked frames include first idle frame' 'scenes\creature_visuals\shin_getter_one_idle_frames.tres' 'sprite_000001\.png'
Require-Pattern 'Getter One baked frames include last idle frame' 'scenes\creature_visuals\shin_getter_one_idle_frames.tres' 'sprite_000024\.png'
Require-AbsentPattern 'Getter One scene does not reference removed frame 25' 'scenes\creature_visuals\shin_getter.tscn' 'sprite_000025\.png'
Require-AbsentPattern 'Getter One baked frames do not reference removed frame 25' 'scenes\creature_visuals\shin_getter_one_idle_frames.tres' 'sprite_000025\.png'
Require-Pattern 'PCK validator checks Getter One idle SpriteFrames resource' 'tools\validate-mod-resources.gd' 'shin_getter_one_idle_frames\.tres'
Require-Pattern 'Root manifest version is at least v0.9.10' 'ShinGetterMod.json' '"version":\s*"v0\.9\.(1[0-9]|[2-9][0-9])"'

Require-Pattern 'Backup Plan energy reward uses icon' 'ShinGetterMod\localization\zhs\cards.json' 'S_G_C_BACKUP_PLAN\.description":\s*"[^"]*\{Energy:energyIcons\(\)\}'
Require-Pattern 'Bold Plan energy reward uses icon with X amount' 'ShinGetterMod\localization\zhs\cards.json' 'S_G_C_BOLD_PLAN\.description":\s*"[^"]*X\{IfUpgraded:show:\+1\}\{Energy:energyIcons\(\)\}'
Require-Pattern 'Overload future energy loss uses icon' 'ShinGetterMod\localization\zhs\cards.json' 'S_G_C_OVERLOAD\.description":\s*"[^"]*下回合失去\{SGP_Overload:energyIcons\(\)\}'
Require-Pattern 'Evolution Engine delayed energy uses icon' 'ShinGetterMod\localization\zhs\cards.json' 'S_G_C_EVOLUTION_ENGINE\.description":\s*"[^"]*\{SGP_EvolutionEngine:energyIcons\(\)\}'

Require-AbsentPattern 'Backup Plan no longer says point energy' 'ShinGetterMod\localization\zhs\cards.json' 'S_G_C_BACKUP_PLAN\.description":\s*"[^"]*点能量'
Require-AbsentPattern 'Bold Plan no longer says point energy' 'ShinGetterMod\localization\zhs\cards.json' 'S_G_C_BOLD_PLAN\.description":\s*"[^"]*点能量'
Require-AbsentPattern 'Overload no longer says point energy' 'ShinGetterMod\localization\zhs\cards.json' 'S_G_C_OVERLOAD\.description":\s*"[^"]*点能量'
Require-AbsentPattern 'Evolution Engine no longer says point energy' 'ShinGetterMod\localization\zhs\cards.json' 'S_G_C_EVOLUTION_ENGINE\.description":\s*"[^"]*点能量'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.10+ / 2026-07-04 feedback checks failing:'
    $failures | Select-Object -First 120
    exit 1
}

Write-Host 'GREEN: v0.9.10+ / 2026-07-04 feedback checks passed.'
