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
    try {
        return (Read-RepoFile $relativePath) | ConvertFrom-Json -AsHashtable
    }
    catch {
        $Failures.Add("Invalid JSON: $relativePath ($($_.Exception.Message))")
        return $null
    }
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

function Assert-JsonContains([string] $name, [hashtable] $json, [string] $key, [string] $pattern) {
    if ($null -eq $json -or -not $json.ContainsKey($key) -or [string]$json[$key] -notmatch $pattern) {
        $Failures.Add($name)
    }
}

function Get-SetEntries([string] $text, [string] $setName) {
    $match = [regex]::Match($text, "(?s)$setName\s*=.*?\{(?<body>.*?)\};")
    if (-not $match.Success) {
        $Failures.Add("Missing animation set: $setName")
        return @()
    }

    return [regex]::Matches($match.Groups['body'].Value, '"(?<name>SGC_[A-Za-z0-9_]+)"') |
        ForEach-Object { $_.Groups['name'].Value } |
        Sort-Object -Unique
}

function Assert-SetEquals([string] $name, [string[]] $actual, [string[]] $expected) {
    $actualText = ($actual | Sort-Object) -join ','
    $expectedText = ($expected | Sort-Object) -join ','
    if ($actualText -cne $expectedText) {
        $Failures.Add("$name (actual: $actualText)")
    }
}

$scene = Read-RepoFile "scenes\creature_visuals\shin_getter.tscn"
Assert-Contains "Getter One remains ten percent higher" $scene 'GetterOne[\s\S]{0,420}position\s*=\s*Vector2\(38,\s*-193\.6\)'
Assert-Contains "Getter Two is five percent higher" $scene 'GetterTwo[\s\S]{0,420}position\s*=\s*Vector2\(34,\s*-184\.8\)'
Assert-Contains "Getter Three is five percent higher" $scene 'GetterThree[\s\S]{0,420}position\s*=\s*Vector2\(22,\s*-203\.7\)'
Assert-Contains "Shin Getter Dragon is five percent higher" $scene 'ShinDragon[\s\S]{0,420}position\s*=\s*Vector2\(0,\s*-184\.8\)'

$selectScene = Read-RepoFile "scenes\screens\char_select\char_select_bg_shin_getter.tscn"
Assert-Contains "Character select background fills its parent" $selectScene 'anchors_preset\s*=\s*15[\s\S]{0,260}anchor_right\s*=\s*1\.0[\s\S]{0,160}anchor_bottom\s*=\s*1\.0'
Assert-Contains "Character select background ignores source size" $selectScene 'expand_mode\s*=\s*1'
Assert-Contains "Character select background uses centered aspect cover" $selectScene 'stretch_mode\s*=\s*6'

$transition = Read-RepoFile "materials\transitions\shin_getter_transition_mat.tres"
Assert-Contains "Transition implements polygon coverage" $transition 'point_in_polygon'
Assert-Contains "Transition includes the PPT top shape" $transition 'top_shape'
Assert-Contains "Transition includes the PPT lower-left shape" $transition 'lower_left_shape'
Assert-Contains "Transition includes the PPT right shape" $transition 'right_shape'
Assert-NotContains "Transition no longer uses three rectangular bands" $transition 'floor\(UV\.y\s*\*\s*3\.0\)'

$ancient = Read-RepoFile "src\Patches\ShinGetterAncientDialoguePatch.cs"
Assert-Contains "Ancient dialogue visit index follows the localization index" $ancient 'VisitIndex\s*=\s*HasRepeatingSuffix\([^;]+\)\s*\?\s*null\s*:\s*dialogueIndex'
Assert-Contains "Only r-suffixed ancient dialogue repeats" $ancient 'IsRepeating\s*=\s*HasRepeatingSuffix'
Assert-NotContains "Ancient dialogues are not all forced to repeat" $ancient 'IsRepeating\s*=\s*true'

