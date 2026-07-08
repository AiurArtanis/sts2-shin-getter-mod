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

$shaderPath = 'shaders\shin_getter_hsv.gdshader'

Require-Pattern 'Root manifest version is at least v0.9.12' 'ShinGetterMod.json' '"version":\s*"v0\.9\.(1[2-9]|[2-9][0-9])"'
Require-Pattern 'Frame shader computes inner text-panel x bounds from UV' $shaderPath 'inner_x\s*=[\s\S]*UV\.x'
Require-Pattern 'Frame shader computes text-panel y bounds from UV' $shaderPath 'inner_y\s*=[\s\S]*UV\.y'
Require-Pattern 'Frame shader models the sloped text-panel top edge' $shaderPath 'panel_top\s*=\s*mix\(0\.545,\s*0\.425'
Require-Pattern 'Frame shader keeps requested base color inside text panel' $shaderPath 'base_rgb[\s\S]*tint_base_color\.rgb'
Require-Pattern 'Frame shader applies requested border color outside text panel' $shaderPath 'frame_mask\s*=\s*1\.0 - text_panel_mask[\s\S]*frame_rgb[\s\S]*tint_color\.rgb'
Require-Pattern 'Frame shader blends frame and base by the UV mask' $shaderPath 'target_rgb\s*=\s*mix\(base_rgb,\s*frame_rgb,\s*frame_mask\)'
Require-AbsentPattern 'Frame shader does not use brightness as the outer-frame selector' $shaderPath 'border_mask\s*=\s*smoothstep\(0\.42,\s*0\.82,\s*luma\)'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.12+ / frame border mask checks failing:'
    $failures | Select-Object -First 120
    exit 1
}

Write-Host 'GREEN: v0.9.12+ / frame border mask checks passed.'
