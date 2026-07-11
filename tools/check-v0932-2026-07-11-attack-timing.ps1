$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Failures = New-Object System.Collections.Generic.List[string]

function Read-RepoFile([string] $relativePath) {
    $path = Join-Path $Root $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        $Failures.Add("Missing file: $relativePath")
        return ""
    }

    return Get-Content -LiteralPath $path -Raw -Encoding UTF8
}

function Assert-Contains([string] $name, [string] $text, [string] $pattern) {
    if ($text -notmatch $pattern) {
        $Failures.Add($name)
    }
}

$cardBase = Read-RepoFile "src\Models\Cards\ShinGetterCardBase.cs"

Assert-Contains "Ordinary attack effect delay is fixed at half a second" $cardBase 'OrdinaryAttackEffectDelaySeconds\s*=\s*0\.5f'
Assert-Contains "Card base delays effects from BeforeCardPlayed" $cardBase 'override\s+async\s+Task\s+BeforeCardPlayed\(CardPlay\s+cardPlay\)'
Assert-Contains "Delay only applies to the card being played" $cardBase 'ReferenceEquals\(cardPlay\.Card,\s*this\)'
Assert-Contains "Delay only applies before the first play result" $cardBase 'cardPlay\.PlayIndex\s*!=\s*0'
Assert-Contains "Delay only applies to the ordinary Attack animation" $cardBase 'GetActionAnimationTrigger\(\)\s*!=\s*"Attack"'
Assert-Contains "Ordinary attack waits before OnPlay continues" $cardBase 'Cmd\.CustomScaledWait\(\s*OrdinaryAttackEffectDelaySeconds,\s*OrdinaryAttackEffectDelaySeconds\)'

foreach ($cardName in @(
    "SGC_Annihilation",
    "SGC_Avalanche",
    "SGC_ExpansionStrike",
    "SGC_GetterElbow",
    "SGC_GetterMissile",
    "SGC_GetterTomahawk",
    "SGC_HotBlood",
    "SGC_HurricaneStrike",
    "SGC_StarSlash"
)) {
    $escapedCardName = [regex]::Escape('"' + $cardName + '"')
    Assert-Contains "$cardName keeps its dedicated VFX timing" $cardBase ("AttackTimingHandledByVfxCards[\s\S]{0,1600}" + $escapedCardName)
}

Assert-Contains "Dedicated VFX cards bypass the ordinary attack delay" $cardBase 'AttackTimingHandledByVfxCards\.Contains\(GetType\(\)\.Name\)'

if ($Failures.Count -gt 0) {
    Write-Host "FAILED v0.9.32 2026-07-11 attack timing checks:" -ForegroundColor Red
    foreach ($failure in $Failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "PASSED v0.9.32 2026-07-11 attack timing checks." -ForegroundColor Green
