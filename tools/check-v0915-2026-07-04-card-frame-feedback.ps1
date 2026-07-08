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
$exporterPath = 'src\Diagnostics\CardExport\ShinGetterCardPngExporter.cs'

Require-Pattern 'Root manifest version is v0.9.15+' 'ShinGetterMod.json' '"version":\s*"v0\.9\.(1[5-9]|[2-9]\d)"'
Require-Pattern 'Card frame patch has export default tint override depth' $patchPath 'DefaultTintOverrideDepth'
Require-Pattern 'Card frame patch exposes BeginDefaultTintOverride' $patchPath 'BeginDefaultTintOverride'
Require-Pattern 'Card frame patch exposes EndDefaultTintOverride' $patchPath 'EndDefaultTintOverride'
Require-Pattern 'Card frame target honors export default override before form powers' $patchPath 'DefaultTintOverrideDepth\s*>\s*0[\s\S]*return DefaultGetterRayTarget\(\)'
Require-AbsentPattern 'Card frame target no longer gates combat form tint by combat pile eligibility' $patchPath 'if \(!IsCombatTintEligible\(model\)\)\s*return DefaultGetterRayTarget\(\);'
Require-AbsentPattern 'Obsolete combat pile eligibility helper removed' $patchPath 'IsCombatTintEligible'
Require-Pattern 'Getter One form tint can apply to deck and preview cards during combat' $patchPath 'GetPower<SGP_ShinGetterOne>\(\)[\s\S]*GetterOneRedKey'
Require-Pattern 'Getter Two form tint can apply to deck and preview cards during combat' $patchPath 'GetPower<SGP_ShinGetterTwo>\(\)[\s\S]*GetterTwoSilverKey'
Require-Pattern 'Getter Three form tint can apply to deck and preview cards during combat' $patchPath 'GetPower<SGP_ShinGetterThree>\(\)[\s\S]*GetterThreeYellowKey'
Require-Pattern 'Card PNG exporter forces default frame tint for offscreen export' $exporterPath 'BeginDefaultTintOverride\(\)[\s\S]*UpdateVisuals\(PileType\.None'
Require-Pattern 'Card PNG exporter always ends default frame tint override' $exporterPath 'finally[\s\S]*EndDefaultTintOverride\(\)'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.15 / 2026-07-04 card frame feedback checks failing:'
    $failures | Select-Object -First 120
    exit 1
}

Write-Host 'GREEN: v0.9.15 / 2026-07-04 card frame feedback checks passed.'
