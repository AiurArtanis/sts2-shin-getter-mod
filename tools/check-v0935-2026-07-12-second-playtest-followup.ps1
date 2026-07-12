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

foreach ($file in @(
    'images\relics\s_g_r_yummy_cookie.png',
    'images\atlases\relic_atlas.sprites\s_g_r_yummy_cookie.tres',
    'images\atlases\relic_outline_atlas.sprites\s_g_r_yummy_cookie.tres',
    'src\Models\Relics\SGR_YummyCookie.cs',
    'src\Patches\ShinGetterTezcataraPatch.cs'
)) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $file))) { $Failures.Add("Missing Yummy Cookie asset/code: $file") }
}

$cookieAtlas = Read-RepoFile 'images\atlases\relic_atlas.sprites\s_g_r_yummy_cookie.tres'
Assert-Contains 'Yummy Cookie uses atlas slot thirteen' $cookieAtlas 'region\s*=\s*Rect2\(0,\s*256,\s*128,\s*128\)'
$cookie = Read-RepoFile 'src\Models\Relics\SGR_YummyCookie.cs'
Assert-Contains 'Getter Yummy Cookie upgrades four cards' $cookie 'CardsVar\(4\)'
Assert-Contains 'Getter Yummy Cookie upgrades selected cards' $cookie 'CardCmd\.Upgrade'
$tezcatara = Read-RepoFile 'src\Patches\ShinGetterTezcataraPatch.cs'
Assert-Contains 'Tezcatara replaces Yummy Cookie only for Shin Getter' $tezcatara 'Tezcatara[\s\S]*ShinGetter[\s\S]*YummyCookie[\s\S]*SGR_YummyCookie'

$scene = Read-RepoFile 'scenes\creature_visuals\shin_getter.tscn'
Assert-Contains 'Getter One is another five percent higher' $scene 'GetterOne[\s\S]{0,420}position\s*=\s*Vector2\(38,\s*-202\.4\)'
Assert-Contains 'Getter Two is another five percent higher' $scene 'GetterTwo[\s\S]{0,420}position\s*=\s*Vector2\(34,\s*-193\.6\)'
Assert-Contains 'Getter Three is another five percent higher' $scene 'GetterThree[\s\S]{0,420}position\s*=\s*Vector2\(22,\s*-213\.4\)'
Assert-Contains 'Shin Getter Dragon keeps its prior height' $scene 'ShinDragon[\s\S]{0,420}position\s*=\s*Vector2\(0,\s*-184\.8\)'

$selectScene = Read-RepoFile 'scenes\screens\char_select\char_select_bg_shin_getter.tscn'
Assert-Contains 'Character select background uses viewport fitter' $selectScene 'NShinGetterCharacterSelectBackground\.cs'
$selectFitter = Read-RepoFile 'src\Nodes\Screens\NShinGetterCharacterSelectBackground.cs'
Assert-Contains 'Character select fitter registers as a Godot global class' $selectFitter '\[GlobalClass\]'
Assert-Contains 'Character select fitter reads the game viewport' $selectFitter 'GetVisibleRect\(\)'
Assert-Contains 'Character select fitter cancels parent overscan transform' $selectFitter 'AffineInverse\(\)'
$resourceValidator = Read-RepoFile 'tools\validate-mod-resources.gd'
Assert-Contains 'C sharp character select scene uses PCK existence validation' $resourceValidator 'EXISTS_ONLY_RESOURCES\s*:=\s*\[[\s\S]*char_select_bg_shin_getter\.tscn'
Assert-Contains 'Yummy Cookie atlas is required in exported PCK' $resourceValidator 'relic_atlas\.sprites/s_g_r_yummy_cookie\.tres'
Assert-Contains 'Yummy Cookie outline is required in exported PCK' $resourceValidator 'relic_outline_atlas\.sprites/s_g_r_yummy_cookie\.tres'
Assert-Contains 'Temporary Dexterity icon is required in exported PCK' $resourceValidator 'power_atlas\.sprites/s_g_p_hurricane_temporary_dexterity\.tres'

