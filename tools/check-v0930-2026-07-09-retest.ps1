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

function Assert-NotContains([string] $name, [string] $text, [string] $pattern) {
    if ($text -match $pattern) {
        $Failures.Add($name)
    }
}

function Assert-JsonPropertyEquals(
    [string] $relativePath,
    [string] $propertyName,
    [string] $expectedValue
) {
    $json = Read-RepoFile $relativePath | ConvertFrom-Json
    $property = $json.PSObject.Properties[$propertyName]
    if ($null -eq $property -or $property.Value -ne $expectedValue) {
        $Failures.Add("$relativePath must define $propertyName as '$expectedValue'")
    }
}

$eventAlias = Read-RepoFile "src\Patches\EventConsoleAliasPatch.cs"
Assert-Contains "Event console alias intercepts Process with issuing player and result" $eventAlias 'bool\s+Prefix\(Player\?\s+issuingPlayer,\s*string\[\]\s+args,\s*ref\s+CmdResult\s+__result\)'
Assert-Contains "Event console alias resolves Getter Mandala directly from ModelDb" $eventAlias 'ModelDb\.Event<SGE_GetterMandala>\(\)'
Assert-Contains "Event console alias enters Getter Mandala directly" $eventAlias 'EnterRoom\(new\s+EventRoom\(eventModel\)\)'
Assert-Contains "Event console alias handles custom event instead of original AllEvents lookup" $eventAlias 'return\s+false\s*;'

Assert-JsonPropertyEquals "ShinGetterMod\localization\zhs\events.json" "S_G_E_GETTER_MANDALA.title" "盖塔曼陀罗"
Assert-JsonPropertyEquals "ShinGetterMod\localization\eng\events.json" "S_G_E_GETTER_MANDALA.title" "Getter Mandala"
Assert-JsonPropertyEquals "ShinGetterMod\localization\jpn\events.json" "S_G_E_GETTER_MANDALA.title" "ゲッターマンダラ"

$powerUi = Read-RepoFile "src\Patches\ShinGetterPowerUiPatch.cs"
Assert-Contains "Form icon transition caches at the power container removal boundary" $powerUi '\[HarmonyPatch\(typeof\(NPowerContainer\),\s*"Remove"\)\]'
Assert-Contains "Power container removal prefix caches the outgoing form icon" $powerUi 'NPowerContainer[\s\S]*Prefix\(PowerModel\s+power\)[\s\S]*CacheRemovedFormIcon\(power\)'
Assert-Contains "Form icon transition waits for the power node to be ready before consuming the cache" $powerUi 'if\s*\(!__instance\.IsNodeReady\(\)\)\s*return;[\s\S]*TryConsumeRemovedFormIcon'
Assert-Contains "Form icon transition cache uses weak creature keys" $powerUi 'ConditionalWeakTable<Creature,\s*RemovedFormIconCache>'
Assert-Contains "Form icon transition only caches during an active non-ending combat" $powerUi 'CombatManager\.Instance\.IsInProgress[\s\S]*!CombatManager\.Instance\.IsEnding'

$ki = Read-RepoFile "src\Models\Powers\SGP_Ki.cs"
Assert-Contains "Ki reduces damage in the shared damage hook used by intents" $ki 'override\s+decimal\s+ModifyDamageAdditive\('
Assert-Contains "Ki damage hook subtracts its amount for its owner" $ki 'target\s*==\s*Owner[\s\S]*-Amount'
Assert-NotContains "Ki no longer applies a second reduction after block resolution" $ki 'ModifyHpLostAfterOstyLate'

$scene = Read-RepoFile "scenes\creature_visuals\shin_getter.tscn"
Assert-Contains "Getter One receives the second ten percent scale increase" $scene 'name="GetterOne"[\s\S]*scale = Vector2\(0\.7623,\s*0\.7623\)'
Assert-Contains "Getter Three receives the second ten percent scale increase" $scene 'name="GetterThree"[\s\S]*scale = Vector2\(0\.726,\s*0\.726\)'
Assert-Contains "Getter Three moves upward to keep the chassis above the health bar" $scene 'name="GetterThree"[\s\S]*position = Vector2\(22,\s*-194\)'

$sequence = Read-RepoFile "src\Nodes\Combat\NShinGetterSpriteSequence.cs"
Assert-Contains "Getter attack animations use a dedicated faster frame rate" $sequence 'AttackFramesPerSecond\s*=\s*36d'
$attackSpeedUses = [regex]::Matches(
    $sequence,
    'LoadLinearAnimation\([^;]*AttackAnimationName[^;]*AttackFramesPerSecond',
    [System.Text.RegularExpressions.RegexOptions]::Singleline).Count
if ($attackSpeedUses -lt 3) {
    $Failures.Add("Getter One, Two, and Three attack animations must all use AttackFramesPerSecond")
}

if ($Failures.Count -gt 0) {
    Write-Host "FAILED v0.9.30 2026-07-09 retest checks:" -ForegroundColor Red
    foreach ($failure in $Failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "PASSED v0.9.30 2026-07-09 retest checks." -ForegroundColor Green
