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

function Assert-JsonKeys([string] $relativePath, [string[]] $keys) {
    $path = Join-Path $Root $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        $Failures.Add("Missing localization file: $relativePath")
        return
    }

    $json = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    $actualKeys = @($json.PSObject.Properties.Name)
    foreach ($key in $keys) {
        if ($actualKeys -notcontains $key) {
            $Failures.Add("$relativePath missing key $key")
        }
    }
}

$relicPool = Read-RepoFile "src\Models\RelicPools\ShinGetterRelicPool.cs"
Assert-Contains "Relic pool has an explicit weighting helper" $relicPool "WeightedShinGetterRelics"
Assert-Contains "Relic pool duplicates Shin Getter relics for weight 2" $relicPool "weight\s*:\s*2"
Assert-Contains "Relic pool keeps starter relic out of random weighting" $relicPool "Rarity\s*!=\s*RelicRarity\.Starter"
Assert-Contains "Relic pool keeps ancient relics out of random weighting" $relicPool "Rarity\s*!=\s*RelicRarity\.Ancient"

$potionPool = Read-RepoFile "src\Models\PotionPools\ShinGetterPotionPool.cs"
Assert-Contains "Potion pool has an explicit weighting helper" $potionPool "WeightedCustomPotions"
Assert-Contains "Potion pool duplicates custom potions for weight 2" $potionPool "weight\s*:\s*2"
Assert-NotContains "Potion pool no longer returns shared potions because PotionFactory adds them" $potionPool "SharedPotionPool"
Assert-Contains "Potion pool returns weighted custom potions from GenerateAllPotions" $potionPool "GenerateAllPotions\(\)[\s\S]*WeightedCustomPotions"

Assert-File "src\Patches\ShinGetterPotionFactoryWeightPatch.cs"
$potionFactoryPatch = Read-RepoFile "src\Patches\ShinGetterPotionFactoryWeightPatch.cs"
Assert-Contains "Potion factory weight patch targets random potion creation" $potionFactoryPatch "PotionFactory.*CreateRandomPotion"
Assert-Contains "Potion factory weight patch removes all copies after selection" $potionFactoryPatch "RemoveAll\(potion\s*=>\s*potion\.Id\s*==\s*item\.Id\)"

$ancientPatch = Read-RepoFile "src\Patches\ShinGetterAncientRewardPatch.cs"
Assert-Contains "Touch of Orobas setup is patched for Shin Getter" $ancientPatch "TouchOfOrobas.*SetupForPlayer"
Assert-Contains "Touch of Orobas obtain is patched for Shin Getter" $ancientPatch "TouchOfOrobas.*AfterObtained"
Assert-Contains "Getter Furnace can be found for Orobas replacement" $ancientPatch "FindGetterFurnace"
Assert-Contains "Getter Furnace replacement creates Emperor's Fragment" $ancientPatch "SGR_EmperorsFragment"
Assert-Contains "Getter Furnace replacement uses RelicCmd.Replace" $ancientPatch "RelicCmd\.Replace"

Assert-File "src\Models\Cards\SGC_HolyDragonRoar.cs"
$holyDragon = Read-RepoFile "src\Models\Cards\SGC_HolyDragonRoar.cs"
Assert-Contains "Holy Dragon Roar is a Shin Getter card" $holyDragon "class\s+SGC_HolyDragonRoar\s*:\s*ShinGetterCardBase"
Assert-Contains "Holy Dragon Roar is ancient" $holyDragon "CardRarity\.Ancient"
Assert-Contains "Holy Dragon Roar is currently a placeholder" $holyDragon "Placeholder"
Assert-Contains "Holy Dragon Roar is intentionally effectless" $holyDragon "Task\.CompletedTask"

$cardPool = Read-RepoFile "src\Models\CardPools\ShinGetterCardPool.cs"
Assert-Contains "Card pool includes Holy Dragon Roar" $cardPool "SGC_HolyDragonRoar"

Assert-File "src\Models\Events\SGE_GetterMandala.cs"
$mandala = Read-RepoFile "src\Models\Events\SGE_GetterMandala.cs"
Assert-Contains "Getter Mandala is an event model" $mandala "class\s+SGE_GetterMandala\s*:\s*EventModel"
Assert-Contains "Getter Mandala has initial option pool" $mandala "BuildOptionPool"
Assert-Contains "Getter Mandala samples three pool options" $mandala "Take\(3\)"
Assert-Contains "Getter Mandala always has ignore option" $mandala "IGNORE"
Assert-Contains "Getter Mandala adds Insect Virus on ignore" $mandala "SGC_InsectVirus"
Assert-Contains "Getter Mandala can replace Getter Furnace" $mandala "ReplaceGetterFurnace"
Assert-Contains "Getter Mandala can grant Shin Form" $mandala "SGC_ShinForm"
Assert-Contains "Getter Mandala applies Devolution enchantment" $mandala "SGE_Devolution"
Assert-Contains "Getter Mandala applies Adaptation enchantment" $mandala "SGE_Adaptation"
Assert-Contains "Getter Mandala can grant Holy Dragon Roar" $mandala "SGC_HolyDragonRoar"
Assert-Contains "Getter Mandala upgrades Getter cards by title" $mandala 'Contains\("盖塔"'
Assert-Contains "Getter Mandala uses deck enchant selection" $mandala "CardSelectCmd\.FromDeckForEnchantment"
Assert-Contains "Getter Mandala shows enchant VFX" $mandala "NCardEnchantVfx"

