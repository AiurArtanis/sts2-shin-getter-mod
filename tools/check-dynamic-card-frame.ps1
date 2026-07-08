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

Require-Pattern 'Frame shader exposes HSV hue uniform' $shaderPath 'uniform float h: hint_range'
Require-Pattern 'Frame shader exposes HSV saturation uniform' $shaderPath 'uniform float s: hint_range'
Require-Pattern 'Frame shader exposes HSV value uniform' $shaderPath 'uniform float v'
Require-Pattern 'Frame shader keeps original HSV recolor path' $shaderPath 'rgb_to_yiq[\s\S]*sin_hue[\s\S]*cos_hue[\s\S]*inverse\(rgb_to_yiq\)'
Require-Pattern 'Frame shader exposes narrow border color overlay' $shaderPath 'uniform vec4 border_color'
Require-Pattern 'Frame shader applies narrow border overlay after HSV transform' $shaderPath 'edge_mask[\s\S]*mix\(col\.rgb,\s*border_color\.rgb,\s*edge_mask\s*\*\s*border_tint_strength\)'
Require-AbsentPattern 'Frame shader no longer uses fragile UV text panel masks' $shaderPath 'text_panel_mask|frame_mask|panel_top|inner_x|inner_y'
Require-AbsentPattern 'Frame shader no longer stores separate tint/base colors' $shaderPath 'tint_color|tint_base_color|(?<!border_)tint_strength|tint_base_strength|source_shade'
Require-Pattern 'Default frame material uses getter-ray HSV hue' $materialPath 'shader_parameter/h = 0\.455'
Require-Pattern 'Default frame material uses getter-ray HSV saturation' $materialPath 'shader_parameter/s = 1\.05'
Require-Pattern 'Default frame material uses getter-ray HSV value' $materialPath 'shader_parameter/v = 1\.16'
Require-Pattern 'Default frame material stores default getter-ray border color' $materialPath 'shader_parameter/border_color = Color\(0\.109804,\s*0\.752941,\s*0\.6,\s*1\)'
Require-AbsentPattern 'Default frame material no longer stores separate tint/base colors' $materialPath 'tint_color|tint_base_color|(?<!border_)tint_strength|tint_base_strength|tint_contrast'

Require-Pattern 'Card frame patch also refreshes color on NCard.UpdateVisuals' $patchPath 'HarmonyPatch\(nameof\(NCard\.UpdateVisuals\)\)'
Require-Pattern 'Card frame patch duplicates ShaderMaterial per card' $patchPath 'shaderMaterial\.Duplicate\(\)'
Require-Pattern 'Card frame patch tracks per-card tint state without per-frame polling' $patchPath 'ConditionalWeakTable<NCard, FrameTintState>'
Require-Pattern 'Card frame patch uses a short tween for color transitions' $patchPath 'FrameTintTweenSeconds[\s\S]*CreateTween\(\)[\s\S]*TweenMethod'
Require-Pattern 'Card frame patch avoids animation when color did not change' $patchPath 'state\.ColorKey == target\.Key'
Require-Pattern 'Card frame patch supports export default getter-ray override' $patchPath 'DefaultTintOverrideDepth\s*>\s*0[\s\S]*return DefaultGetterRayTarget\(\);'
Require-Pattern 'Card frame patch lets combat deck previews use current form tint' $patchPath 'CombatManager\.Instance[\s\S]*DebugOnlyGetState\(\)[\s\S]*TryGetLocalCombatCreature'
Require-Pattern 'Card frame patch excludes Ancient cards from dynamic frame tint' $patchPath 'model\.Rarity == CardRarity\.Ancient'
Require-Pattern 'Card frame patch only tints attack skill and power cards' $patchPath 'CardType\.Attack[\s\S]*CardType\.Skill[\s\S]*CardType\.Power'
Require-Pattern 'Frame patch uses HSV target records' $patchPath 'FrameHsvTarget'
Require-Pattern 'Getter 1 frame tint uses Ironclad-like red HSV' $patchPath 'GetterOneRedKey[\s\S]*0\.025f[\s\S]*0\.85f[\s\S]*1\.0f'
Require-Pattern 'Getter 2 frame tint uses desaturated silver HSV' $patchPath 'GetterTwoSilverKey[\s\S]*0\.0f[\s\S]*0\.08f[\s\S]*1\.22f'
Require-Pattern 'Getter 3 frame tint uses yellow HSV' $patchPath 'GetterThreeYellowKey[\s\S]*0\.14f[\s\S]*1\.35f[\s\S]*1\.12f'
Require-Pattern 'Shin Form/default frame tint uses getter-ray HSV' $patchPath 'DefaultGetterRayKey[\s\S]*0\.455f[\s\S]*1\.05f[\s\S]*1\.16f'
Require-Pattern 'Shin Form applies getter-ray HSV tint' $patchPath 'GetPower<SGP_ShinForm>\(\)[\s\S]*return DefaultGetterRayTarget\(\);'
Require-Pattern 'Disabled target keeps invalid or Ancient cards on default HSV' $patchPath 'DisabledTarget\(\)[\s\S]*DefaultDisabledKey,\s*new Color\(DefaultGetterRayKey\),\s*0\.455f,\s*1\.05f,\s*1\.16f'
Require-Pattern 'Frame patch animates HSV parameters' $patchPath 'SetShaderParameter\("h"[\s\S]*SetShaderParameter\("s"[\s\S]*SetShaderParameter\("v"'
Require-Pattern 'Frame patch animates border color parameters' $patchPath 'Color borderColor = fromBorderColor\.Lerp\(toBorderColor,\s*t\)[\s\S]*SetShaderParameter\("border_color"'
Require-AbsentPattern 'Card frame patch does not use _Process polling' $patchPath '_Process'

if ($failures.Count -gt 0) {
    Write-Host 'RED: dynamic card frame checks failing:'
    $failures | Select-Object -First 80
    exit 1
}

Write-Host 'GREEN: dynamic card frame checks passed.'
