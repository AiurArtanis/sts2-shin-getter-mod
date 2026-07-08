$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$vaultRoot = 'E:\Obsidian\all-in-one'
$failures = [System.Collections.Generic.List[string]]::new()

function Read-RepoFile([string]$relativePath) {
    $path = Join-Path $root $relativePath
    if (!(Test-Path -LiteralPath $path)) {
        return $null
    }

    return Get-Content -Raw -Encoding UTF8 -LiteralPath $path
}

function Read-VaultFile([string]$relativePath) {
    $path = Join-Path $vaultRoot $relativePath
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

function Require-VaultPattern([string]$name, [string]$relativePath, [string]$pattern) {
    $text = Read-VaultFile $relativePath
    if ($null -eq $text -or $text -notmatch $pattern) {
        $failures.Add($name)
    }
}

function Require-JsonMinimumVersion([string]$name, [string]$relativePath, [string]$minimumVersion) {
    $text = Read-RepoFile $relativePath
    if ($null -eq $text) {
        $failures.Add($name)
        return
    }

    $json = $text | ConvertFrom-Json
    $actual = [version](($json.version -as [string]).TrimStart('v'))
    $minimum = [version]$minimumVersion.TrimStart('v')
    if ($actual -lt $minimum) {
        $failures.Add("$name (found '$($json.version)')")
    }
}

function Require-StrikeTagged([string]$relativePath) {
    $text = Read-RepoFile $relativePath
    if ($null -eq $text -or $text -notmatch 'CardTag\.Strike') {
        $failures.Add("Strike-tagged card missing CardTag.Strike: $relativePath")
    }
}

Require-JsonMinimumVersion 'Root manifest version is at least v0.9.8' 'ShinGetterMod.json' 'v0.9.8'
Require-JsonMinimumVersion 'Build manifest version is at least v0.9.8' 'build\ShinGetterMod.json' 'v0.9.8'

Require-Pattern 'Relic console alias patch exists' 'src\Patches\RelicConsoleAliasPatch.cs' 'HarmonyPatch\(typeof\(RelicConsoleCmd\),\s*nameof\(RelicConsoleCmd\.Process\)\)'
Require-Pattern 'Relic console alias accepts SGR class names' 'src\Patches\RelicConsoleAliasPatch.cs' 'ClassPrefix\s*=\s*"SGR_"'
Require-Pattern 'Relic console alias also handles relic add/remove syntax' 'src\Patches\RelicConsoleAliasPatch.cs' 'args\[1\].StartsWith\(ClassPrefix'
Require-Pattern 'Relic console alias converts BattleInstinct to S_G_R_BATTLE_INSTINCT' 'src\Patches\RelicConsoleAliasPatch.cs' 'S_G_R_'

Require-Pattern 'Frame patch uses non-solid feedback tint strength' 'src\Patches\ShinGetterCardFramePatch.cs' 'FrameTintStrength\s*=\s*0\.82f'
Require-Pattern 'Frame patch uses non-solid feedback base tint strength' 'src\Patches\ShinGetterCardFramePatch.cs' 'FrameBaseTintStrength\s*=\s*0\.46f'
Require-Pattern 'Frame patch uses feedback Getter 2 silver border' 'src\Patches\ShinGetterCardFramePatch.cs' 'GetterTwoSilverKey\s*=\s*"D9E4DE"'
Require-Pattern 'Frame patch uses feedback Getter 2 gray middle tint' 'src\Patches\ShinGetterCardFramePatch.cs' 'GetterTwoSilverBaseKey\s*=\s*"5A6463"'
Require-Pattern 'Frame patch uses stronger silver border only for Getter 2' 'src\Patches\ShinGetterCardFramePatch.cs' 'FrameSilverTintStrength'
Require-Pattern 'Frame material still keeps export tint disabled' 'materials\cards\frames\card_frame_shin_getter_mat.tres' 'shader_parameter/tint_strength = 0(?:\.0)?'

Require-Pattern 'Dive Strike uses calculated damage preview' 'src\Models\Cards\SGC_DiveStrike.cs' 'CalculatedDamageVar\(ValueProp\.Move\)\.WithMultiplier\(GetAirborneMultiplier\)'
Require-Pattern 'Dive Strike doubles preview when Airborne is present' 'src\Models\Cards\SGC_DiveStrike.cs' 'GetPower<SGP_Airborne>\(\)[\s\S]*\?\s*1m\s*:\s*0m'
Require-Pattern 'Dive Strike deals CalculatedDamage' 'src\Models\Cards\SGC_DiveStrike.cs' 'DamageCmd\.Attack\(DynamicVars\.CalculatedDamage\)'

Require-Pattern 'Transform aborts while sealed before removing old form' 'src\Models\Cards\ShinGetterCardBase.cs' 'GetPower<SGP_Seal>\(\)\s+is\s+\{\s*\}\s+seal[\s\S]*seal\.FlashBlockedTransform\(\);[\s\S]*return;'
Require-Pattern 'Shin Form aborts while sealed before removing old form' 'src\Models\Cards\SGC_ShinForm.cs' 'GetPower<SGP_Seal>\(\)\s+is\s+\{\s*\}\s+seal[\s\S]*seal\.FlashBlockedTransform\(\);[\s\S]*return;'
Require-Pattern 'Seal exposes public blocked-transform flash wrapper' 'src\Models\Powers\SGP_Seal.cs' 'public\s+void\s+FlashBlockedTransform\(\)\s*=>\s*Flash\(\);'
Require-Pattern 'Seal allows brand-new visible positive powers' 'src\Models\Powers\SGP_Seal.cs' 'amount\s*>\s*0m[\s\S]*target\.GetPower\(canonicalPower\.Id\)\s*==\s*null'
Require-Pattern 'Seal still blocks existing visible amount changes' 'src\Models\Powers\SGP_Seal.cs' 'canonicalPower\.IsVisible[\s\S]*modifiedAmount\s*=\s*0m'
Require-AbsentPattern 'Awakened Soul no longer decrements internal remaining count' 'src\Models\Powers\SGP_AwakenedSoul.cs' 'remaining--'

Require-Pattern 'Getter Claw ignores ethereal exhaust callbacks' 'src\Models\Cards\SGC_GetterClaw.cs' 'causedByEthereal'
Require-Pattern 'Getter Claw does not return to hand from ethereal exhaust' 'src\Models\Cards\SGC_GetterClaw.cs' 'causedByEthereal\s*\|\|'

Require-Pattern 'Getter Rush localization mentions Getter 3 reward' 'ShinGetterMod\localization\zhs\cards.json' 'S_G_C_GETTER_RUSH\.description[\s\S]*\[yellow\]三号机\[/yellow\][\s\S]*覆甲'
Require-Pattern 'Getter Rush contextual hover terms include Getter 3 Plating' 'src\Models\Cards\ShinGetterCardBase.cs' 'SGC_GetterRush"\]\s*=\s*new\[\]\s*\{[^}]*"易伤"[^}]*"覆甲"[^}]*"三号机"'

Get-ChildItem -LiteralPath (Join-Path $root 'src\Models\Cards') -Filter '*Strike.cs' |
    ForEach-Object {
        $relative = 'src\Models\Cards\' + $_.Name
        Require-StrikeTagged $relative
    }

Require-Pattern 'Getter Will is Exhaust by default' 'src\Models\Cards\SGC_GetterWill.cs' 'CanonicalKeywords\s*=>\s*new\[\]\s*\{\s*CardKeyword\.Exhaust\s*\}'
Require-Pattern 'Getter Will upgrade removes Exhaust' 'src\Models\Cards\SGC_GetterWill.cs' 'RemoveKeyword\(CardKeyword\.Exhaust\)'
Require-AbsentPattern 'Getter Will upgrade no longer adds Innate' 'src\Models\Cards\SGC_GetterWill.cs' 'AddKeyword\(CardKeyword\.Innate\)'
Require-Pattern 'Dark Cape block lowered to 9' 'src\Models\Cards\SGC_DarkCape.cs' 'new\s+BlockVar\(9m'
Require-Pattern 'Dark Cape upgrades block from 9 to 11' 'src\Models\Cards\SGC_DarkCape.cs' 'DynamicVars\.Block\.UpgradeValueBy\(2m\)'
Require-Pattern 'Seize Future block lowered to 6' 'src\Models\Cards\SGC_SeizeFuture.cs' 'new\s+BlockVar\(6m'
Require-Pattern 'Seize Future upgrades block from 6 to 8' 'src\Models\Cards\SGC_SeizeFuture.cs' 'DynamicVars\.Block\.UpgradeValueBy\(2m\)'
Require-Pattern 'Spirit requires 3 spirit' 'src\Models\Cards\SGC_Spirit.cs' 'SpiritRequirement\s*=>\s*3'

Require-Pattern 'Awakened Soul grants 8 vigor' 'src\Models\Cards\SGC_AwakenedSoul.cs' 'new\s+PowerVar<SGP_AwakenedSoul>\(8m\)'
Require-Pattern 'Awakened Soul upgrades vigor from 8 to 12' 'src\Models\Cards\SGC_AwakenedSoul.cs' 'UpgradeValueBy\(4m\)'
Require-Pattern 'Awakened Soul power grants Vigor at turn start' 'src\Models\Powers\SGP_AwakenedSoul.cs' 'AfterEnergyReset\(Player player\)[\s\S]*PowerCmd\.Apply<VigorPower>'
Require-AbsentPattern 'Awakened Soul no longer modifies attack damage' 'src\Models\Powers\SGP_AwakenedSoul.cs' 'ModifyDamageMultiplicative'
Require-Pattern 'Awakened Soul localization describes start-of-turn vigor' 'ShinGetterMod\localization\zhs\cards.json' 'S_G_C_AWAKENED_SOUL\.description[\s\S]*回合开始获得\{SGP_AwakenedSoul:diff\(\)\}点\[gold\]活力\[/gold\]'
Require-Pattern 'Awakened Soul power localization describes vigor gain' 'ShinGetterMod\localization\zhs\powers.json' 'S_G_P_AWAKENED_SOUL\.description[\s\S]*回合开始时，获得\{Amount\}点活力'

Require-Pattern 'Spirit requirement now reduces cost by one instead of free' 'src\Models\Cards\ShinGetterCardBase.cs' 'modifiedCost\s*=\s*Math\.Max\(0m,\s*originalCost\s*-\s*1m\)'
Require-Pattern 'Kusuha Juice still makes spirit cards free' 'src\Models\Cards\ShinGetterCardBase.cs' 'GetPower<SGP_KusuhaJuice>\(\)[\s\S]*modifiedCost\s*=\s*0m'
Require-Pattern 'Spirit command hover tip says cost minus one' 'ShinGetterMod\localization\zhs\static_hover_tips.json' '能量消耗降低 1'
Require-Pattern 'Spirit command hover tip no longer says cost becomes zero' 'ShinGetterMod\localization\zhs\static_hover_tips.json' '楠叶汁'

Require-Pattern 'Shin Getter architect dialogues end with player attack' 'src\Patches\ShinGetterAncientDialoguePatch.cs' 'EndAttackers\s*=\s*ArchitectAttackers\.Player'
Require-Pattern 'Shin Getter architect attack patch hooks player attack animation' 'src\Patches\ShinGetterArchitectAttackPatch.cs' 'AnimPlayerAttackIfNecessary'
Require-Pattern 'Shin Getter architect attack patch skips base shuffle for Shin Getter' 'src\Patches\ShinGetterArchitectAttackPatch.cs' 'return\s+false;'
Require-Pattern 'Architect attack sequence plays Getter Beam first' 'src\Patches\ShinGetterArchitectAttackPatch.cs' 'PlayGetterBeamHit'
Require-Pattern 'Architect attack sequence plays Tornado Drill second' 'src\Patches\ShinGetterArchitectAttackPatch.cs' 'PlayTornadoDrillHit'
Require-Pattern 'Architect attack sequence plays Getter Missile third' 'src\Patches\ShinGetterArchitectAttackPatch.cs' 'PlayGetterMissileHit'

Require-VaultPattern 'Feedback2 processing record is under the 2026-07-01 feedback2 heading' '游戏\杀戮尖塔2\问答\12-V0.8.26-2026-06-26反馈处理.md' '## 2026-07-01 反馈2[\s\S]*### 处理记录（2026-07-01，反馈2）'
Require-VaultPattern 'Question 12 has a 2026-07-02 processing record' '游戏\杀戮尖塔2\问答\12-V0.8.26-2026-06-26反馈处理.md' '## 2026-07-02 反馈[\s\S]*### 处理记录（2026-07-02）'
Require-VaultPattern 'Development doc has V0.9.8 record' '游戏\杀戮尖塔2\杀戮尖塔2-盖塔模组-开发文档.md' 'V0\.9\.8'
Require-VaultPattern 'Development doc has B0.6.2 balance record' '游戏\杀戮尖塔2\杀戮尖塔2-盖塔模组-开发文档.md' 'B0\.6\.2'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.8+ / 2026-07-02 feedback checks failing:'
    $failures | Select-Object -First 160
    exit 1
}

Write-Host 'GREEN: v0.9.8+ / 2026-07-02 feedback checks passed.'