$architect = Read-RepoFile "src\Patches\ShinGetterArchitectAttackPatch.cs"
Assert-Contains "Architect dialogue bubble closes before the attack sequence" $architect 'SpeechBubbleRef[\s\S]{0,1800}AnimOut\(\)[\s\S]{0,500}PlayGetterBeamHit'

$holyDragon = Read-RepoFile "src\Models\Cards\SGC_HolyDragonRoar.cs"
Assert-Contains "Holy Dragon Roar has fifteen base damage" $holyDragon 'DamageVar\(15m'
Assert-Contains "Holy Dragon Roar has five burn damage per exhausted card" $holyDragon 'IntVar\("BurnDamage",\s*5m\)'
Assert-Contains "Holy Dragon Roar counts exhausted Getter cards in damage" $holyDragon 'getterCards\.Count[\s\S]{0,160}BurnDamage'
Assert-Contains "Holy Dragon Roar upgrades base damage by five" $holyDragon 'Damage\.UpgradeValueBy\(5m\)'
Assert-Contains "Holy Dragon Roar upgrades burn damage by three" $holyDragon '\["BurnDamage"\]\.UpgradeValueBy\(3m\)'

$specialization = Read-RepoFile "src\Models\Cards\SGC_Specialization.cs"
Assert-NotContains "Specialization upgrade keeps Exhaust" $specialization 'RemoveKeyword\(CardKeyword\.Exhaust\)'
Assert-NotContains "Specialization form pool excludes Getter Beam" $specialization 'ModelDb\.Card<SGC_GetterBeam>'
Assert-Contains "Specialization generates an upgraded card count" $specialization 'IntVar\("Cards",\s*1m\)'
Assert-Contains "Specialization upgrades generated card count" $specialization '\["Cards"\]\.UpgradeValueBy\(1m\)'

$getterFlash = Read-RepoFile "src\Models\Cards\SGC_GetterFlash.cs"
Assert-Contains "Getter Flash Getter One bonus is two Vigor" $getterFlash 'VigorPower[^;]*2m'
Assert-Contains "Getter Flash Getter One bonus is two Airborne" $getterFlash 'SGP_Airborne[^;]*2m'

$chosenCard = Read-RepoFile "src\Models\Cards\SGC_ChosenOne.cs"
$chosenPower = Read-RepoFile "src\Models\Powers\SGP_ChosenOne.cs"
Assert-Contains "Chosen One has four base Block" $chosenCard 'BlockVar\(4m'
Assert-Contains "Chosen One upgrades Block by two" $chosenCard 'Block\.UpgradeValueBy\(2m\)'
Assert-Contains "Chosen One stores Block per transform" $chosenPower 'AddBlockPerTransform'
Assert-Contains "Chosen One grants Block on every transform" $chosenPower 'OnTransform[\s\S]{0,900}CreatureCmd\.GainBlock'
Assert-NotContains "Chosen One no longer waits for three transforms" $chosenPower 'threshold|transformCount'

$manuscript = Read-RepoFile "src\Models\Relics\SGR_KenIshikawaManuscript.cs"
Assert-Contains "Manuscript shows only the Infinite Evolution card" $manuscript 'HoverTipFactory\.FromCard<SGC_InfiniteEvolution>'
Assert-NotContains "Manuscript does not expand Infinite Evolution related hovers" $manuscript 'FromCardWithCardHoverTips'

$infinitePower = Read-RepoFile "src\Models\Powers\SGP_InfiniteEvolution.cs"
Assert-Contains "Infinite Evolution records its gain before powers are cleared" $infinitePower 'override\s+async\s+Task\s+AfterCombatEnd'
Assert-NotContains "Infinite Evolution no longer runs after powers are cleared" $infinitePower 'AfterCombatVictory'
Assert-NotContains "Infinite Evolution does not add transient powers while listeners are iterating" $infinitePower 'PowerCmd\.Apply<(Strength|Dexterity)Power>'

