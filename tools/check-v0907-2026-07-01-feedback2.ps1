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

function Require-VaultPattern([string]$name, [string]$relativePath, [string]$pattern) {
    $text = Read-VaultFile $relativePath
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

Require-JsonMinimumVersion 'Root manifest version is at least v0.9.7' 'ShinGetterMod.json' 'v0.9.7'
Require-JsonMinimumVersion 'Build manifest version is at least v0.9.7' 'build\ShinGetterMod.json' 'v0.9.7'

Require-Pattern 'Acceleration requires 3 spirit' 'src\Models\Cards\SGC_Acceleration.cs' 'SpiritRequirement\s*=>\s*3'
Require-Pattern 'Awakened Soul requires 4 spirit' 'src\Models\Cards\SGC_AwakenedSoul.cs' 'SpiritRequirement\s*=>\s*4'
Require-Pattern 'Enable requires 6 spirit before upgrade and 5 after upgrade' 'src\Models\Cards\SGC_Enable.cs' 'SpiritRequirement\s*=>\s*IsUpgraded\s*\?\s*5\s*:\s*6'
Require-Pattern 'Enable upgrade preview spirit requirement is 5' 'src\Models\Cards\SGC_Enable.cs' 'UpgradePreviewSpiritRequirement\s*=>\s*5'
Require-Pattern 'Hot Blood requires 3 spirit before upgrade and 2 after upgrade' 'src\Models\Cards\SGC_HotBlood.cs' 'SpiritRequirement\s*=>\s*IsUpgraded\s*\?\s*2\s*:\s*3'
Require-Pattern 'Hot Blood upgrade preview spirit requirement is 2' 'src\Models\Cards\SGC_HotBlood.cs' 'UpgradePreviewSpiritRequirement\s*=>\s*2'
Require-AbsentPattern 'Hot Blood upgrade no longer increases damage' 'src\Models\Cards\SGC_HotBlood.cs' 'Damage\.UpgradeValueBy'
Require-Pattern 'Iron Wall amount is 7' 'src\Models\Cards\SGC_IronWall.cs' 'PowerVar<SGP_IronWall>\(7m\)'
Require-Pattern 'Getter Claw base damage is 3' 'src\Models\Cards\SGC_GetterClaw.cs' 'new\s+DamageVar\(3m'

Require-Pattern 'Stoner Sunshine uses calculated damage preview' 'src\Models\Cards\SGC_StonerSunshine.cs' 'CalculatedDamageVar\(ValueProp\.Move\)\.WithMultiplier\(GetVigorGainedBonus\)'
Require-Pattern 'Stoner Sunshine counts only gained VigorPower' 'src\Models\Cards\SGC_StonerSunshine.cs' 'entry\.Power\s+is\s+VigorPower[\s\S]*entry\.Amount\s*>\s*0'
Require-AbsentPattern 'Stoner Sunshine no longer counts all buff power gains' 'src\Models\Cards\SGC_StonerSunshine.cs' 'Power\.Type\s*==\s*PowerType\.Buff'
Require-Pattern 'Stoner Sunshine has dynamic Wane amount' 'src\Models\Cards\SGC_StonerSunshine.cs' 'new\s+DynamicVar\("Wane",\s*2m\)'
Require-Pattern 'Stoner Sunshine upgrades Wane to 3' 'src\Models\Cards\SGC_StonerSunshine.cs' 'DynamicVars\["Wane"\]\.UpgradeValueBy\(1m\)'
Require-Pattern 'Stoner Sunshine applies dynamic Wane amount' 'src\Models\Cards\SGC_StonerSunshine.cs' 'DynamicVars\["Wane"\]\.BaseValue'
Require-Pattern 'Stoner Sunshine localization mentions gained vigor bonus' 'ShinGetterMod\localization\zhs\cards.json' 'S_G_C_STONER_SUNSHINE\.description[\s\S]*本场战斗获得的\[gold\]活力\[/gold\]'
Require-Pattern 'Dark Cape localization mentions Getter 1 Airborne reward' 'ShinGetterMod\localization\zhs\cards.json' 'S_G_C_DARK_CAPE\.description[\s\S]*\[red\]一号机\[/red\][\s\S]*腾空'
Require-Pattern 'Stoner Sunshine contextual terms include vigor' 'src\Models\Cards\ShinGetterCardBase.cs' 'SGC_StonerSunshine"\]\s*=\s*new\[\]\s*\{[^}]*"衰退"[^}]*"活力"'

Require-Pattern 'Evolution waits briefly after flashing before consuming itself' 'src\Models\Powers\SGP_Evolution.cs' 'Flash\(\);\s*await\s+Cmd\.CustomScaledWait'

Require-Pattern 'Frame shader exposes base tint color' 'shaders\shin_getter_hsv.gdshader' 'uniform vec4 tint_base_color'
Require-Pattern 'Frame shader exposes base tint strength' 'shaders\shin_getter_hsv.gdshader' 'uniform float tint_base_strength'
Require-Pattern 'Frame shader separates border from middle by luminance' 'shaders\shin_getter_hsv.gdshader' 'smoothstep\([^;]*luma'
Require-Pattern 'Frame material keeps export tint disabled' 'materials\cards\frames\card_frame_shin_getter_mat.tres' 'shader_parameter/tint_strength = 0(?:\.0)?'
Require-Pattern 'Frame material increases default frame saturation' 'materials\cards\frames\card_frame_shin_getter_mat.tres' 'shader_parameter/s = 1\.0[0-9]'
Require-Pattern 'Frame material increases default frame brightness' 'materials\cards\frames\card_frame_shin_getter_mat.tres' 'shader_parameter/v = 1\.1[0-9]'
Require-Pattern 'Card frame patch suppresses mid-transform refresh' 'src\Patches\ShinGetterCardFramePatch.cs' 'BeginFormTransition[\s\S]*EndFormTransitionAndRefresh'
Require-Pattern 'Card frame patch animates base tint as well as border tint' 'src\Patches\ShinGetterCardFramePatch.cs' 'fromBaseColor[\s\S]*toBaseColor[\s\S]*tint_base_color'
Require-Pattern 'Card frame patch gives Getter 2 a gray middle base color' 'src\Patches\ShinGetterCardFramePatch.cs' 'GetterTwoSilverBaseKey'
Require-Pattern 'TransformTo refreshes frames after final form is applied' 'src\Models\Cards\ShinGetterCardBase.cs' 'BeginFormTransition\(\)[\s\S]*finally[\s\S]*EndFormTransitionAndRefresh\(\)'
Require-Pattern 'Shin Form card refreshes frames after final form is applied' 'src\Models\Cards\SGC_ShinForm.cs' 'BeginFormTransition\(\)[\s\S]*finally[\s\S]*EndFormTransitionAndRefresh\(\)'

Require-VaultPattern 'Design doc relic table includes Getter Furnace class' '游戏\杀戮尖塔2\杀戮尖塔2-盖塔模组-设计文档.md' '盖塔熔炉[\s\S]*SGR_GetterFurnace'
Require-VaultPattern 'Design doc relic table includes Ken Ishikawa Manuscript class' '游戏\杀戮尖塔2\杀戮尖塔2-盖塔模组-设计文档.md' '石川贤的原稿[\s\S]*SGR_KenIshikawaManuscript'
Require-VaultPattern 'Design doc potion table includes Transform Potion class' '游戏\杀戮尖塔2\杀戮尖塔2-盖塔模组-设计文档.md' '变形润滑油[\s\S]*SGR_TransformPotion'
Require-VaultPattern 'Design doc potion table includes Getter Cold Brew class' '游戏\杀戮尖塔2\杀戮尖塔2-盖塔模组-设计文档.md' '盖塔冷萃[\s\S]*SGR_GetterColdBrew'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.7+ / 2026-07-01 feedback2 checks failing:'
    $failures | Select-Object -First 120
    exit 1
}

Write-Host 'GREEN: v0.9.7+ / 2026-07-01 feedback2 checks passed.'
