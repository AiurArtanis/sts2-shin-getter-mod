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
    $text = Read-RepoFile $relativePath
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    try {
        return $text | ConvertFrom-Json -AsHashtable
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

function Assert-ContainsCaseSensitive([string] $name, [string] $text, [string] $pattern) {
    if ($text -cnotmatch $pattern) {
        $Failures.Add($name)
    }
}

function Assert-NotContains([string] $name, [string] $text, [string] $pattern) {
    if ($text -match $pattern) {
        $Failures.Add($name)
    }
}

function Assert-FileMinSize([string] $name, [string] $relativePath, [long] $minimumBytes) {
    $path = Join-Path $Root $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        $Failures.Add("$name (missing $relativePath)")
        return
    }

    if ((Get-Item -LiteralPath $path).Length -lt $minimumBytes) {
        $Failures.Add("$name (smaller than $minimumBytes bytes)")
    }
}

function Assert-JsonContains([string] $name, [hashtable] $json, [string] $key, [string] $pattern) {
    if ($null -eq $json -or -not $json.ContainsKey($key) -or [string]$json[$key] -notmatch $pattern) {
        $Failures.Add($name)
    }
}

$sequence = Read-RepoFile "src\Nodes\Combat\NShinGetterSpriteSequence.cs"
$visuals = Read-RepoFile "src\Nodes\Combat\NShinGetterStaticVisuals.cs"
$cardBase = Read-RepoFile "src\Models\Cards\ShinGetterCardBase.cs"
$combatVfx = (Read-RepoFile "src\Nodes\Vfx\ShinGetterCombatVfx.cs") + (Read-RepoFile "src\Nodes\Vfx\ShinGetterCombatVfx.Extra.cs")
$creatureScene = Read-RepoFile "scenes\creature_visuals\shin_getter.tscn"

Assert-Contains "Sprite sequences expose idle-only lazy loaders" $sequence 'EnsureGetterTwoIdleLoaded|EnsureIdleLoaded'
Assert-Contains "Form switching uses idle-only loading" $visuals 'EnsureGetterTwoIdleLoaded|EnsureIdleLoaded'
Assert-Contains "Movement VFX cards are deferred from enqueue animation" $cardBase 'MovementVfxTimingCards'
Assert-Contains "Movement VFX starts its action animation at displacement time" $cardBase 'PlayMovementVfx'
Assert-Contains "Dive Strike synchronizes movement and Dash animation" (Read-RepoFile "src\Models\Cards\SGC_DiveStrike.cs") 'PlayMovementVfx'
Assert-Contains "Getter Rush synchronizes movement and Dash animation" (Read-RepoFile "src\Models\Cards\SGC_GetterRush.cs") 'PlayMovementVfx'
Assert-Contains "Getter Elbow synchronizes movement and its action animation" (Read-RepoFile "src\Models\Cards\SGC_GetterElbow.cs") 'PlayMovementVfx'
Assert-Contains "Getter Flash synchronizes movement and its action animation" (Read-RepoFile "src\Models\Cards\SGC_GetterFlash.cs") 'PlayMovementVfx'
Assert-Contains "Expansion Strike synchronizes movement and its action animation" (Read-RepoFile "src\Models\Cards\SGC_ExpansionStrike.cs") 'PlayMovementVfx'
Assert-Contains "Shining Spark synchronizes movement and its action animation" (Read-RepoFile "src\Models\Cards\SGC_ShiningSpark.cs") 'PlayMovementVfx'
Assert-Contains "Tactical Retreat synchronizes movement and its action animation" (Read-RepoFile "src\Models\Cards\SGC_TacticalRetreat.cs") 'PlayMovementVfx'

Assert-Contains "All four forms move upward by ten percent" $creatureScene 'GetterOne[\s\S]{0,420}position\s*=\s*Vector2\(38,\s*-193\.6\)[\s\S]{0,900}GetterTwo[\s\S]{0,420}position\s*=\s*Vector2\(34,\s*-193\.6\)[\s\S]{0,900}GetterThree[\s\S]{0,420}position\s*=\s*Vector2\(22,\s*-213\.4\)[\s\S]{0,900}ShinDragon[\s\S]{0,420}position\s*=\s*Vector2\(0,\s*-193\.6\)'

$tornado = Read-RepoFile "src\Models\Cards\SGC_TornadoDrill.cs"
Assert-Contains "Tornado Drill uses a rising drill VFX" $tornado 'PlayRisingDrill'
Assert-Contains "Rising drill VFX exists" $combatVfx 'Task\s+PlayRisingDrill'
Assert-Contains "Rising drill travels upward" $combatVfx 'Vector2\.Up'