$virus = Read-RepoFile "src\Models\Cards\SGC_InsectVirus.cs"
Assert-Contains "Insect Virus uses the original Curse visual pool" $virus 'VisualCardPool\s*=>\s*ModelDb\.CardPool<CurseCardPool>'

$cardBase = Read-RepoFile "src\Models\Cards\ShinGetterCardBase.cs"
$expectedDash = @(
    'SGC_Acceleration', 'SGC_DiveStrike', 'SGC_Enable', 'SGC_GetterElbow',
    'SGC_GetterFlash', 'SGC_GetterRush', 'SGC_LigerAssault', 'SGC_ShiningSpark'
)
$expectedCastAttacks = @(
    'SGC_Annihilation', 'SGC_FinalGetterBeam', 'SGC_GetterBeam',
    'SGC_HolyDragonRoar', 'SGC_PoseidonThunder', 'SGC_StonerSunshine'
)
$expectedBlock = @(
    'SGC_BlackArmor', 'SGC_DarkCape', 'SGC_Defend', 'SGC_Guts',
    'SGC_HedgehogTactic', 'SGC_IronWall', 'SGC_SeizeFuture', 'SGC_TacticalRetreat'
)
Assert-SetEquals "Dash animation cards match the Obsidian Base" (Get-SetEntries $cardBase 'DashAnimationCards') $expectedDash
Assert-SetEquals "Cast attack cards match the Obsidian Base" (Get-SetEntries $cardBase 'CastAttackAnimationCards') $expectedCastAttacks
Assert-SetEquals "Block animation cards match the Obsidian Base" (Get-SetEntries $cardBase 'BlockAnimationCards') $expectedBlock
Assert-Contains "Dash animation dispatch also applies to Skill and Power cards" $cardBase 'DashAnimationCards\.Contains\(cardTypeName\)[\s\S]{0,160}return\s+"Dash";[\s\S]{0,160}Type\s*==\s*CardType\.Attack'

foreach ($language in @('zhs', 'eng', 'jpn')) {
    $cards = Read-Json "ShinGetterMod\localization\$language\cards.json"
    $powers = Read-Json "ShinGetterMod\localization\$language\powers.json"

    Assert-JsonContains "$language Getter Chop steals Block twice" $cards 'S_G_C_GETTER_CHOP.description' '(2次|2 times|2回)'
    Assert-JsonContains "$language Holy Dragon Roar exposes burn damage" $cards 'S_G_C_HOLY_DRAGON_ROAR.description' '\{BurnDamage:diff\(\)\}'
    Assert-JsonContains "$language Holy Dragon Roar does not duplicate Exhaust text" $cards 'S_G_C_HOLY_DRAGON_ROAR.description' '^(?![\s\S]*(\[gold\]消耗\[/gold\]|\[gold\]Exhaust\[/gold\]|\[gold\]廃棄\[/gold\]))[\s\S]*$'
    Assert-JsonContains "$language Getter Flash shows the reduced form bonus" $cards 'S_G_C_GETTER_FLASH.description' '(2点|Gain 2|を2)'
    Assert-JsonContains "$language Chosen One shows Block" $cards 'S_G_C_CHOSEN_ONE.description' '\{Block:diff\(\)\}'
    Assert-JsonContains "$language Specialization shows generated card count" $cards 'S_G_C_SPECIALIZATION.description' '\{Cards:diff\(\)\}'
    Assert-JsonContains "$language Chosen One power describes every transform" $powers 'S_G_P_CHOSEN_ONE.description' '(每次|Whenever|たび)'
}

if ($Failures.Count -gt 0) {
    Write-Host "FAILED v0.9.34 2026-07-11 second playtest follow-up checks:" -ForegroundColor Red
    foreach ($failure in $Failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "PASSED v0.9.34 2026-07-11 second playtest follow-up checks." -ForegroundColor Green
