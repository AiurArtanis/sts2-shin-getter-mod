$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Failures = New-Object System.Collections.Generic.List[string]

function Read-RepoFile([string] $relativePath) {
    return Get-Content -LiteralPath (Join-Path $Root $relativePath) -Raw
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

$ki = Read-RepoFile "src\Models\Powers\SGP_Ki.cs"
Assert-Contains "SGP_Ki uses final hp-loss hook" $ki "ModifyHpLostAfterOstyLate"
Assert-NotContains "SGP_Ki no longer reduces pre-block damage" $ki "ModifyDamageAdditive"
Assert-Contains "SGP_Ki only drops after actual unblocked hp loss" $ki "result\.UnblockedDamage\s*<=\s*0"
Assert-Contains "Emperor's Fragment prevents Ki loss on damage" $ki "GetRelic<SGR_EmperorsFragment>\(\)\s*!=\s*null"

$chainPatch = Read-RepoFile "src\Patches\VigorPowerSetAmountPatch.cs"
Assert-Contains "Chain reaction triggers once per vigor-loss event" $chainPatch "int\s+gain\s*=\s*chain\.Amount\s*;"
Assert-NotContains "Chain reaction no longer multiplies by lost vigor amount" $chainPatch "delta\s*\*\s*chain\.Amount"

$infiniteCard = Read-RepoFile "src\Models\Cards\SGC_InfiniteEvolution.cs"
Assert-Contains "Infinite Evolution costs 3" $infiniteCard "base\(3,\s*CardType\.Skill"
Assert-Contains "Infinite Evolution exhausts" $infiniteCard "CardKeyword\.Exhaust"
Assert-Contains "Infinite Evolution saves strength gain" $infiniteCard "PermanentStrengthGain"
Assert-Contains "Infinite Evolution saves dexterity gain" $infiniteCard "PermanentDexterityGain"
Assert-Contains "Infinite Evolution saves max hp gain" $infiniteCard "PermanentMaxHpGain"
Assert-Contains "Infinite Evolution applies saved strength at combat start" $infiniteCard "BeforeCombatStart"

$infinitePower = Read-RepoFile "src\Models\Powers\SGP_InfiniteEvolution.cs"
Assert-Contains "Infinite Evolution power is unique" $infinitePower "PowerStackType\.Single"
Assert-Contains "Infinite Evolution records victory gain on source card" $infinitePower "RecordVictoryGain"

$enableCard = Read-RepoFile "src\Models\Cards\SGC_Enable.cs"
Assert-Contains "Enable costs 4" $enableCard "base\(4,\s*CardType\.Power"
Assert-Contains "Enable upgraded spirit requirement is 4" $enableCard "IsUpgraded\s*\?\s*4\s*:\s*6"
Assert-Contains "Enable flashes its status icon on play" $enableCard "enablePower\?\.FlashOnPlay"

$steelSpirit = Read-RepoFile "src\Models\Cards\SGC_SteelSpirit.cs"
Assert-Contains "Steel Spirit autoplays selected spirit command" $steelSpirit "CardCmd\.AutoPlay"
Assert-Contains "Steel Spirit searches combat piles before generation" $steelSpirit "FindSpiritCommandInCombatPiles"
Assert-Contains "Steel Spirit upgraded path generates a spirit command" $steelSpirit "CreateRandomSpiritCommand"

$cardsJson = Read-RepoFile "ShinGetterMod\localization\zhs\cards.json"
Assert-Contains "Desperation colors Getter One label" $cardsJson "\[red\]一号机\[/red\]"
Assert-Contains "Desperation colors Getter Two label" $cardsJson "\[white\]二号机\[/white\]"
Assert-Contains "Desperation colors Getter Three label" $cardsJson "\[yellow\]三号机\[/yellow\]"
Assert-Contains "Steel Spirit description mentions autoplay" $cardsJson "随机打出1张"
Assert-Contains "Infinite Evolution description mentions exhaust-compatible cost" $cardsJson "战斗胜利后"

$powersJson = Read-RepoFile "ShinGetterMod\localization\zhs\powers.json"
Assert-NotContains "Evolution description no longer says permanent" $powersJson "S_G_P_EVOLUTION\.description[^`n]*永久"
Assert-Contains "Chain Reaction description says once per loss" $powersJson "每次失去"

$tipsJson = Read-RepoFile "ShinGetterMod\localization\zhs\static_hover_tips.json"
Assert-Contains "Spirit command hover tip embeds ki icon" $tipsJson "s_g_p_ki"

$resourceValidation = Read-RepoFile "tools\validate-mod-resources.gd"
Assert-Contains "Evolution engine atlas icon is validated" $resourceValidation "s_g_p_evolution_engine\.tres"
Assert-Contains "Evolution engine large icon is validated" $resourceValidation "s_g_p_evolution_engine\.png"

$potionAlias = Read-RepoFile "src\Patches\PotionConsoleAliasPatch.cs"
Assert-Contains "Potion console accepts SGR_ class aliases" $potionAlias "PotionConsoleCmd"
Assert-Contains "Potion console alias maps to S_G_R entries" $potionAlias "S_G_R_"

$colorfulPatch = Read-RepoFile "src\Patches\ShinGetterColorfulPhilosophersPatch.cs"
Assert-Contains "Colorful philosophers patch targets the original event" $colorfulPatch "ColorfulPhilosophers"
Assert-Contains "Colorful philosophers can add Shin Getter pool" $colorfulPatch "ShinGetterCardPool"
Assert-Contains "Colorful philosophers keeps at most three options" $colorfulPatch "Mathf\.Min\(3"

$eventsJson = Read-RepoFile "ShinGetterMod\localization\zhs\events.json"
Assert-Contains "Colorful philosophers Shin Getter option has cyan title" $eventsJson "COLORFUL_PHILOSOPHERS\.pages\.INITIAL\.options\.SHIN_GETTER\.title"
Assert-Contains "Colorful philosophers Shin Getter option grants Shin Getter cards" $eventsJson "真盖塔的卡牌"

$ancientProceedPatch = Read-RepoFile "src\Patches\ShinGetterAncientProceedPatch.cs"
Assert-Contains "Shin Getter ancients stay interactive when options are empty" $ancientProceedPatch "GenerateInitialOptionsWrapper"
Assert-Contains "Shin Getter ancient fallback uses proceed option" $ancientProceedPatch "NEventRoom\.Proceed"
Assert-Contains "Shin Getter ancient fallback is limited to Shin Getter" $ancientProceedPatch "Character\s+is\s+not\s+ShinGetter"

$cardFramePatch = Read-RepoFile "src\Patches\ShinGetterCardFramePatch.cs"
Assert-Contains "Card frame transition stores target form" $cardFramePatch "PendingTransitionForm"
Assert-Contains "Card frame transition can target Shin Dragon directly" $cardFramePatch "BeginFormTransitionToShinDragon"

$cardBase = Read-RepoFile "src\Models\Cards\ShinGetterCardBase.cs"
Assert-Contains "Shin Getter cards enqueue action animation by type" $cardBase "OnEnqueuePlayVfx"
Assert-Contains "Rare and ancient attacks request heavy animation" $cardBase "HeavyAttack"
Assert-Contains "Shin Form card requests transform vfx" $cardBase "PlayShinFormTransformVfx"

$staticVisuals = Read-RepoFile "src\Nodes\Combat\NShinGetterStaticVisuals.cs"
Assert-Contains "Static visuals can resolve all visible forms" $staticVisuals "TryPlayVisibleFormActionAnimation"
Assert-Contains "Static visuals fall back heavy attack to attack" $staticVisuals "HeavyAttack"

$spriteSequence = Read-RepoFile "src\Nodes\Combat\NShinGetterSpriteSequence.cs"
Assert-Contains "Sprite sequence declares heavy attack animation" $spriteSequence "HeavyAttackAnimationName"
Assert-Contains "Sprite sequence accepts exported png remap files" $spriteSequence "\.png\.remap"
Assert-Contains "Sprite sequence accepts exported png import files" $spriteSequence "\.png\.import"
Assert-Contains "Sprite sequence normalizes remap filenames before loading" $spriteSequence "NormalizeFrameResourceFile"

$merchantVisuals = Read-RepoFile "src\Nodes\Screens\Shops\ShinGetterMerchantVisuals.cs"
Assert-Contains "Merchant Ryoma scale reduced" $merchantVisuals "SpriteScale\s*=\s*0\.376f"
Assert-Contains "Merchant Ryoma is moved downward" $merchantVisuals "SpriteFootYOffset"

$goodCitizen = Read-RepoFile "src\Models\Relics\SGR_GoodCitizenCard.cs"
Assert-Contains "Good Citizen Card tracks free shop floor" $goodCitizen "LastFreeFloor\s*==\s*Owner\.RunState\.TotalFloor"
Assert-Contains "Good Citizen Card resets disabled status after floor changes" $goodCitizen "AfterRoomEntered[\s\S]*Status\s*=\s*RelicStatus\.Normal"

$potionPool = Read-RepoFile "src\Models\PotionPools\ShinGetterPotionPool.cs"
Assert-Contains "Shin Getter potion pool includes Transform Potion" $potionPool "SGR_TransformPotion"
Assert-Contains "Shin Getter potion pool includes Kusuha Juice" $potionPool "SGR_KusuhaJuice"
Assert-Contains "Shin Getter potion pool includes Getter Cold Brew" $potionPool "SGR_GetterColdBrew"

$shinGetterScene = Read-RepoFile "scenes\creature_visuals\shin_getter.tscn"
Assert-Contains "Getter One position adjusted right" $shinGetterScene "(?s)GetterOne.*?position = Vector2\(38, -176\)"
Assert-Contains "Getter Two position adjusted right" $shinGetterScene "(?s)GetterTwo.*?position = Vector2\(34, -176\)"
Assert-Contains "Getter Three position adjusted right" $shinGetterScene "(?s)GetterThree.*?position = Vector2\(22, -176\)"
Assert-Contains "Shin Dragon slightly enlarged" $shinGetterScene "(?s)ShinDragon.*?scale = Vector2\(0\.63, 0\.63\)"

$tomahawkPower = Read-RepoFile "src\Models\Powers\SGP_Tomahawk.cs"
Assert-Contains "Getter Tomahawk removes itself after next-turn autoplay" $tomahawkPower "PowerCmd\.Remove\(this\)"

$getterTomahawk = Read-RepoFile "src\Models\Cards\SGC_GetterTomahawk.cs"
Assert-Contains "Getter Tomahawk only queues delayed axes on manual play" $getterTomahawk "!cardPlay\.IsAutoPlay"

$getterClaw = Read-RepoFile "src\Models\Cards\SGC_GetterClaw.cs"
Assert-Contains "Getter Claw does not return itself when exhausted" $getterClaw "card\s*==\s*this"
Assert-Contains "Getter Claw ignores ethereal exhaust" $getterClaw "causedByEthereal"
Assert-Contains "Getter Claw only returns from draw or discard pile" $getterClaw "PileType\.Draw[\s\S]*PileType\.Discard"

$getterMissile = Read-RepoFile "src\Models\Cards\SGC_GetterMissile.cs"
Assert-Contains "Getter Missile stops when combat is ending" $getterMissile "CombatManager\.Instance\.IsOverOrEnding"
Assert-Contains "Getter Missile checks for living enemies between shots" $getterMissile "HasLivingEnemyTargets"

$grapplePower = Read-RepoFile "src\Models\Powers\SGP_Grapple.cs"
Assert-Contains "Grapple reduces multi-hit repeat count" $grapplePower "ModifyAttackHitCount"

$multiAttackPatch = Read-RepoFile "src\Patches\ShinGetterMultiAttackIntentPatch.cs"
Assert-Contains "Multi-attack intent label uses adjusted repeats" $multiAttackPatch "GetAdjustedRepeats"

$getterChop = Read-RepoFile "src\Models\Cards\SGC_GetterChop.cs"
Assert-Contains "Getter Chop plunders shield before damage" $getterChop "PlunderShield\(cardPlay\)[\s\S]*DamageCmd\.Attack"

$getterFlash = Read-RepoFile "src\Models\Cards\SGC_GetterFlash.cs"
Assert-Contains "Getter Flash grants Vigor from actual unblocked damage" $getterFlash "UnblockedDamage"

$architectPatch = Read-RepoFile "src\Patches\ShinGetterArchitectAttackPatch.cs"
Assert-Contains "Architect ending includes Getter Beam VFX" $architectPatch "GetterBeam"
Assert-Contains "Architect ending includes Tornado Drill VFX" $architectPatch "TornadoDrill"
Assert-Contains "Architect ending includes Getter Missile VFX" $architectPatch "GetterMissile"

if ($Failures.Count -gt 0) {
    Write-Host "Feedback checks failed:" -ForegroundColor Red
    foreach ($failure in $Failures) {
        Write-Host " - $failure"
    }
    exit 1
}

Write-Host "Feedback checks passed." -ForegroundColor Green
