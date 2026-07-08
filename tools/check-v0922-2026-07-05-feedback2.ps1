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

$cardsJson = 'ShinGetterMod\localization\zhs\cards.json'
$overloadCard = 'src\Models\Cards\SGC_Overload.cs'
$engineCard = 'src\Models\Cards\SGC_EvolutionEngine.cs'
$getterRushCard = 'src\Models\Cards\SGC_GetterRush.cs'
$hurricaneCard = 'src\Models\Cards\SGC_HurricaneStrike.cs'
$getterBeamCard = 'src\Models\Cards\SGC_GetterBeam.cs'
$cardBase = 'src\Models\Cards\ShinGetterCardBase.cs'
$consoleCmd = 'src\Diagnostics\ShinGetterAddAllCardsConsoleCmd.cs'
$consolePatch = 'src\Patches\ShinGetterConsoleCommandPatch.cs'
$validationScript = 'tools\validate-mod-resources.gd'

Require-Pattern 'Root manifest version is v0.9.22 or later' 'ShinGetterMod.json' '"version":\s*"v0\.9\.(?:2[2-9]|[3-9][0-9])"'

Require-Pattern 'Overload defines an EnergyVar for delayed energy loss icon rendering' $overloadCard 'new\s+EnergyVar\("DelayedEnergyLoss",\s*1\)'
Require-Pattern 'Overload still applies SGP_Overload for next-turn energy loss' $overloadCard 'PowerCmd\.Apply<SGP_Overload>'
Require-Pattern 'Evolution Engine defines an EnergyVar for pending energy icon rendering' $engineCard 'new\s+EnergyVar\("EvolutionEngineEnergy",\s*1\)'
Require-Pattern 'Evolution Engine upgrades the EnergyVar together with SGP_EvolutionEngine' $engineCard 'DynamicVars\["EvolutionEngineEnergy"\]\.UpgradeValueBy\(1m\)'
Require-AbsentPattern 'Card localization does not call energyIcons on PowerVar values' $cardsJson '\{SGP_(Overload|EvolutionEngine):energyIcons\(\)\}'
Require-Pattern 'Overload description uses delayed EnergyVar icon placeholder' $cardsJson '"S_G_C_OVERLOAD\.description":\s*"[^"]*\{DelayedEnergyLoss:energyIcons\(\)\}'
Require-Pattern 'Evolution Engine description uses pending EnergyVar icon placeholder' $cardsJson '"S_G_C_EVOLUTION_ENGINE\.description":\s*"[^"]*\{EvolutionEngineEnergy:energyIcons\(\)\}'
Require-Pattern 'Dive Strike uses CalculatedDamage because the card has no DamageVar' $cardsJson '"S_G_C_DIVE_STRIKE\.description":\s*"造成\{CalculatedDamage:diff\(\)\}'
Require-AbsentPattern 'Chinese card localization has no bare Damage:diff placeholder caused by missing braces' $cardsJson '(?<![A-Za-z0-9_\{])Damage:diff\(\)'

Require-Pattern 'Getter Rush disables default attacker animation so PlayRush owns the movement' $getterRushCard 'WithNoAttackerAnim\(\)[\s\S]*BeforeDamage\(\(\)\s*=>\s*ShinGetterCombatVfx\.PlayRush'
Require-Pattern 'Hurricane Strike keeps the flurry/spray VFX assigned to it' $hurricaneCard 'BeforeDamage\(\(\)\s*=>\s*ShinGetterCombatVfx\.PlayDaggerSpray'

Require-Pattern 'Evolution big power icon is validated from images/powers' $validationScript 'res://images/powers/s_g_p_evolution\.png'
Require-Pattern 'Evolution packed power icon is validated from atlas sprites' $validationScript 'res://images/atlases/power_atlas\.sprites/s_g_p_evolution\.tres'

Require-Pattern 'Non-Shin Getter Transform exits before applying form powers' $cardBase 'if\s*\(!IsShinGetterPlayer\(player\)\)\s*\{\s*return;\s*\}'
Require-Pattern 'Transform compatibility helper checks ShinGetter character type' $cardBase 'player\.Character\s+is\s+ShinGetter'

Require-Pattern 'Getter Beam upgraded Wane increases from 1 to 3' $getterBeamCard 'DynamicVars\["Wane"\]\.UpgradeValueBy\(2m\)'

Require-Pattern 'Add-all-cards console command exists' $consoleCmd 'public\s+sealed\s+class\s+ShinGetterAddAllCardsConsoleCmd\s*:\s*AbstractConsoleCmd'
Require-Pattern 'Add-all-cards command name is shin_getter_add_cards' $consoleCmd 'CmdName\s*=>\s*"shin_getter_add_cards"'
Require-Pattern 'Add-all-cards command parses quoted character filter' $consoleCmd 'TryParseQuotedCommandArgs'
Require-Pattern 'Add-all-cards command uses ShinGetterCardPool AllCards' $consoleCmd 'ModelDb\.CardPool<ShinGetterCardPool>\(\)[\s\S]*\.AllCards'
Require-Pattern 'Add-all-cards command creates run-state deck cards' $consoleCmd 'issuingPlayer\.RunState\.CreateCard\(canonicalCard,\s*issuingPlayer\)'
Require-Pattern 'Add-all-cards command can upgrade generated cards' $consoleCmd 'CardCmd\.Upgrade\(card,\s*CardPreviewStyle\.None\)'
Require-Pattern 'Console patch intercepts add-all-cards command' $consolePatch 'AddAllCardsCommandName\s*=\s*"shin_getter_add_cards"'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.22+ / 2026-07-05 feedback2 checks failing:'
    $failures | Select-Object -First 160
    exit 1
}

Write-Host 'GREEN: v0.9.22+ / 2026-07-05 feedback2 checks passed.'
