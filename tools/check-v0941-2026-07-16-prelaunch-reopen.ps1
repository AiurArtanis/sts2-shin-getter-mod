$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Failures = New-Object System.Collections.Generic.List[string]

function Read-RepoFile([string] $relativePath) {
    $path = Join-Path $Root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $Failures.Add("Missing file: $relativePath")
        return ""
    }
    return Get-Content -LiteralPath $path -Raw -Encoding UTF8
}

function Read-Json([string] $relativePath) {
    try { return (Read-RepoFile $relativePath) | ConvertFrom-Json -AsHashtable }
    catch {
        $Failures.Add("Invalid JSON: $relativePath ($($_.Exception.Message))")
        return $null
    }
}

function Assert-Contains([string] $name, [string] $text, [string] $pattern) {
    if ($text -notmatch $pattern) { $Failures.Add($name) }
}

function Assert-NotContains([string] $name, [string] $text, [string] $pattern) {
    if ($text -match $pattern) { $Failures.Add($name) }
}

function Assert-JsonContains([string] $name, [hashtable] $json, [string] $key, [string] $pattern) {
    if ($null -eq $json -or -not $json.ContainsKey($key) -or [string]$json[$key] -notmatch $pattern) {
        $Failures.Add($name)
    }
}

function Assert-JsonNotContains([string] $name, [hashtable] $json, [string] $key, [string] $pattern) {
    if ($null -eq $json -or -not $json.ContainsKey($key) -or [string]$json[$key] -match $pattern) {
        $Failures.Add($name)
    }
}

$backgroundScene = Read-RepoFile 'scenes\screens\char_select\char_select_bg_shin_getter.tscn'
$backgroundLayout = Read-RepoFile 'src\Nodes\Screens\NShinGetterCharacterSelectBackground.cs'
Assert-Contains 'Character-select background uses the viewport-aware layout node' $backgroundScene 'NShinGetterCharacterSelectBackground\.cs'
Assert-Contains 'Character-select portrait covers the resolved viewport' $backgroundScene 'Portrait[\s\S]{0,360}stretch_mode\s*=\s*6'
Assert-Contains 'Character-select layout reacts to viewport size changes' $backgroundLayout 'Viewport\.SignalName\.SizeChanged'
Assert-Contains 'Character-select layout cancels the native parent transform' $backgroundLayout 'GetGlobalTransformWithCanvas\(\)\.AffineInverse\(\)'
Assert-Contains 'Character-select layout resolves the visible viewport rectangle' $backgroundLayout 'GetVisibleRect\(\)'

$ancientReplay = Read-RepoFile 'src\Patches\ShinGetterAncientDialogueReplayPatch.cs'
$ancientLegacy = Read-RepoFile 'src\Patches\ShinGetterAncientDialoguePatch.cs'
Assert-Contains 'Ancient dialogue replay is an independent GetValidDialogues prefix' $ancientReplay 'HarmonyPatch\(typeof\(AncientDialogueSet\),\s*nameof\(AncientDialogueSet\.GetValidDialogues\)\)'
Assert-Contains 'Every Shin Getter ancient encounter starts from visit zero' $ancientReplay 'characterId\.Entry\s*==[\s\S]{0,120}charVisits\s*=\s*0'
Assert-NotContains 'Obsolete visit-index fallback was removed' $ancientLegacy 'exactVisit|dialogues\[charVisits\s*%'