$shiftStrike = Read-RepoFile "src\Models\Cards\SGC_ShiftStrike.cs"
Assert-Contains "Upgraded Shift Strike transforms before damage and after buffs" $shiftStrike 'if\s*\(IsUpgraded\)[\s\S]{0,220}Transform\([\s\S]{0,500}DamageCmd\.Attack[\s\S]{0,700}VigorPower[\s\S]{0,500}RegenPower[\s\S]{0,500}PlatingPower[\s\S]{0,420}Transform\('

$steelSpirit = Read-RepoFile "src\Models\Cards\SGC_SteelSpirit.cs"
Assert-Contains "Steel Spirit always has Ethereal" $steelSpirit 'CanonicalKeywords[\s\S]{0,180}CardKeyword\.Ethereal'
Assert-NotContains "Steel Spirit upgrade does not remove Ethereal" $steelSpirit 'OnUpgrade\(\)[\s\S]{0,160}RemoveKeyword\(CardKeyword\.Ethereal\)'

$shinForm = Read-RepoFile "src\Models\Cards\SGC_ShinForm.cs"
Assert-Contains "Shin Form is a Power card" $shinForm 'base\(4,\s*CardType\.Power,\s*CardRarity\.Ancient'
Assert-Contains "Shin Form awaits its wrapping VFX before changing form" $shinForm 'await\s+NShinGetterStaticVisuals\.PlayShinFormTransformVfx\([\s\S]{0,700}PowerCmd\.Apply<SGP_ShinForm>'
Assert-Contains "Shin Form VFX is awaitable" $visuals 'async\s+Task\s+PlayShinFormTransformVfx'
Assert-Contains "Shin form transform grants Vigor" $cardBase 'TriggerShinFormTransform[\s\S]{0,900}PowerCmd\.Apply<VigorPower>'
Assert-Contains "Shin form transform grants Regen" $cardBase 'TriggerShinFormTransform[\s\S]{0,1100}PowerCmd\.Apply<RegenPower>'
Assert-Contains "Shin form transform grants Plating" $cardBase 'TriggerShinFormTransform[\s\S]{0,1300}PowerCmd\.Apply<PlatingPower>'

$finalBeam = Read-RepoFile "src\Models\Cards\SGC_FinalGetterBeam.cs"
$temporaryStrength = Read-RepoFile "src\Models\Powers\SGP_FinalGetterBeamStrengthDown.cs"
Assert-Contains "Final Getter Beam uses its own temporary strength power" $finalBeam 'PowerCmd\.Apply<SGP_FinalGetterBeamStrengthDown>'
Assert-NotContains "Final Getter Beam no longer uses Mangle" $finalBeam 'ManglePower'
Assert-Contains "Final Getter Beam temporary strength power reuses core behavior" $temporaryStrength 'TemporaryStrengthPower'
Assert-FileMinSize "Final Getter Beam strength-down big icon reuses a real icon" "images\powers\s_g_p_final_getter_beam_strength_down.png" 1000
Assert-Contains "Final Getter Beam strength-down atlas alias exists" (Read-RepoFile "images\atlases\power_atlas.sprites\s_g_p_final_getter_beam_strength_down.tres") 'mangle_power\.tres'

$holyDragon = Read-RepoFile "src\Models\Cards\SGC_HolyDragonRoar.cs"
Assert-Contains "Holy Dragon Roar uses the provided large portrait" $holyDragon 's_g_c_saint_dragon_roar\.png'
Assert-Contains "Holy Dragon Roar uses the provided card portrait" $holyDragon 's_g_c_saint_dragon_roar_card\.png'
Assert-Contains "Holy Dragon Roar costs three and targets all enemies" $holyDragon 'base\(3,\s*CardType\.Attack,\s*CardRarity\.Ancient,\s*TargetType\.AllEnemies\)'
Assert-Contains "Holy Dragon Roar deals 20 base damage" $holyDragon 'DamageVar\(20m'
Assert-Contains "Holy Dragon Roar upgrades damage by ten" $holyDragon 'UpgradeValueBy\(10m\)'
Assert-Contains "Holy Dragon Roar stuns enemies" $holyDragon 'CreatureCmd\.Stun'
Assert-Contains "Holy Dragon Roar exhausts Getter cards" $holyDragon 'CardCmd\.Exhaust'
Assert-Contains "Holy Dragon Roar identifies Getter cards by stable model ID" $holyDragon 'IsGetterCard[\s\S]{0,220}Id\.Entry[\s\S]{0,120}GETTER'
Assert-NotContains "Holy Dragon Roar does not depend on localized card titles" $holyDragon 'IsGetterCard[\s\S]{0,220}\.Title'
Assert-Contains "Holy Dragon Roar has full-screen ray VFX" $holyDragon 'PlayHolyDragonRoar'
Assert-Contains "Holy Dragon Roar VFX exists" $combatVfx 'Task\s+PlayHolyDragonRoar'
Assert-Contains "Attack animation falls back to Cast for forms without Attack frames" $visuals 'trigger\s*==\s*"Attack"[\s\S]{0,500}TryPlay\(animation,\s*trigger[\s\S]{0,400}TryPlay\(animation,\s*"Cast"'

