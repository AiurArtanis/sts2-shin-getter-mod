$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'check-v0918-2026-07-04-card-frame-hsv-materials.ps1')
exit $LASTEXITCODE

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

function Require-ConstantAtLeast([string]$name, [string]$relativePath, [string]$constantName, [double]$minimum) {
    $text = Read-RepoFile $relativePath
    if ($null -eq $text -or $text -notmatch "$constantName\s*=\s*([0-9.]+)f") {
        $failures.Add($name)
        return
    }

    $value = [double]$Matches[1]
    if ($value -lt $minimum) {
        $failures.Add("$name (expected >= $minimum, got $value)")
    }
}

function Require-MaterialParameterAtLeast([string]$name, [string]$relativePath, [string]$parameterName, [double]$minimum) {
    $text = Read-RepoFile $relativePath
    if ($null -eq $text -or $text -notmatch "$parameterName\s*=\s*([0-9.]+)") {
        $failures.Add($name)
        return
    }

    $value = [double]$Matches[1]
    if ($value -lt $minimum) {
        $failures.Add("$name (expected >= $minimum, got $value)")
    }
}

$patchPath = 'src\Patches\ShinGetterCardFramePatch.cs'
$shaderPath = 'shaders\shin_getter_hsv.gdshader'
$materialPath = 'materials\cards\frames\card_frame_shin_getter_mat.tres'
$visualsPath = 'src\Nodes\Combat\NShinGetterStaticVisuals.cs'

Require-Pattern 'Root manifest version is v0.9.17' 'ShinGetterMod.json' '"version":\s*"v0\.9\.17"'

Require-ConstantAtLeast 'Common border tint strength is strong enough to remove leftover getter-ray/card-type hue' $patchPath 'FrameTintStrength' 0.94
Require-ConstantAtLeast 'Common text-panel tint strength is strong enough to remove mixed attack/skill base colors' $patchPath 'FrameBaseTintStrength' 0.92
Require-ConstantAtLeast 'Getter Two silver border tint strength stays near target silver' $patchPath 'FrameSilverTintStrength' 0.96
Require-ConstantAtLeast 'Getter Two text-panel tint strength stays near requested gray base' $patchPath 'FrameSilverBaseTintStrength' 0.94

Require-Pattern 'Getter 1 frame still uses requested red border' $patchPath 'GetterOneRedKey\s*=\s*"B00A0C"'
Require-Pattern 'Getter 1 frame still uses requested dark text-panel base' $patchPath 'GetterOneRedBaseKey\s*=\s*"491E2F"'
Require-Pattern 'Getter 2 frame still uses requested silver border' $patchPath 'GetterTwoSilverKey\s*=\s*"D9E4DE"'
Require-Pattern 'Getter 2 frame still uses requested gray text-panel base' $patchPath 'GetterTwoSilverBaseKey\s*=\s*"5A6463"'
Require-Pattern 'Getter 3 frame still uses requested yellow border' $patchPath 'GetterThreeYellowKey\s*=\s*"C4AD59"'
Require-Pattern 'Getter 3 frame still uses requested ocher text-panel base' $patchPath 'GetterThreeYellowBaseKey\s*=\s*"7F6A1F"'
Require-Pattern 'Shin Dragon/default frame still uses getter-ray border' $patchPath 'DefaultGetterRayKey\s*=\s*"1CC099"'
Require-Pattern 'Shin Dragon/default text-panel base still uses getter-ray base' $patchPath 'DefaultGetterRayBaseKey\s*=\s*"4F8373"'
Require-Pattern 'Frame tint can fall back to the active combat state for deck and preview clones' $patchPath 'CombatManager\.Instance[\s\S]*DebugOnlyGetState\(\)'
Require-Pattern 'Frame tint prefers the local combat player when card owner is unavailable' $patchPath 'LocalContext\.GetMe'
Require-Pattern 'Frame tint can use owner creature before falling back to local combat player' $patchPath 'model\.Owner\?\.Creature'

Require-MaterialParameterAtLeast 'Default exported frame material uses strong border tint' $materialPath 'shader_parameter/tint_strength' 0.94
Require-MaterialParameterAtLeast 'Default exported frame material uses strong text-panel tint' $materialPath 'shader_parameter/tint_base_strength' 0.92
Require-AbsentPattern 'Default frame material no longer keeps old weak text-panel tint' $materialPath 'shader_parameter/tint_base_strength = 0\.46'

Require-Pattern 'Shader derives a source shade from texture luminance' $shaderPath 'source_shade'
Require-Pattern 'Shader separates text panel from outer frame by UV mask' $shaderPath 'text_panel_mask[\s\S]*frame_mask'
Require-Pattern 'Shader applies requested text-panel color independently from requested border color' $shaderPath 'base_rgb[\s\S]*tint_base_color\.rgb[\s\S]*frame_rgb[\s\S]*tint_color\.rgb'
Require-Pattern 'Shader preserves texture detail through shade after selecting the target color' $shaderPath 'target_rgb\s*\*\s*source_shade'
Require-AbsentPattern 'Shader no longer leaves old weak base tint default' $shaderPath 'tint_base_strength[^\n]*=\s*0\.46'

Require-Pattern 'Form visual switching has a dedicated idle activation helper' $visualsPath 'ActivateIdleAnimation\(FormVisual'
Require-Pattern 'SwitchTo replays idle when the target form is already visible' $visualsPath 'if \(next\.Item\.Visible && next\.Item\.Modulate\.A > 0\.99f\)[\s\S]*ActivateIdleAnimation\(next\)[\s\S]*return;'
Require-Pattern 'SwitchTo activates idle after instant form swaps' $visualsPath '!animate[\s\S]*ActivateIdleAnimation\(next\)'
Require-Pattern 'SwitchTo activates idle before animated fade-in' $visualsPath 'next\.Item\.Visible = true;[\s\S]*ActivateIdleAnimation\(next\)'
Require-Pattern 'Getter Two idle activation uses the Getter Two frame loader' $visualsPath 'GetterTwo[\s\S]*NShinGetterSpriteSequence\.EnsureGetterTwoLoaded'
Require-Pattern 'Getter One idle activation keeps the Getter One loader' $visualsPath 'NShinGetterSpriteSequence\.EnsureLoaded'
Require-AbsentPattern 'Form visual switching still avoids per-frame animation polling' $visualsPath '_Process'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.17 / card frame solid base and Getter Two idle checks failing:'
    $failures | Select-Object -First 120
    exit 1
}

Write-Host 'GREEN: v0.9.17 / card frame solid base and Getter Two idle checks passed.'