$creatureScene = Read-RepoFile 'scenes\creature_visuals\shin_getter.tscn'
$beam = Read-RepoFile 'src\Nodes\Vfx\ShinGetterBeamVfx.cs'
$combatVfx = Read-RepoFile 'src\Nodes\Vfx\ShinGetterCombatVfx.cs'
$extraVfx = Read-RepoFile 'src\Nodes\Vfx\ShinGetterCombatVfx.Extra.cs'
Assert-Contains 'Shin Getter Dragon moves another five percent upward' $creatureScene 'ShinDragon[\s\S]{0,420}position\s*=\s*Vector2\(0,\s*-229\.8404\)'
Assert-Contains 'Final Getter Beam uses exact 44fcc5 outer color' $beam 'GetterRay\s*=\s*new\(0\.266667f,\s*0\.988235f,\s*0\.772549f,\s*1f\)'
Assert-Contains 'Final Getter Beam outer layers share the Getter Ray color' $beam 'TintCanvasItems\(beam,\s*GetterRay,\s*GetterRay\)'
Assert-Contains 'Final Getter Beam pink center doubles vertically again' $beam 'Scale\s*=\s*new Vector2\(1f,\s*1\.40f\)'
Assert-Contains 'Star Slash shares the adjusted Getter Ray color' $combatVfx 'GetterRay\s*=\s*new\(0\.266667f,\s*0\.988235f,\s*0\.772549f,\s*1f\)'
Assert-Contains 'Dive Strike rise lasts half of the attack animation' $extraVfx 'global_position",\s*apex,\s*0\.56f'

$cardBase = Read-RepoFile 'src\Models\Cards\ShinGetterCardBase.cs'
$diveStrike = Read-RepoFile 'src\Models\Cards\SGC_DiveStrike.cs'
$getterFlash = Read-RepoFile 'src\Models\Cards\SGC_GetterFlash.cs'
Assert-NotContains 'Dive Strike no longer uses the Dash action animation' $cardBase 'DashAnimationCards[\s\S]{0,300}"SGC_DiveStrike"'
Assert-Contains 'Dive Strike keeps its custom movement while suppressing the engine attacker animation' $diveStrike 'WithNoAttackerAnim\(\)[\s\S]{0,220}PlayMovementVfx'
Assert-Contains 'Getter Flash rushes before its attack animation' $getterFlash 'PlayFlashRush[\s\S]{0,220}QueueNextActionSpeed[\s\S]{0,180}"Attack"'
Assert-Contains 'Getter Flash attack animation runs at 1.75 speed' $getterFlash 'QueueNextActionSpeed\(Owner\.Creature,\s*1\.75f\)'

$poseidon = Read-RepoFile 'src\Models\Cards\SGC_PoseidonThunder.cs'
Assert-Contains 'Poseidon Thunder defines Frail' $poseidon 'PowerVar<FrailPower>\(1m\)'
Assert-Contains 'Poseidon Thunder applies Frail' $poseidon 'PowerCmd\.Apply<FrailPower>'
Assert-Contains 'Poseidon Thunder upgrades Frail from one to three' $poseidon '"FrailPower"\]\.UpgradeValueBy\(2m\)'
Assert-Contains 'Poseidon Thunder preserves its thunder-field VFX' $poseidon 'PlayThunderField'
Assert-Contains 'Poseidon Thunder cast is not overwritten by the default attack animation' $poseidon 'WithNoAttackerAnim\(\)'

$execution = Read-RepoFile 'src\Audio\ShinGetterExecutionMusicService.cs'
$battleInstinct = Read-RepoFile 'src\Models\Relics\SGR_BattleInstinct.cs'
Assert-Contains 'Execution theme fades out for three seconds after combat' $execution 'CombatEndFadeOutDurationSeconds\s*=\s*3f[\s\S]{0,1800}TweenProperty\(player,\s*"volume_db",\s*SilentVolumeDb,\s*CombatEndFadeOutDurationSeconds\)'
Assert-Contains 'Normal BGM remains muted during the execution-theme fade' $execution 'SetBgmVol\(0f\)[\s\S]{0,700}TweenProperty\(player'
Assert-Contains 'Battle Instinct restores its state at combat end' $battleInstinct 'AfterCombatEnd\(CombatRoom\s+_\)[\s\S]{0,120}TriggeredThisCombat\s*=\s*false'

$pool = Read-RepoFile 'src\Models\CardPools\ShinGetterCardPool.cs'
Assert-Contains 'Unlocked Shin Getter pool excludes Insect Virus' $pool 'FilterThroughEpochs[\s\S]{0,260}card\s+is\s+not\s+SGC_InsectVirus'
Assert-Contains 'Insect Virus remains registered in AllCards for its event' $pool 'ModelDb\.Card<SGC_InsectVirus>\(\)'

