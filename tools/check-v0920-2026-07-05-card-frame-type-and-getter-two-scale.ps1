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
$scenePath = 'scenes\creature_visuals\shin_getter.tscn'

Require-Pattern 'Root manifest version is v0.9.21+' 'ShinGetterMod.json' '"version":\s*"v0\.9\.(2[1-9]|[3-9][0-9])"'

Require-Pattern 'Card frame patch defines a shared dynamic frame texture path' $patchPath 'DynamicFrameTexturePath\s*=\s*"res://images/atlases/ui_atlas\.sprites/card/card_frame_attack_s\.tres"'
Require-Pattern 'Card frame patch caches the shared dynamic frame texture' $patchPath 'SharedDynamicFrameTexture'
Require-Pattern 'Card frame patch uses shared dynamic frame texture for tint-eligible attack skill and power cards' $patchPath 'frame\.Texture\s*=\s*GetFrameTexture\(model\)'
Require-Pattern 'GetFrameTexture keeps Ancient and unsupported cards on their own frame' $patchPath 'GetFrameTexture\(CardModel model\)[\s\S]*if \(!IsDynamicTintEligible\(model\)\)[\s\S]*return model\.Frame'
Require-Pattern 'GetFrameTexture loads attack frame texture for dynamic cards' $patchPath 'ResourceLoader\.Load<Texture2D>\([\s\S]*DynamicFrameTexturePath'
Require-AbsentPattern 'EnsureFrameMaterial no longer assigns model.Frame directly' $patchPath 'frame\.Texture\s*=\s*model\.Frame;'
Require-Pattern 'Dynamic tint eligibility remains attack skill power only' $patchPath 'IsDynamicTintEligible\(CardModel model\)[\s\S]*CardType\.Attack[\s\S]*CardType\.Skill[\s\S]*CardType\.Power'

Require-Pattern 'Getter Two scale is raised to the same visual order as Getter One' $scenePath 'node name="GetterTwo"[\s\S]*scale = Vector2\(0\.6,\s*0\.6\)'
Require-Pattern 'Getter Two vertical position is recalibrated for the larger scale' $scenePath 'node name="GetterTwo"[\s\S]*position = Vector2\(0,\s*-176\)'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.21 / card frame type and Getter Two scale checks failing:'
    $failures | Select-Object -First 120
    exit 1
}

Write-Host 'GREEN: v0.9.21 / card frame type and Getter Two scale checks passed.'
