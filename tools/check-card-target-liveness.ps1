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

$cardPoolText = Read-RepoFile 'src\Models\CardPools\ShinGetterCardPool.cs'
if ($null -eq $cardPoolText) {
    throw 'Could not read ShinGetterCardPool.cs'
}

$activeCardTypes = [regex]::Matches($cardPoolText, 'ModelDb\.Card<(?<type>SGC_[A-Za-z0-9_]+)>\(\)') |
    ForEach-Object { $_.Groups['type'].Value } |
    Sort-Object -Unique

foreach ($cardType in $activeCardTypes) {
    $relativePath = "src\Models\Cards\$cardType.cs"
    $text = Read-RepoFile $relativePath
    if ($null -eq $text) {
        $failures.Add("Active card source exists: $relativePath")
        continue
    }

    $hasPostDamageTargetPower =
        $text -match 'DamageCmd\.Attack[\s\S]*?\.Execute\(choiceContext\);\s*await PowerCmd\.Apply<[^>]+>\(choiceContext,\s*cardPlay\.Target'

    if ($hasPostDamageTargetPower -and $text -notmatch 'if\s*\(\s*cardPlay\.Target\.IsAlive\s*\)\s*\{[\s\S]*?await PowerCmd\.Apply<[^>]+>\(choiceContext,\s*cardPlay\.Target') {
        $failures.Add("Post-damage target power is guarded by IsAlive: $cardType")
    }
}

Require-Pattern `
    'Focus Fire skips the bonus hit when the first hit killed the target' `
    'src\Models\Cards\SGC_FocusFire.cs' `
    'if\s*\(\s*cardPlay\.Target\.IsAlive\s*&&\s*cardPlay\.Target\.Powers\.Any'

Require-Pattern `
    'Final Getter Beam skips strength loss when the beam killed the target' `
    'src\Models\Cards\SGC_FinalGetterBeam.cs' `
    'if\s*\(\s*cardPlay\.Target\.IsAlive\s*\)\s*\{[\s\S]*?await PowerCmd\.Apply<ManglePower>\(choiceContext,\s*cardPlay\.Target'

Require-Pattern `
    'Getter Elbow skips Weak when the hit killed the target' `
    'src\Models\Cards\SGC_GetterElbow.cs' `
    'if\s*\(\s*cardPlay\.Target\.IsAlive\s*\)\s*\{[\s\S]*?await PowerCmd\.Apply<WeakPower>\(choiceContext,\s*cardPlay\.Target'

Require-Pattern `
    'Spiral Drill stops manual unblockable hits after the target dies' `
    'src\Models\Cards\SGC_SpiralDrill.cs' `
    'for\s*\(\s*int i = 0;\s*i < 4 && cardPlay\.Target\.IsAlive;\s*i\+\+\s*\)'

Require-Pattern `
    'Getter Chop skips its second explicit strike if the first strike killed the target' `
    'src\Models\Cards\SGC_GetterChop.cs' `
    'if\s*\(\s*cardPlay\.Target\.IsAlive\s*\)\s*\{[\s\S]*?DamageCmd\.Attack\(base\.DynamicVars\.Damage\.BaseValue\)'

Require-Pattern `
    'Getter Missile recomputes living missile targets before every missile' `
    'src\Models\Cards\SGC_GetterMissile.cs' `
    'for\s*\(\s*int i = 0;\s*i < 4;\s*i\+\+\s*\)[\s\S]*?HasLivingEnemyTargets\(\)[\s\S]*?GetLivingMissileTargets\(\)'

if ($failures.Count -gt 0) {
    Write-Host 'RED: card target liveness checks failing:'
    $failures | Select-Object -First 80
    exit 1
}

Write-Host 'GREEN: card target liveness checks passed.'