$getterLaunch = Read-RepoFile "src\Models\Cards\SGC_GetterLaunch.cs"
$meltdown = Read-RepoFile "src\Models\Cards\SGC_Meltdown.cs"
$hedgehog = Read-RepoFile "src\Models\Cards\SGC_HedgehogTactic.cs"
$specialization = Read-RepoFile "src\Models\Cards\SGC_Specialization.cs"
$backupPlan = Read-RepoFile "src\Models\Cards\SGC_BackupPlan.cs"
Assert-Contains "Getter Launch grants two Ki" $getterLaunch 'PowerCmd\.Apply<SGP_Ki>\([^;]*2m'
Assert-Contains "Meltdown has Exhaust" $meltdown 'CanonicalKeywords[\s\S]{0,180}CardKeyword\.Exhaust'
Assert-Contains "Hedgehog Tactic upgrades Block by two" $hedgehog 'Block\.UpgradeValueBy\(2m\)'
Assert-Contains "Hedgehog Tactic upgrades Vigor by one" $hedgehog 'VigorPower[^;]*UpgradeValueBy\(1m\)'
Assert-Contains "Specialization gains Block" $specialization 'CreatureCmd\.GainBlock'
Assert-Contains "Specialization has six base Block" $specialization 'BlockVar\(6m'
Assert-Contains "Specialization upgrades Block by two" $specialization 'Block\.UpgradeValueBy\(2m\)'
Assert-Contains "Backup Plan gains Block" $backupPlan 'CreatureCmd\.GainBlock'
Assert-Contains "Backup Plan has five Block" $backupPlan 'BlockVar\(5m'

$mandala = Read-RepoFile "src\Models\Events\SGE_GetterMandala.cs"
Assert-NotContains "Mandala no longer upgrades Shin Form reward" $mandala 'CardCmd\.Upgrade\(card\)'
Assert-Contains "Mandala Shin Form option has a card hover" $mandala 'GETTER_G_FUSION[\s\S]{0,300}HoverTipFactory\.FromCard<SGC_ShinForm>'
Assert-Contains "Mandala Devolution option has an enchantment hover" $mandala 'PRIMAL_GETTER[\s\S]{0,300}HoverTipFactory\.FromEnchantment<SGE_Devolution>'
Assert-Contains "Mandala Adaptation option has an enchantment hover" $mandala 'FIRST_EVOLUTION[\s\S]{0,300}HoverTipFactory\.FromEnchantment<SGE_Adaptation>'
Assert-Contains "Mandala Holy Dragon option has a card hover" $mandala 'HOLY_DRAGON[\s\S]{0,300}HoverTipFactory\.FromCard<SGC_HolyDragonRoar>'
Assert-Contains "Mandala solar option exposes both relic hovers" $mandala 'SOLAR_BATTLESHIP[\s\S]{0,500}SGR_GetterFurnace[\s\S]{0,500}SGR_EmperorsFragment'
Assert-Contains "Mandala ignore option has Insect Virus hover" $mandala 'IGNORE[\s\S]{0,300}HoverTipFactory\.FromCard<SGC_InsectVirus>'

$devolution = Read-RepoFile "src\Models\Enchantments\SGE_Devolution.cs"
$adaptation = Read-RepoFile "src\Models\Enchantments\SGE_Adaptation.cs"
Assert-Contains "Devolution excludes zero-cost cards" $devolution 'GetWithModifiers\(CostModifiers\.None\)\s*>\s*0'
Assert-Contains "Adaptation only decrements stackable debuffs" $adaptation 'StackType\s*==\s*PowerStackType\.Counter'

