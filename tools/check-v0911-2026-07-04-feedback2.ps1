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

$patchPath = 'src\Patches\ShinGetterCardFramePatch.cs'
$materialPath = 'materials\cards\frames\card_frame_shin_getter_mat.tres'
$shaderPath = 'shaders\shin_getter_hsv.gdshader'

Require-Pattern 'Root manifest version is at least v0.9.11' 'ShinGetterMod.json' '"version":\s*"v0\.9\.(1[1-9]|[2-9][0-9])"'
Require-Pattern 'Getter 1 frame uses feedback red border' $patchPath 'GetterOneRedKey\s*=\s*"B00A0C"'
Require-Pattern 'Getter 1 frame uses feedback red base' $patchPath 'GetterOneRedBaseKey\s*=\s*"491E2F"'
Require-Pattern 'Getter 2 frame uses feedback silver border' $patchPath 'GetterTwoSilverKey\s*=\s*"D9E4DE"'
Require-Pattern 'Getter 2 frame uses feedback gray base' $patchPath 'GetterTwoSilverBaseKey\s*=\s*"5A6463"'
Require-Pattern 'Getter 3 frame uses feedback yellow border' $patchPath 'GetterThreeYellowKey\s*=\s*"C4AD59"'
Require-Pattern 'Getter 3 frame uses feedback yellow base' $patchPath 'GetterThreeYellowBaseKey\s*=\s*"7F6A1F"'
Require-Pattern 'Default getter ray frame uses feedback border' $patchPath 'DefaultGetterRayKey\s*=\s*"1CC099"'
Require-Pattern 'Default getter ray frame uses feedback base' $patchPath 'DefaultGetterRayBaseKey\s*=\s*"4F8373"'
Require-Pattern 'Default getter ray target keeps frame texture visible' $patchPath 'DefaultGetterRayTarget\(\)[\s\S]*FrameTintStrength,\s*FrameBaseTintStrength'
Require-Pattern 'Non-combat and export cards use default getter ray target' $patchPath 'if \(!IsCombatTintEligible\(model\)\)\s*return DefaultGetterRayTarget\(\);'
Require-Pattern 'Shin Form uses default getter ray target' $patchPath 'GetPower<SGP_ShinForm>\(\)[\s\S]*return DefaultGetterRayTarget\(\);'
Require-Pattern 'Default material uses feedback getter ray border color' $materialPath 'shader_parameter/tint_color = Color\(0\.109804,\s*0\.752941,\s*0\.6,\s*1\)'
Require-Pattern 'Default material uses feedback getter ray base color' $materialPath 'shader_parameter/tint_base_color = Color\(0\.309804,\s*0\.513725,\s*0\.45098,\s*1\)'
Require-Pattern 'Default material keeps non-solid tint strength' $materialPath 'shader_parameter/tint_strength = 0\.82'
Require-Pattern 'Default material keeps non-solid base strength' $materialPath 'shader_parameter/tint_base_strength = 0\.46'
Require-Pattern 'Shader default border tint matches feedback color' $shaderPath 'tint_color[\s\S]*vec4\(0\.110,\s*0\.753,\s*0\.600,\s*1\.0\)'
Require-Pattern 'Shader default base tint matches feedback color' $shaderPath 'tint_base_color[\s\S]*vec4\(0\.310,\s*0\.514,\s*0\.451,\s*1\.0\)'
Require-Pattern 'Shader separates the requested outer frame color from the text panel base color' $shaderPath 'text_panel_mask[\s\S]*frame_mask[\s\S]*tint_color'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.11+ / 2026-07-04 feedback2 checks failing:'
    $failures | Select-Object -First 120
    exit 1
}

Write-Host 'GREEN: v0.9.11+ / 2026-07-04 feedback2 checks passed.'