$staticVisuals = Read-RepoFile 'src\Nodes\Combat\NShinGetterStaticVisuals.cs'
Assert-Contains 'Shin Form uses rising ray rings' $staticVisuals 'CreateRisingRayRing'
Assert-Contains 'Shin Form ends in a full ray silhouette' $staticVisuals 'CreateGetterRaySilhouette'

$emperor = Read-RepoFile 'src\Models\Relics\SGR_EmperorsFragment.cs'
Assert-Contains 'Emperor Fragment restores Getter One at combat start' $emperor 'PowerCmd\.Apply<SGP_ShinGetterOne>'

$holy = Read-RepoFile 'src\Models\Cards\SGC_HolyDragonRoar.cs'
Assert-Contains 'Holy Dragon Roar deals one combined attack' $holy 'decimal\s+totalDamage[\s\S]{0,220}DamageCmd\.Attack\(totalDamage\)'
Assert-NotContains 'Holy Dragon Roar does not use hit count' $holy 'WithHitCount'

$backup = Read-RepoFile 'src\Models\Cards\SGC_BackupPlan.cs'
Assert-Contains 'Backup Plan uses its custom selection prompt' $backup 'CardSelectorPrefs\(SelectionScreenPrompt'

$beam = Read-RepoFile 'src\Nodes\Vfx\ShinGetterBeamVfx.cs'
Assert-Contains 'Final Getter Beam widens Hyperbeam by thirty percent' $beam 'Width\s*\*=\s*1\.3f'
Assert-Contains 'Final Getter Beam remaps only the blue palette' $beam 'RemapBlueToGetterRay'
Assert-Contains 'Final Getter Beam adds a centered Getter beam' $beam 'AddCenterGetterBeam'
Assert-NotContains 'Final Getter Beam keeps Hyperbeam materials' $beam 'line\.Material\s*=\s*null'

$chosen = Read-RepoFile 'src\Models\Powers\SGP_ChosenOne.cs'
Assert-Contains 'Chosen One fills the Block localization variable' $chosen 'description\.Add\("Block"'

$mutationPatch = Read-RepoFile 'src\Patches\ShinGetterCardMutationVisualPatch.cs'
Assert-Contains 'Card mutation visual refreshes are batched' $mutationPatch 'BeginBatch[\s\S]*EndBatch'
Assert-Contains 'Globe Head mutation is batched' $mutationPatch 'GalvanicPower'
Assert-Contains 'Spectral Knight mutation is batched' $mutationPatch 'HexPower'
Assert-Contains 'Magi Knight mutation is batched' $mutationPatch 'DampenPower'

