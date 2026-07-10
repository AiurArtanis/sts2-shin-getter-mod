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

$tomahawkCard = Read-RepoFile "src\Models\Cards\SGC_GetterTomahawk.cs"
$tomahawkPower = Read-RepoFile "src\Models\Powers\SGP_Tomahawk.cs"
Assert-Contains "Tomahawk power stores replay card instances" $tomahawkPower 'List<CardModel>'
Assert-Contains "Tomahawk power exposes a replay queue method" $tomahawkPower 'QueueReplay\(CardModel\s+card\)'
Assert-NotContains "Tomahawk replay queue preserves repeated plays of the same card" $tomahawkPower 'Cards\.Contains\(card\)'
Assert-Contains "Tomahawk replays the stored card instance" $tomahawkPower 'CardCmd\.AutoPlay\([\s\S]*card,\s*null'
Assert-NotContains "Tomahawk no longer creates a generated copy" $tomahawkPower 'CreateCard<Models\.Cards\.SGC_GetterTomahawk>'
Assert-Contains "Tomahawk queues the card that was manually played" $tomahawkCard 'tomahawkPower\?\.QueueReplay\(this\)'
Assert-Contains "Tomahawk still schedules only manual plays" $tomahawkCard '!cardPlay\.IsAutoPlay[\s\S]*PowerCmd\.Apply<SGP_Tomahawk>'
Assert-NotContains "Tomahawk autoplay no longer suppresses the Getter One reward" $tomahawkCard '!cardPlay\.IsAutoPlay\s*&&\s*HasForm'

$insight = Read-RepoFile "src\Models\Cards\SGC_Insight.cs"
Assert-Contains "Insight uses Dexterity as its upgrade-highlight dynamic value" $insight 'new PowerVar<DexterityPower>\(1m\)'
Assert-Contains "Insight applies its delayed power using the Dexterity dynamic value" $insight 'PowerCmd\.Apply<SGP_Insight>[\s\S]*DynamicVars\.Dexterity\.BaseValue'
Assert-Contains "Insight upgrades the Dexterity dynamic value" $insight 'DynamicVars\.Dexterity\.UpgradeValueBy\(1m\)'

$blueprint = Read-RepoFile "src\Models\Cards\SGC_SaotomeBlueprint.cs"
Assert-Contains "Blueprint uses Evolution as its upgrade-highlight dynamic value" $blueprint 'new PowerVar<SGP_Evolution>\(1m\)'
Assert-Contains "Blueprint applies its delayed power using the Evolution dynamic value" $blueprint 'PowerCmd\.Apply<SGP_Blueprint>[\s\S]*DynamicVars\["SGP_Evolution"\]\.BaseValue'
Assert-Contains "Blueprint upgrades the Evolution dynamic value" $blueprint 'DynamicVars\["SGP_Evolution"\]\.UpgradeValueBy\(1m\)'

$elbow = Read-RepoFile "src\Models\Cards\SGC_GetterElbow.cs"
Assert-Contains "Getter Elbow costs one energy" $elbow ':\s*base\(1,\s*CardType\.Attack'

$darkCape = Read-RepoFile "src\Models\Cards\SGC_DarkCape.cs"
Assert-Contains "Dark Cape grants three Airborne in Getter One" $darkCape 'PowerCmd\.Apply<SGP_Airborne>\([^;]*3m'

$getterFlash = Read-RepoFile "src\Models\Cards\SGC_GetterFlash.cs"
Assert-Contains "Getter Flash grants three bonus Vigor in Getter One" $getterFlash 'PowerCmd\.Apply<VigorPower>\([^;]*3m'
Assert-Contains "Getter Flash grants three Airborne in Getter One" $getterFlash 'PowerCmd\.Apply<SGP_Airborne>\([^;]*3m'

$cardBase = Read-RepoFile "src\Models\Cards\ShinGetterCardBase.cs"
Assert-Contains "Getter Flash registers the Airborne hover term" $cardBase '\["SGC_GetterFlash"\]\s*=\s*new\[\]\s*\{\s*"活力",\s*"腾空",\s*"一号机"\s*\}'
Assert-Contains "Bold Plan registers Radiation, Ki, and Energy hover terms" $cardBase '\["SGC_BoldPlan"\]\s*=\s*new\[\]\s*\{\s*"辐射",\s*"气力",\s*"能量"\s*\}'