$starSlash = Read-RepoFile 'src\Models\Cards\SGC_StarSlash.cs'
$getterWill = Read-RepoFile 'src\Models\Cards\SGC_GetterWill.cs'
$desperation = Read-RepoFile 'src\Models\Cards\SGC_Desperation.cs'
$rayPower = Read-RepoFile 'src\Models\Powers\SGP_GetterRayOverflow.cs'
Assert-Contains 'Star Slash base damage is 22' $starSlash 'DamageVar\(22m'
Assert-Contains 'Star Slash grants four Vigor per selected card' $starSlash 'DynamicVar\("Vigor",\s*4m\)'
Assert-Contains 'Star Slash caps stacked printed values at 50' $starSlash 'Math\.Min\(selected\.Sum\(SumOriginalCardValues\),\s*50m\)'
Assert-Contains 'Star Slash voice starts after selection resolves' $starSlash 'CardSelectCmd\.FromCombatPile[\s\S]{0,320}TryPlayCardVoice\(this\)'
Assert-Contains 'Star Slash has a stacking hover tip' $cardBase '"SGC_StarSlash"\]\s*=\s*new\[\]\s*\{\s*"叠加"'
Assert-Contains 'Getter Will grants two Evolution in Getter One' $getterWill 'PowerVar<SGP_Evolution>\(2m\)[\s\S]{0,900}HasForm\(Owner,\s*ShinGetterForm\.Getter1\)[\s\S]{0,260}Apply<SGP_Evolution>'
Assert-NotContains 'Getter Will no longer makes the selected Power free' $getterWill 'SetToFreeThisTurn'
Assert-Contains 'Desperation grants three Evolution' $desperation 'PowerVar<SGP_Evolution>\(3m\)[\s\S]{0,900}Apply<SGP_Evolution>'
Assert-Contains 'Getter Ray Burst reacts after each played card' $rayPower 'AfterCardPlayed'
Assert-Contains 'Getter Ray Burst recognizes Getter anywhere in a card class name' $rayPower 'IsGetterCard\(CardModel card\)[\s\S]{0,160}\.Name\.Contains\("Getter",\s*System\.StringComparison\.Ordinal\)'
Assert-Contains 'Getter Ray Burst shares its Getter predicate between cost and play triggers' $rayPower 'TryModifyEnergyCostInCombat[\s\S]{0,220}IsGetterCard\(card\)[\s\S]{0,620}AfterCardPlayed[\s\S]{0,300}!IsGetterCard\(card\)'
Assert-NotContains 'Getter Ray Burst no longer relies on the SGC_Getter prefix' $rayPower 'StartsWith\("SGC_Getter"'
Assert-Contains 'Getter Ray Burst grants Evolution equal to its amount' $rayPower 'Apply<SGP_Evolution>[\s\S]{0,160}Amount'

foreach ($language in @('zhs', 'eng', 'jpn')) {
    $cards = Read-Json "ShinGetterMod\localization\$language\cards.json"
    $tips = Read-Json "ShinGetterMod\localization\$language\static_hover_tips.json"
    Assert-JsonContains "$language Poseidon Thunder describes Frail" $cards 'S_G_C_POSEIDON_THUNDER.description' '\{FrailPower:diff\(\)\}'
    Assert-JsonContains "$language Getter Ray Burst describes Evolution" $cards 'S_G_C_GETTER_RAY_OVERFLOW.description' '\[cyan\].+\[/cyan\]'
    Assert-JsonContains "$language Getter Will describes Evolution" $cards 'S_G_C_GETTER_WILL.description' '\[cyan\].+\[/cyan\]'
    Assert-JsonContains "$language Desperation describes Evolution" $cards 'S_G_C_DESPERATION.description' '\[cyan\].+\[/cyan\]'
    Assert-JsonNotContains "$language Meltdown no longer repeats Exhaust" $cards 'S_G_C_MELTDOWN.description' '消耗|Exhaust|廃棄'
    Assert-JsonContains "$language stacking hover title exists" $tips 'SHIN_GETTER_STACK.title' '.+'
    Assert-JsonContains "$language stacking hover explains the 50 cap" $tips 'SHIN_GETTER_STACK.description' '50'
}

