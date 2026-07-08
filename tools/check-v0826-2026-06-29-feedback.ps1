$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Read-RepoFile([string]$relativePath) {
    $path = Join-Path $root $relativePath
    if (!(Test-Path -LiteralPath $path)) {
        return $null
    }

    return Get-Content -Raw -Encoding UTF8 -LiteralPath $path
}

function Require-File([string]$name, [string]$relativePath) {
    if (!(Test-Path -LiteralPath (Join-Path $root $relativePath))) {
        $failures.Add($name)
    }
}

function Require-AbsentPath([string]$name, [string]$relativePath) {
    if (Test-Path -LiteralPath (Join-Path $root $relativePath)) {
        $failures.Add($name)
    }
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

function Require-RepoTextAbsent([string]$name, [string]$pattern) {
    $matches = Get-ChildItem -LiteralPath $root -Recurse -File |
        Where-Object {
            $_.FullName -notlike '*\.git\*' -and
            $_.FullName -notlike '*\.godot\*' -and
            $_.FullName -notlike '*\bin\*' -and
            $_.FullName -notlike '*\obj\*' -and
            $_.FullName -notlike '*\build\*' -and
            $_.FullName -notlike '*\tmp\*' -and
            $_.Name -ne 'check-v0826-2026-06-29-feedback.ps1' -and
            $_.Extension -in @('.cs', '.json', '.md', '.base', '.ps1', '.gd', '.tres', '.import')
        } |
        Select-String -Pattern $pattern -SimpleMatch -List

    if ($matches) {
        $failures.Add($name)
    }
}

Require-File 'Stoner Sunshine source file exists' 'src\Models\Cards\SGC_StonerSunshine.cs'
Require-AbsentPath 'Old Stoner Shine source file removed' 'src\Models\Cards\SGC_StonerShine.cs'
Require-AbsentPath 'Old Stoner Shine atlas sprite removed' 'images\atlases\card_atlas.sprites\shin_getter\s_g_c_stoner_shine.tres'
Require-File 'Stoner Sunshine large portrait exists' 'images\packed\card_portraits\shin_getter\s_g_c_stoner_sunshine.png'
Require-File 'Stoner Sunshine single-card portrait exists' 'images\packed\card_single\shin_getter\s_g_c_stoner_sunshine_card.png'
Require-File 'Shin Form large portrait exists' 'images\packed\card_portraits\shin_getter\s_g_c_shin_form.png'
Require-File 'Shin Form single-card portrait exists' 'images\packed\card_single\shin_getter\s_g_c_shin_form_card.png'

Require-Pattern 'Stoner Sunshine class renamed' 'src\Models\Cards\SGC_StonerSunshine.cs' 'public sealed class SGC_StonerSunshine'
Require-Pattern 'Stoner Sunshine constructor renamed' 'src\Models\Cards\SGC_StonerSunshine.cs' 'public SGC_StonerSunshine\(\)'
Require-Pattern 'Stoner Sunshine uses single-card portrait' 'src\Models\Cards\SGC_StonerSunshine.cs' 'card_single/shin_getter/s_g_c_stoner_sunshine_card\.png'
Require-Pattern 'Stoner Sunshine tracks large portrait asset' 'src\Models\Cards\SGC_StonerSunshine.cs' 'card_portraits/shin_getter/s_g_c_stoner_sunshine\.png'
Require-Pattern 'Shin Form uses single-card portrait' 'src\Models\Cards\SGC_ShinForm.cs' 'card_single/shin_getter/s_g_c_shin_form_card\.png'
Require-Pattern 'Shin Form tracks large portrait asset' 'src\Models\Cards\SGC_ShinForm.cs' 'card_portraits/shin_getter/s_g_c_shin_form\.png'
Require-Pattern 'Card pool uses Stoner Sunshine' 'src\Models\CardPools\ShinGetterCardPool.cs' 'ModelDb\.Card<SGC_StonerSunshine>\(\)'
Require-Pattern 'Card tips use Stoner Sunshine key' 'src\Models\Cards\ShinGetterCardBase.cs' '\["SGC_StonerSunshine"\]'
Require-Pattern 'Localization uses Stoner Sunshine key' 'ShinGetterMod\localization\zhs\cards.json' '"S_G_C_STONER_SUNSHINE\.title"'

Require-AbsentPattern 'No old Stoner Shine class reference in card pool' 'src\Models\CardPools\ShinGetterCardPool.cs' 'SGC_StonerShine'
Require-AbsentPattern 'No old Stoner Shine term key' 'src\Models\Cards\ShinGetterCardBase.cs' 'SGC_StonerShine'
Require-AbsentPattern 'No old Stoner Shine loc key' 'ShinGetterMod\localization\zhs\cards.json' 'S_G_C_STONER_SHINE'
Require-RepoTextAbsent 'No old Stoner Shine identifier remains' 'SGC_StonerShine'
Require-RepoTextAbsent 'No old Stoner Shine localization key remains' 'S_G_C_STONER_SHINE'
Require-RepoTextAbsent 'No old Stoner Shine resource key remains' 's_g_c_stoner_shine'

Require-Pattern 'Spirit UI icons stay at normal card-local ZIndex' 'src\Patches\SpiritRequirementCardUiPatch.cs' 'ZIndex = 0'
Require-AbsentPattern 'Spirit UI no longer forces high ZIndex 30' 'src\Patches\SpiritRequirementCardUiPatch.cs' 'ZIndex = 30'
Require-AbsentPattern 'Spirit UI no longer forces high ZIndex 31' 'src\Patches\SpiritRequirementCardUiPatch.cs' 'ZIndex = 31'

Require-File 'Multi-attack intent patch exists' 'src\Patches\ShinGetterMultiAttackIntentPatch.cs'
Require-Pattern 'Multi-attack intent patch targets MultiAttackIntent' 'src\Patches\ShinGetterMultiAttackIntentPatch.cs' 'MultiAttackIntent'
Require-Pattern 'Multi-attack intent patch applies Grapple' 'src\Patches\ShinGetterMultiAttackIntentPatch.cs' 'SGP_Grapple'
Require-Pattern 'Multi-attack intent patch clamps repeats at zero' 'src\Patches\ShinGetterMultiAttackIntentPatch.cs' 'Math\.Max\(0'

Require-Pattern 'Getter Missile filters living missile targets' 'src\Models\Cards\SGC_GetterMissile.cs' 'GetLivingMissileTargets'
Require-Pattern 'Getter Missile stops after enemies are gone' 'src\Models\Cards\SGC_GetterMissile.cs' 'HasLivingEnemyTargets'
Require-Pattern 'Getter Missile checks combat ending during loop' 'src\Models\Cards\SGC_GetterMissile.cs' 'CombatManager\.Instance\.IsOverOrEnding'

Require-File 'Ancient reward patch exists' 'src\Patches\ShinGetterAncientRewardPatch.cs'
Require-Pattern 'Dusty Tome grants Shin Form to Shin Getter' 'src\Patches\ShinGetterAncientRewardPatch.cs' 'DustyTome[\s\S]*SGC_ShinForm'
Require-Pattern 'Archaic Tooth transforms Getter Beam to Stoner Sunshine' 'src\Patches\ShinGetterAncientRewardPatch.cs' 'ArchaicTooth[\s\S]*SGC_GetterBeam[\s\S]*SGC_StonerSunshine'

Require-File 'Old Stoner Shine progress save compatibility patch exists' 'src\Patches\ShinGetterProgressCompatibilityPatch.cs'
Require-Pattern 'Progress compatibility patch normalizes card stats' 'src\Patches\ShinGetterProgressCompatibilityPatch.cs' 'NormalizeCardStats'
Require-Pattern 'Progress compatibility patch normalizes discovered cards' 'src\Patches\ShinGetterProgressCompatibilityPatch.cs' 'NormalizeDiscoveredCards'
Require-Pattern 'Progress compatibility patch maps to Stoner Sunshine' 'src\Patches\ShinGetterProgressCompatibilityPatch.cs' 'SGC_StonerSunshine'

if ($failures.Count -gt 0) {
    Write-Host 'RED: 2026-06-29 feedback checks failing:'
    $failures | Select-Object -First 40
    exit 1
}

Write-Host 'GREEN: 2026-06-29 feedback checks passed.'