$jammer = Read-RepoFile 'src\Models\Cards\SGC_Jammer.cs'
Assert-Contains 'Jammer gains ten Block' $jammer 'BlockVar\(10m'
Assert-Contains 'Jammer applies its Block' $jammer 'CreatureCmd\.GainBlock'
$ki = Read-RepoFile 'src\Models\Cards\SGC_Ki.cs'
Assert-Contains 'Ki upgrades Vigor instead of cost' $ki 'VigorPower"\]\.UpgradeValueBy\(1m\)'
Assert-NotContains 'Ki upgrade no longer reduces cost' $ki 'EnergyCost\.UpgradeBy'
$spirit = Read-RepoFile 'src\Models\Cards\SGC_Spirit.cs'
Assert-Contains 'Spirit upgrades the transformed Ki card' $spirit 'CreateKiCard[\s\S]*CardCmd\.Upgrade'
Assert-NotContains 'Spirit upgrade no longer transforms two cards' $spirit 'Cards\.UpgradeValueBy'
$shedLoad = Read-RepoFile 'src\Models\Cards\SGC_ShedLoad.cs'
Assert-Contains 'Shed Load grants ten Block per Ki' $shedLoad 'BlockVar\(10m'
Assert-Contains 'Shed Load Getter Two bonus grants Dexterity' $shedLoad 'DexterityPower'
Assert-Contains 'Shed Load Getter Two bonus grants Regen' $shedLoad 'RegenPower'
$hotBlood = Read-RepoFile 'src\Models\Cards\SGC_HotBlood.cs'
Assert-Contains 'Hot Blood base Spirit requirement is four' $hotBlood 'IsUpgraded\s*\?\s*2\s*:\s*4'
$awakened = Read-RepoFile 'src\Models\Cards\SGC_AwakenedSoul.cs'
Assert-Contains 'Awakened Soul has six base Vigor' $awakened 'SGP_AwakenedSoul>\(6m\)'
Assert-Contains 'Awakened Soul upgrades to nine Vigor' $awakened 'UpgradeValueBy\(3m\)'
$getterBeam = Read-RepoFile 'src\Models\Cards\SGC_GetterBeam.cs'
Assert-Contains 'Getter Beam has eight base damage' $getterBeam 'CalculationBaseVar\(8m\)'
Assert-Contains 'Getter Beam bonus counts Ki gained this combat' $getterBeam 'entry\.Power\s+is\s+SGP_Ki'
$spark = Read-RepoFile 'src\Models\Cards\SGC_ShiningSpark.cs'
Assert-Contains 'Shining Spark costs two' $spark ':\s*base\(2,\s*CardType\.Attack'
Assert-Contains 'Shining Spark gains two Vulnerable' $spark 'VulnerablePower[^;]*2m'
Assert-Contains 'Shining Spark gains two Frail' $spark 'FrailPower[^;]*2m'
Assert-Contains 'Shining Spark accelerates follow-up attacks' $spark 'PlayShiningSparkFollowup'
$insight = Read-RepoFile 'src\Models\Cards\SGC_Insight.cs'
Assert-Contains 'Insight has two base Dexterity' $insight 'DexterityPower>\(2m\)'
$hurricane = Read-RepoFile 'src\Models\Cards\SGC_HurricaneStrike.cs'
Assert-Contains 'Hurricane Strike uses temporary Dexterity' $hurricane 'TemporaryDexterity'
$juice = Read-RepoFile 'src\Models\Potions\SGR_KusuhaJuice.cs'
Assert-Contains 'Kusuha Juice includes Vulnerable' $juice 'VulnerablePower'

$warriorPower = Read-RepoFile 'src\Models\Powers\SGP_WarriorMedal.cs'
Assert-NotContains 'Warrior Medal no longer scales from Ki' $warriorPower 'GetPower<SGP_Ki>'

$manifest = Read-Json 'ShinGetterMod.json'
Assert-JsonContains 'Manifest advances to v0.9.35' $manifest 'version' '^v0\.9\.35$'

foreach ($language in @('zhs', 'eng', 'jpn')) {
    $cards = Read-Json "ShinGetterMod\localization\$language\cards.json"
    $powers = Read-Json "ShinGetterMod\localization\$language\powers.json"
    $potions = Read-Json "ShinGetterMod\localization\$language\potions.json"
    $relics = Read-Json "ShinGetterMod\localization\$language\relics.json"

    Assert-JsonContains "$language Backup Plan has custom selection prompt" $cards 'S_G_C_BACKUP_PLAN.selectionScreenPrompt' '.+'
    Assert-JsonContains "$language Holy Dragon Roar describes one combined hit" $cards 'S_G_C_HOLY_DRAGON_ROAR.description' '\{Damage:diff\(\)\}[\s\S]*\{BurnDamage:diff\(\)\}'
    Assert-JsonContains "$language Chosen One power has Block" $powers 'S_G_P_CHOSEN_ONE.description' '\{Block\}'
    Assert-JsonContains "$language Kusuha Juice mentions Vulnerable" $potions 'S_G_R_KUSUHA_JUICE.description' '(脆弱|Vulnerable|脱力)'
    Assert-JsonContains "$language Getter Yummy Cookie title exists" $relics 'S_G_R_YUMMY_COOKIE.title' '.+'
}

if ($Failures.Count -gt 0) {
    Write-Host 'FAILED v0.9.35 2026-07-12 second playtest follow-up checks:' -ForegroundColor Red
    foreach ($failure in $Failures) { Write-Host " - $failure" -ForegroundColor Red }
    exit 1
}

Write-Host 'PASSED v0.9.35 2026-07-12 second playtest follow-up checks.' -ForegroundColor Green