Assert-File "src\Patches\ShinGetterGetterMandalaPatch.cs"
$mandalaPatch = Read-RepoFile "src\Patches\ShinGetterGetterMandalaPatch.cs"
Assert-Contains "Getter Mandala patch targets ModifyNextEvent" $mandalaPatch "Hook.*ModifyNextEvent"
Assert-Contains "Getter Mandala appears in act 2" $mandalaPatch "MandalaActIndex\s*=\s*1"
Assert-Contains "Getter Mandala patch is limited to Shin Getter" $mandalaPatch "ShinGetter"
Assert-Contains "Getter Mandala patch checks visited event ids" $mandalaPatch "VisitedEventIds"
Assert-Contains "Getter Mandala patch returns the event" $mandalaPatch "ModelDb\.Event<SGE_GetterMandala>"

Assert-File "images\events\s_g_e_getter_mandala.png"
$resourceValidator = Read-RepoFile "tools\validate-mod-resources.gd"
Assert-Contains "Getter Mandala event image is covered by PCK validation" $resourceValidator "images/events/s_g_e_getter_mandala\.png"

$eventKeys = @(
    "S_G_E_GETTER_MANDALA.pages.INITIAL.description",
    "S_G_E_GETTER_MANDALA.pages.INITIAL.options.SOLAR_BATTLESHIP.title",
    "S_G_E_GETTER_MANDALA.pages.INITIAL.options.GETTER_G_FUSION.title",
    "S_G_E_GETTER_MANDALA.pages.INITIAL.options.PRIMAL_GETTER.title",
    "S_G_E_GETTER_MANDALA.pages.INITIAL.options.FIRST_EVOLUTION.title",
    "S_G_E_GETTER_MANDALA.pages.INITIAL.options.HOLY_DRAGON.title",
    "S_G_E_GETTER_MANDALA.pages.INITIAL.options.GUARDIAN_GOD.title",
    "S_G_E_GETTER_MANDALA.pages.INITIAL.options.IGNORE.title",
    "S_G_E_GETTER_MANDALA.pages.SOLAR_BATTLESHIP.description",
    "S_G_E_GETTER_MANDALA.pages.GETTER_G_FUSION.description",
    "S_G_E_GETTER_MANDALA.pages.PRIMAL_GETTER.description",
    "S_G_E_GETTER_MANDALA.pages.FIRST_EVOLUTION.description",
    "S_G_E_GETTER_MANDALA.pages.HOLY_DRAGON.description",
    "S_G_E_GETTER_MANDALA.pages.GUARDIAN_GOD.description",
    "S_G_E_GETTER_MANDALA.pages.IGNORE.description"
)

$cardKeys = @(
    "S_G_C_HOLY_DRAGON_ROAR.title",
    "S_G_C_HOLY_DRAGON_ROAR.description"
)

foreach ($lang in @("zhs", "eng", "jpn")) {
    Assert-JsonKeys "ShinGetterMod\localization\$lang\events.json" $eventKeys
    Assert-JsonKeys "ShinGetterMod\localization\$lang\cards.json" $cardKeys
}

$q14 = "E:\Obsidian\all-in-one\游戏\杀戮尖塔2\问答\14-V0.9.26-商店流龙马静态图与PCK压缩验证清单.md"
if (Test-Path -LiteralPath $q14) {
    $q14Text = Get-Content -LiteralPath $q14 -Raw -Encoding UTF8
    Assert-Contains "Q14 records 2026-07-07 handling" $q14Text "## 2026-07-07 处理记录"
    Assert-Contains "Q14 records recent untested work list" $q14Text "最近20小时内未游戏内测试清单"
    Assert-Contains "Q14 lists Getter Mandala as untested" $q14Text "盖塔曼陀罗"
    Assert-Contains "Q14 lists enchantments as untested" $q14Text "适应.*退化|退化.*适应"
    Assert-Contains "Q14 lists English/Japanese localization as untested" $q14Text "英语/日语本地化"
} else {
    $Failures.Add("Missing Obsidian Q14 note")
}

if ($Failures.Count -gt 0) {
    Write-Host "FAILED v0.9.28 2026-07-07 feedback checks:" -ForegroundColor Red
    foreach ($failure in $Failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "PASSED v0.9.28 2026-07-07 feedback checks." -ForegroundColor Green