foreach ($language in @("zhs", "eng", "jpn")) {
    $cards = Read-Json "ShinGetterMod\localization\$language\cards.json"
    $events = Read-Json "ShinGetterMod\localization\$language\events.json"
    $powers = Read-Json "ShinGetterMod\localization\$language\powers.json"

    Assert-JsonContains "$language Getter Launch text shows two Ki" $cards "S_G_C_GETTER_LAUNCH.description" '(2|二)'
    Assert-JsonContains "$language Meltdown text includes Exhaust" $cards "S_G_C_MELTDOWN.description" '(Exhaust|消耗|廃棄)'
    Assert-JsonContains "$language Specialization text includes Block" $cards "S_G_C_SPECIALIZATION.description" '\{Block:diff\(\)\}'
    Assert-JsonContains "$language Backup Plan text includes Block" $cards "S_G_C_BACKUP_PLAN.description" '\{Block:diff\(\)\}'
    Assert-JsonContains "$language Holy Dragon text is implemented" $cards "S_G_C_HOLY_DRAGON_ROAR.description" '\{Damage:diff\(\)\}'
    Assert-JsonContains "$language Final Beam strength power title exists" $powers "S_G_P_FINAL_GETTER_BEAM_STRENGTH_DOWN.title" '.+'
    Assert-JsonContains "$language Mandala Shin Form reward is not upgraded" $events "S_G_E_GETTER_MANDALA.pages.INITIAL.options.GETTER_G_FUSION.description" '^(?!.*(升级|upgrad|アップグレード)).*$'

    foreach ($key in @(
        "S_G_E_GETTER_MANDALA.pages.INITIAL.options.GETTER_G_FUSION.description",
        "S_G_E_GETTER_MANDALA.pages.INITIAL.options.PRIMAL_GETTER.description",
        "S_G_E_GETTER_MANDALA.pages.INITIAL.options.FIRST_EVOLUTION.description",
        "S_G_E_GETTER_MANDALA.pages.INITIAL.options.HOLY_DRAGON.description",
        "S_G_E_GETTER_MANDALA.pages.INITIAL.options.SOLAR_BATTLESHIP.description"
    )) {
        Assert-JsonContains "$language Mandala terms have no quotation marks: $key" $events $key '^[^“”「」"]+$'
    }
}

Assert-FileMinSize "Enable small power icon is present" "images\atlases\power_icons_atlas_shin_getter.png" 265000
Assert-FileMinSize "Enable large power icon is present" "images\powers\s_g_p_enable.png" 40000
Assert-Contains "Enable atlas points at its new slot" (Read-RepoFile "images\atlases\power_atlas.sprites\s_g_p_enable.tres") 'Rect2\(64,\s*256,\s*64,\s*64\)'
Assert-FileMinSize "Kusuha Juice large icon is restored" "images\powers\s_g_p_kusuha_juice.png" 60000
Assert-FileMinSize "Large power atlas contains the new state art" "images\atlases\power_atlas_shin_getter.png" 2450000

$character = Read-RepoFile "src\Models\Characters\ShinGetter.cs"
$visualPatch = Read-RepoFile "src\Patches\ShinGetterVisualsPatch.cs"
$audioPatch = Read-RepoFile "src\Patches\ShinGetterCharacterSelectAudioPatch.cs"
$selectScene = Read-RepoFile "scenes\screens\char_select\char_select_bg_shin_getter.tscn"
$transitionMaterial = Read-RepoFile "materials\transitions\shin_getter_transition_mat.tres"
Assert-FileMinSize "Updated character select portrait is present" "images\packed\character_select\char_select_shin_getter.png" 55000
Assert-FileMinSize "Character select background is imported" "animations\character_select\shin_getter\character_select_shin_getter_bg.png" 1500000
Assert-FileMinSize "Character select sound is imported" "audio\sfx\characters\shin_getter\shin_getter_select.wav" 90000
Assert-Contains "Character uses the custom selection sound" $character 'res://audio/sfx/characters/shin_getter/shin_getter_select\.wav'
Assert-Contains "Character selection sound patch plays WAV resources" $audioPatch 'AudioStreamPlayer'
Assert-ContainsCaseSensitive "Character selection sound uses the game SFX bus" $audioPatch 'Bus\s*=\s*"SFX"'
Assert-Contains "Character background patch uses the custom scene" $visualPatch 'char_select_bg_shin_getter\.tscn'
Assert-Contains "Character transition patch uses the custom material" $visualPatch 'shin_getter_transition_mat\.tres'
Assert-NotContains "Character visuals no longer fall back to Ironclad" $visualPatch 'ironclad'
Assert-Contains "Character select scene uses the provided background" $selectScene 'character_select_shin_getter_bg\.png'
Assert-Contains "Transition shader divides the screen into three bands" $transitionMaterial 'third|3\.0'
Assert-Contains "Transition shader exposes the standard threshold parameter" $transitionMaterial 'uniform\s+float\s+threshold'

if ($Failures.Count -gt 0) {
    Write-Host "FAILED v0.9.33 2026-07-11 reopened second playtest checks:" -ForegroundColor Red
    foreach ($failure in $Failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "PASSED v0.9.33 2026-07-11 reopened second playtest checks." -ForegroundColor Green