$boldPlan = Read-RepoFile "src\Models\Cards\SGC_BoldPlan.cs"
Assert-Contains "Bold Plan gains Radiation" $boldPlan 'PowerCmd\.Apply<SGP_Radiation>\([^;]*x'
Assert-Contains "Bold Plan gains Ki" $boldPlan 'PowerCmd\.Apply<SGP_Ki>\([^;]*x'
Assert-Contains "Bold Plan gains X energy" $boldPlan 'PlayerCmd\.GainEnergy\(x,\s*Owner\)'
Assert-Contains "Bold Plan draws X cards" $boldPlan 'CardPileCmd\.Draw\(choiceContext,\s*x,\s*Owner\)'
Assert-NotContains "Bold Plan no longer loses Ki" $boldPlan 'ModifyAmount\([^;]*-x'

Assert-JsonPropertyEquals "ShinGetterMod\localization\zhs\cards.json" "S_G_C_BACKUP_PLAN.description" "消耗任意张手牌，每消耗一种类型的卡牌抽1张牌。`n[white]二号机[/white]：获得{Energy:energyIcons()}。"
Assert-JsonPropertyEquals "ShinGetterMod\localization\eng\cards.json" "S_G_C_BACKUP_PLAN.description" "Exhaust any number of cards in your hand. For each card type Exhausted, draw 1 card.`n[white]Shin Getter 2[/white]: Gain {Energy:energyIcons()}."
Assert-JsonPropertyEquals "ShinGetterMod\localization\jpn\cards.json" "S_G_C_BACKUP_PLAN.description" "手札を好きな枚数廃棄する。廃棄したカードタイプ1種類につきカードを1枚引く。`n[white]真ゲッター2[/white]：{Energy:energyIcons()}を得る。"

Assert-JsonPropertyEquals "ShinGetterMod\localization\zhs\cards.json" "S_G_C_INSIGHT.description" "若回合开始时受到攻击意图，本回合获得{DexterityPower:diff()}点[gold]敏捷[/gold]。`n[red]一号机[/red]：获得{StrengthPower:diff()}点[gold]力量[/gold]。`n[white]二号机[/white]：获得{Energy:energyIcons()}。`n[yellow]三号机[/yellow]：获得{ThornsPower:diff()}点[gold]荆棘[/gold]。"
Assert-JsonPropertyEquals "ShinGetterMod\localization\eng\cards.json" "S_G_C_INSIGHT.description" "At the start of your turn, if an enemy intends to attack, gain {DexterityPower:diff()} [gold]Dexterity[/gold] this turn.`n[red]Shin Getter 1[/red]: Gain {StrengthPower:diff()} [gold]Strength[/gold].`n[white]Shin Getter 2[/white]: Gain {Energy:energyIcons()}.`n[yellow]Shin Getter 3[/yellow]: Gain {ThornsPower:diff()} [gold]Thorns[/gold]."
Assert-JsonPropertyEquals "ShinGetterMod\localization\jpn\cards.json" "S_G_C_INSIGHT.description" "ターン開始時、敵がアタックを意図しているなら、このターン[gold]敏捷[/gold]を{DexterityPower:diff()}得る。`n[red]真ゲッター1[/red]：[gold]筋力[/gold]を{StrengthPower:diff()}得る。`n[white]真ゲッター2[/white]：{Energy:energyIcons()}を得る。`n[yellow]真ゲッター3[/yellow]：[gold]トゲ[/gold]を{ThornsPower:diff()}得る。"
Assert-JsonPropertyEquals "ShinGetterMod\localization\zhs\cards.json" "S_G_C_SAOTOME_BLUEPRINT.description" "失去生命时，获得{SGP_Evolution:diff()}层[getter_ray]进化[/getter_ray]。"
Assert-JsonPropertyEquals "ShinGetterMod\localization\eng\cards.json" "S_G_C_SAOTOME_BLUEPRINT.description" "Whenever you lose HP, gain {SGP_Evolution:diff()} [getter_ray]Evolution[/getter_ray]."
Assert-JsonPropertyEquals "ShinGetterMod\localization\jpn\cards.json" "S_G_C_SAOTOME_BLUEPRINT.description" "HPを失う時、[getter_ray]進化[/getter_ray]を{SGP_Evolution:diff()}得る。"

