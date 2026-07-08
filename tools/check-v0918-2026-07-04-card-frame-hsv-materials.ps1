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

$patchPath = 'src\Patches\ShinGetterCardFramePatch.cs'
$shaderPath = 'shaders\shin_getter_hsv.gdshader'
$materialPath = 'materials\cards\frames\card_frame_shin_getter_mat.tres'

Require-Pattern 'Root manifest version is v0.9.21+' 'ShinGetterMod.json' '"version":\s*"v0\.9\.(2[1-9]|[3-9][0-9])"'

Require-Pattern 'Frame shader keeps original HSV-style hue uniform' $shaderPath 'uniform float h: hint_range'
Require-Pattern 'Frame shader keeps original HSV-style saturation uniform' $shaderPath 'uniform float s: hint_range'
Require-Pattern 'Frame shader keeps original HSV-style value uniform' $shaderPath 'uniform float v'
Require-Pattern 'Frame shader exposes narrow border color overlay' $shaderPath 'uniform vec4 border_color'
Require-Pattern 'Frame shader applies narrow border overlay after HSV transform' $shaderPath 'edge_mask[\s\S]*mix\(col\.rgb,\s*border_color\.rgb,\s*edge_mask\s*\*\s*border_tint_strength\)'
Require-AbsentPattern 'Frame shader no longer uses fragile UV text panel mask' $shaderPath 'text_panel_mask|frame_mask|panel_top|inner_x|inner_y'
Require-AbsentPattern 'Frame shader no longer uses separate tint/base colors' $shaderPath 'tint_color|tint_base_color|(?<!border_)tint_strength|tint_base_strength|source_shade'

Require-Pattern 'Default material only stores getter-ray HSV parameters' $materialPath 'shader_parameter/h = 0\.455[\s\S]*shader_parameter/s = 1\.05[\s\S]*shader_parameter/v = 1\.16'
Require-Pattern 'Default material stores default getter-ray border color' $materialPath 'shader_parameter/border_color = Color\(0\.109804,\s*0\.752941,\s*0\.6,\s*1\)'
Require-AbsentPattern 'Default material no longer stores separate tint/base colors' $materialPath 'tint_color|tint_base_color|(?<!border_)tint_strength|tint_base_strength|tint_contrast'

Require-Pattern 'Card frame patch uses HSV target records' $patchPath 'FrameHsvTarget'
Require-Pattern 'Getter One uses Ironclad-like red HSV target' $patchPath 'GetterOneRedKey[\s\S]*0\.025f[\s\S]*0\.85f[\s\S]*1\.0f'
Require-Pattern 'Getter Two uses desaturated silver HSV target' $patchPath 'GetterTwoSilverKey[\s\S]*0\.0f[\s\S]*0\.08f[\s\S]*1\.22f'
Require-Pattern 'Getter Three uses yellow HSV target' $patchPath 'GetterThreeYellowKey[\s\S]*0\.14f[\s\S]*1\.35f[\s\S]*1\.12f'
Require-Pattern 'Default getter-ray target keeps cyan HSV target' $patchPath 'DefaultGetterRayKey[\s\S]*0\.455f[\s\S]*1\.05f[\s\S]*1\.16f'
Require-Pattern 'Frame patch animates h/s/v parameters during form transitions' $patchPath 'SetShaderParameter\("h"[\s\S]*SetShaderParameter\("s"[\s\S]*SetShaderParameter\("v"'
Require-Pattern 'Frame patch writes and animates border_color parameter' $patchPath 'Color borderColor = fromBorderColor\.Lerp\(toBorderColor,\s*t\)[\s\S]*SetShaderParameter\("border_color"'
Require-AbsentPattern 'Frame patch no longer writes separate tint/base shader parameters' $patchPath 'tint_color|tint_base_color|(?<!border_)tint_strength|tint_base_strength'
Require-Pattern 'Frame tint can still fall back to active combat state for deck and preview clones' $patchPath 'CombatManager\.Instance[\s\S]*DebugOnlyGetState\(\)[\s\S]*TryGetLocalCombatCreature'
Require-Pattern 'Card export default override is still preserved' $patchPath 'DefaultTintOverrideDepth\s*>\s*0[\s\S]*return DefaultGetterRayTarget\(\)'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.21 / HSV material card frame checks failing:'
    $failures | Select-Object -First 120
    exit 1
}

Write-Host 'GREEN: v0.9.21 / HSV material card frame checks passed.'
