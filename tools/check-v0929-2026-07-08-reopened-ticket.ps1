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

function Assert-File([string] $relativePath) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $relativePath))) {
        $Failures.Add("Missing file: $relativePath")
    }
}

function Assert-DirectoryFrameCount([string] $relativePath, [int] $expectedCount) {
    $path = Join-Path $Root $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        $Failures.Add("Missing animation directory: $relativePath")
        return
    }

    $actualCount = @(Get-ChildItem -LiteralPath $path -File -Filter "sprite_*.png").Count
    if ($actualCount -ne $expectedCount) {
        $Failures.Add("$relativePath has $actualCount png frames, expected $expectedCount")
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

function Assert-JsonTextContains([string] $relativePath, [string] $name, [string] $pattern) {
    $text = Read-RepoFile $relativePath
    Assert-Contains $name $text $pattern
}

$transformPotion = Read-RepoFile "src\Models\Potions\SGR_TransformPotion.cs"
Assert-Contains "Transform potion uses EnergyVar" $transformPotion "new\s+EnergyVar\(1\)"
Assert-Contains "Transform potion gains energy" $transformPotion "PlayerCmd\.GainEnergy\(DynamicVars\.Energy\.BaseValue,\s*Owner\)"
Assert-NotContains "Transform potion no longer uses StarsVar" $transformPotion "StarsVar"
Assert-NotContains "Transform potion no longer gains stars" $transformPotion "GainStars"

foreach ($lang in @("zhs", "eng", "jpn")) {
    Assert-JsonTextContains "ShinGetterMod\localization\$lang\potions.json" "$lang transform potion uses Energy var" '"S_G_R_TRANSFORM_POTION\.description": "[^"]*\{Energy\}'
    Assert-JsonTextContains "ShinGetterMod\localization\$lang\potions.json" "$lang transform potion no longer uses Stars var" '^(?s)(?!.*"S_G_R_TRANSFORM_POTION\.description": "[^"]*\{Stars\}).*$'
}

$cardAlias = Read-RepoFile "src\Patches\CardConsoleAliasPatch.cs"
Assert-Contains "Card console alias handles lowercase model ids" $cardAlias "s_g_c_saint_dragon_roar"
Assert-Contains "Card console alias maps asset alias to Holy Dragon Roar" $cardAlias "S_G_C_HOLY_DRAGON_ROAR"
Assert-Contains "Card console alias normalizes s_g_c snake ids" $cardAlias "NormalizeSnakeAlias"

Assert-File "src\Patches\EventConsoleAliasPatch.cs"
$eventAlias = Read-RepoFile "src\Patches\EventConsoleAliasPatch.cs"
Assert-Contains "Event console alias patches event command" $eventAlias "EventConsoleCmd"
Assert-Contains "Event console alias supports legacy s_g_c prefix" $eventAlias "s_g_c_getter_mandala"
Assert-Contains "Event console alias supports event prefix" $eventAlias "s_g_e_getter_mandala"
Assert-Contains "Event console alias maps Getter Mandala" $eventAlias "S_G_E_GETTER_MANDALA"

$getterNova = Read-RepoFile "src\Models\Cards\SGC_GetterNova.cs"
Assert-Contains "Getter Nova applies radiation only to opponents" $getterNova "CombatState\.GetOpponentsOf\(Owner\.Creature\)[\s\S]*PowerCmd\.Apply<SGP_Radiation>"
Assert-NotContains "Getter Nova no longer applies radiation to all creatures" $getterNova "CombatState\.Creatures\.Where\(creature\s*=>\s*creature\.IsAlive\)"

$spiralDrill = Read-RepoFile "src\Models\Cards\SGC_SpiralDrill.cs"
Assert-Contains "Spiral Drill consumes Hot Blood after Getter 2 unblockable damage" $spiralDrill "ConsumeForCardDamage\(choiceContext,\s*this,\s*ValueProp\.Move\s*\|\s*ValueProp\.Unblockable\)"
$hotBlood = Read-RepoFile "src\Models\Powers\SGP_HotBlood.cs"
Assert-Contains "Hot Blood exposes non-AttackCommand card damage consumption" $hotBlood "ConsumeForCardDamage"
Assert-Contains "Hot Blood decrements on matching powered attack card damage" $hotBlood "PowerCmd\.Decrement\(this\)"

$getterChop = Read-RepoFile "src\Models\Cards\SGC_GetterChop.cs"
Assert-Contains "Getter Chop plunders shield twice" $getterChop "for\s*\(int\s+i\s*=\s*0;\s*i\s*<\s*2"
Assert-Contains "Getter Chop computes steal amount through block hooks" $getterChop "Hook\.ModifyBlock"
Assert-Contains "Getter Chop plunder gain is unpowered after hook calculation" $getterChop "CreatureCmd\.GainBlock\(Owner\.Creature,\s*stolenBlock,\s*ValueProp\.Unpowered"

$ancientDialogue = Read-RepoFile "src\Patches\ShinGetterAncientDialoguePatch.cs"
Assert-Contains "Ancient dialogues are rebuilt for Shin Getter" $ancientDialogue "CharacterDialogues\.Remove\(ShinGetterKey\)"
Assert-Contains "Ancient dialogues are repeating" $ancientDialogue "IsRepeating\s*=\s*true"
Assert-NotContains "Ancient dialogues no longer pin VisitIndex to encounter count" $ancientDialogue "VisitIndex\s*=\s*dialogueIndex"

$powerUi = Read-RepoFile "src\Patches\ShinGetterPowerUiPatch.cs"
Assert-Contains "Form power UI caches removed form icons" $powerUi "CacheRemovedFormIcon"
Assert-Contains "Form power UI adds previous icon overlay" $powerUi "TextureRect"
Assert-Contains "Form power UI fades old icon alpha" $powerUi 'TweenProperty\([^,]+,\s*"modulate:a",\s*0f'
Assert-Contains "Form power UI only transitions Shin Getter form powers" $powerUi "SGP_ShinGetterOne or SGP_ShinGetterTwo or SGP_ShinGetterThree or SGP_ShinForm"

$infiniteEvolution = Read-RepoFile "src\Models\Powers\SGP_InfiniteEvolution.cs"
Assert-Contains "Infinite Evolution resolves deck source card for victory gains" $infiniteEvolution "ResolveSourceCard"
Assert-Contains "Infinite Evolution applies victory strength immediately" $infiniteEvolution "PowerCmd\.Apply<StrengthPower>"
Assert-Contains "Infinite Evolution applies victory dexterity immediately" $infiniteEvolution "PowerCmd\.Apply<DexterityPower>"
Assert-Contains "Infinite Evolution still applies victory max hp immediately" $infiniteEvolution "CreatureCmd\.GainMaxHp"

$scene = Read-RepoFile "scenes\creature_visuals\shin_getter.tscn"
Assert-Contains "Getter One scale increased by 10 percent" $scene "name=""GetterOne""[\s\S]*scale = Vector2\(0\.693,\s*0\.693\)"
Assert-Contains "Getter Two scale increased by 10 percent" $scene "name=""GetterTwo""[\s\S]*scale = Vector2\(0\.66,\s*0\.66\)"
Assert-Contains "Getter Three scale increased by 10 percent" $scene "name=""GetterThree""[\s\S]*scale = Vector2\(0\.66,\s*0\.66\)"
Assert-Contains "Shin Dragon scale increased by 10 percent" $scene "name=""ShinDragon""[\s\S]*scale = Vector2\(0\.693,\s*0\.693\)"

$sequence = Read-RepoFile "src\Nodes\Combat\NShinGetterSpriteSequence.cs"
foreach ($dir in @(
    "getter_one_death",
    "getter_two_cast",
    "getter_two_block",
    "getter_two_dash",
    "getter_three_attack",
    "getter_three_dash",
    "getter_three_cast",
    "getter_three_block",
    "getter_three_death",
    "shin_getter_dragon_cast",
    "shin_getter_dragon_dash",
    "shin_getter_dragon_death"
)) {
    Assert-Contains "Sprite sequence references $dir" $sequence $dir
}
Assert-Contains "Sprite sequence has dash animation name" $sequence "DashAnimationName"
Assert-Contains "Sprite sequence has block animation name" $sequence "BlockAnimationName"
Assert-Contains "Sprite sequence has death animation name" $sequence "DeathAnimationName"

$stateMachine = Read-RepoFile "src\Nodes\Combat\NShinGetterSpriteAnimationStateMachine.cs"
Assert-Contains "Animation state machine maps Dash trigger" $stateMachine '"Dash"\s*=>\s*NShinGetterSpriteSequence\.DashAnimationName'
Assert-Contains "Animation state machine maps Hit trigger to block" $stateMachine '"Hit"\s*=>\s*NShinGetterSpriteSequence\.BlockAnimationName'
Assert-Contains "Animation state machine maps Block trigger" $stateMachine '"Block"\s*=>\s*NShinGetterSpriteSequence\.BlockAnimationName'
Assert-Contains "Animation state machine maps Dead trigger" $stateMachine '"Dead"\s*=>\s*NShinGetterSpriteSequence\.DeathAnimationName'

$animationPatch = Read-RepoFile "src\Patches\ShinGetterCreatureAnimationPatch.cs"
Assert-Contains "Creature animation patch forwards action triggers" $animationPatch 'IsShinGetterActionTrigger'
Assert-Contains "Creature animation patch forwards Dash" $animationPatch '"Dash"'
Assert-Contains "Creature animation patch forwards Hit" $animationPatch '"Hit"'
Assert-Contains "Creature animation patch forwards Dead" $animationPatch '"Dead"'

$cardBase = Read-RepoFile "src\Models\Cards\ShinGetterCardBase.cs"
Assert-Contains "Card animation has explicit dash class set" $cardBase "DashAnimationCards"
Assert-Contains "Card animation has explicit cast attack class set" $cardBase "CastAttackAnimationCards"
Assert-Contains "Card animation routes defensive skills to block animation" $cardBase "GetActionAnimationTrigger\(\)[\s\S]*Block"
Assert-Contains "Card animation routes dash attacks to Dash" $cardBase "DashAnimationCards\.Contains\(GetType\(\)\.Name\)[\s\S]*return ""Dash"""

$animationDirectories = @{
    "images\characters\shin_getter\forms\getter_one_death" = 48
    "images\characters\shin_getter\forms\getter_two_cast" = 32
    "images\characters\shin_getter\forms\getter_two_block" = 24
    "images\characters\shin_getter\forms\getter_two_dash" = 48
    "images\characters\shin_getter\forms\getter_three_attack" = 40
    "images\characters\shin_getter\forms\getter_three_dash" = 48
    "images\characters\shin_getter\forms\getter_three_cast" = 32
    "images\characters\shin_getter\forms\getter_three_block" = 24
    "images\characters\shin_getter\forms\getter_three_death" = 48
    "images\characters\shin_getter\forms\shin_getter_dragon_cast" = 32
    "images\characters\shin_getter\forms\shin_getter_dragon_dash" = 48
    "images\characters\shin_getter\forms\shin_getter_dragon_death" = 48
}

foreach ($entry in $animationDirectories.GetEnumerator()) {
    Assert-DirectoryFrameCount $entry.Key $entry.Value
}

if ($Failures.Count -gt 0) {
    Write-Host "FAILED v0.9.29 2026-07-08 reopened ticket checks:" -ForegroundColor Red
    foreach ($failure in $Failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "PASSED v0.9.29 2026-07-08 reopened ticket checks." -ForegroundColor Green