Assert-JsonPropertyEquals "ShinGetterMod\localization\zhs\cards.json" "S_G_C_DARK_CAPE.description" "获得{Block:diff()}点[gold]格挡[/gold]。本回合每当[gold]格挡[/gold]完全抵挡伤害时，对所有敌人造成{Damage:diff()}点伤害。`n[red]一号机[/red]：获得3层[gold]腾空[/gold]。"
Assert-JsonPropertyEquals "ShinGetterMod\localization\eng\cards.json" "S_G_C_DARK_CAPE.description" "Gain {Block:diff()} [gold]Block[/gold]. This turn, whenever [gold]Block[/gold] fully blocks damage, deal {Damage:diff()} damage to ALL enemies.`n[red]Shin Getter 1[/red]: Gain 3 [gold]Airborne[/gold]."
Assert-JsonPropertyEquals "ShinGetterMod\localization\jpn\cards.json" "S_G_C_DARK_CAPE.description" "{Block:diff()}[gold]ブロック[/gold]を得る。このターン、[gold]ブロック[/gold]でダメージを完全に防ぐたび、敵全体に{Damage:diff()}ダメージを与える。`n[red]真ゲッター1[/red]：[gold]空中[/gold]を3得る。"

Assert-JsonPropertyEquals "ShinGetterMod\localization\zhs\cards.json" "S_G_C_GETTER_FLASH.description" "造成{Damage:diff()}点伤害，获得等同于造成伤害的[gold]活力[/gold]。`n[red]一号机[/red]：获得3点[gold]活力[/gold]和3层[gold]腾空[/gold]。"
Assert-JsonPropertyEquals "ShinGetterMod\localization\eng\cards.json" "S_G_C_GETTER_FLASH.description" "Deal {Damage:diff()} damage. Gain [gold]Vigor[/gold] equal to the damage dealt.`n[red]Shin Getter 1[/red]: Gain 3 [gold]Vigor[/gold] and 3 [gold]Airborne[/gold]."
Assert-JsonPropertyEquals "ShinGetterMod\localization\jpn\cards.json" "S_G_C_GETTER_FLASH.description" "{Damage:diff()}ダメージを与える。与えたダメージに等しい[gold]活力[/gold]を得る。`n[red]真ゲッター1[/red]：[gold]活力[/gold]を3、[gold]空中[/gold]を3得る。"

Assert-JsonPropertyEquals "ShinGetterMod\localization\zhs\cards.json" "S_G_C_BOLD_PLAN.description" "获得X{IfUpgraded:show:+1}层[gold]辐射[/gold]、X{IfUpgraded:show:+1}点[gold]气力[/gold]、X{IfUpgraded:show:+1}{Energy:energyIcons()}，抽X{IfUpgraded:show:+1}张牌。"
Assert-JsonPropertyEquals "ShinGetterMod\localization\eng\cards.json" "S_G_C_BOLD_PLAN.description" "Gain X{IfUpgraded:show:+1} [gold]Radiation[/gold], X{IfUpgraded:show:+1} [gold]Ki[/gold], and X{IfUpgraded:show:+1}{Energy:energyIcons()}. Draw X{IfUpgraded:show:+1} card(s)."
Assert-JsonPropertyEquals "ShinGetterMod\localization\jpn\cards.json" "S_G_C_BOLD_PLAN.description" "[gold]放射線[/gold]をX{IfUpgraded:show:+1}、[gold]気力[/gold]をX{IfUpgraded:show:+1}、X{IfUpgraded:show:+1}{Energy:energyIcons()}を得て、カードをX{IfUpgraded:show:+1}枚引く。"

if ($Failures.Count -gt 0) {
    Write-Host "FAILED v0.9.31 2026-07-09 second playtest checks:" -ForegroundColor Red
    foreach ($failure in $Failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "PASSED v0.9.31 2026-07-09 second playtest checks." -ForegroundColor Green
