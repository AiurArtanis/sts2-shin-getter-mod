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

$basePath = 'src\Models\Cards\ShinGetterCardBase.cs'
$framePatchPath = 'src\Patches\ShinGetterCardFramePatch.cs'

Require-Pattern 'Shin Getter cards override the vanilla gold glow hook' $basePath 'protected override bool ShouldGlowGoldInternal'
Require-Pattern 'Form glow only applies to hand cards' $basePath 'PileType\.Hand'
Require-Pattern 'Form glow only applies during combat' $basePath 'CombatState\s*==\s*null'
Require-Pattern 'Form glow derives forms from registered card terms' $basePath 'GetGlowFormsForCard'
Require-Pattern 'Getter 1 term maps to Getter1 glow' $basePath '一号机[\s\S]*ShinGetterForm\.Getter1'
Require-Pattern 'Getter 2 term maps to Getter2 glow' $basePath '二号机[\s\S]*ShinGetterForm\.Getter2'
Require-Pattern 'Getter 3 term maps to Getter3 glow' $basePath '三号机[\s\S]*ShinGetterForm\.Getter3'
Require-Pattern 'Current form check reuses Shin Form aware HasForm logic' $basePath 'HasForm\(Owner, form\)'
Require-Pattern 'Dive Strike is tagged as Getter 1 form reward' $basePath 'SGC_DiveStrike"\]\s*=\s*new\[\]\s*\{[^}]*"一号机"'
Require-Pattern 'Visible hand holders refresh when form changes' $framePatchPath 'NHandCardHolder[\s\S]*UpdateCard\(\)'

if ($failures.Count -gt 0) {
    Write-Host 'RED: form card glow checks failing:'
    $failures | Select-Object -First 80
    exit 1
}

Write-Host 'GREEN: form card glow checks passed.'