foreach ($language in @('zhs', 'eng', 'jpn')) {
    $cardsText = Read-RepoFile "ShinGetterMod\localization\$language\cards.json"
    Assert-NotContains "$language card mechanic descriptions no longer color Evolution as getter_ray" $cardsText '\[getter_ray\](进化|Evolution|進化)\[/getter_ray\]'
}

$exportCommand = Read-RepoFile 'src\Diagnostics\CardExport\ShinGetterCardExportConsoleCmd.cs'
$exportRequest = Read-RepoFile 'src\Diagnostics\CardExport\ShinGetterCardPngExportRequest.cs'
$exporter = Read-RepoFile 'src\Diagnostics\CardExport\ShinGetterCardPngExporter.cs'
Assert-Contains 'export_cards requires seven arguments' $exportCommand 'tokens\.Count\s*!=\s*7'
Assert-Contains 'export_cards documents the name-format argument' $exportCommand 'nameFormat:-\|zhs\|jpn\|eng'
Assert-Contains 'Export request models all four name formats' $exportRequest 'Default[\s\S]{0,100}Zhs[\s\S]{0,100}Jpn[\s\S]{0,100}Eng'
Assert-Contains 'Exporter prefixes a three-digit sequence and hyphen' $exporter '\$"\{sequence:D3\}-\{SanitizeFilePart\(stem\)\}\{variantSuffix\}\.png"'
Assert-Contains 'Exporter reads localized card titles from packaged resources' $exporter 'res://ShinGetterMod/localization/\{language\}/cards\.json[\s\S]{0,160}Godot\.FileAccess\.GetFileAsString'
Assert-Contains 'Localized exports use plus for upgraded cards' $exporter 'variantSuffix\s*=\s*isUpgraded\s*\?\s*"\+"\s*:\s*string\.Empty'
Assert-Contains 'English exports strip the Shin Getter card id prefix' $exporter 'entry\[ShinGetterCardIdPrefix\.Length\.\.\]'

$resourceValidator = Read-RepoFile 'tools\validate-mod-resources.gd'
foreach ($language in @('eng', 'jpn', 'zhs')) {
    Assert-Contains "$language card localization is verified in the exported PCK" $resourceValidator ([regex]::Escape("res://ShinGetterMod/localization/$language/cards.json"))
    Assert-Contains "$language stacking tips are verified in the exported PCK" $resourceValidator ([regex]::Escape("res://ShinGetterMod/localization/$language/static_hover_tips.json"))
}

$cardMatches = [regex]::Matches($pool, 'ModelDb\.Card<([^>]+)>\(\)')
$cardNames = @($cardMatches | ForEach-Object { $_.Groups[1].Value })
$accelerationSequence = [Array]::IndexOf($cardNames, 'SGC_Acceleration') + 1
if ($accelerationSequence -ne 40) {
    $Failures.Add("Acceleration sequence is $accelerationSequence, expected 40")
}

if (-not (Test-Path -LiteralPath (Join-Path $Root 'art\.gitkeep') -PathType Leaf)) {
    $Failures.Add('Tracked art directory marker is missing')
}

$manifest = Read-Json 'ShinGetterMod.json'
Assert-JsonContains 'Manifest advances to v0.9.41' $manifest 'version' '^v0\.9\.41$'

if ($Failures.Count -gt 0) {
    Write-Host 'FAILED v0.9.41 2026-07-16 prelaunch reopen checks:' -ForegroundColor Red
    foreach ($failure in $Failures) { Write-Host " - $failure" -ForegroundColor Red }
    exit 1
}

Write-Host 'PASSED v0.9.41 2026-07-16 prelaunch reopen checks.' -ForegroundColor Green
