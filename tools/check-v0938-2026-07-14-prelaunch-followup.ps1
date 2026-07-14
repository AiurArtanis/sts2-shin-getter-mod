$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $projectRoot "ShinGetterMod.json"
$scenePath = Join-Path $projectRoot "scenes\screens\char_select\char_select_bg_shin_getter.tscn"
$fitterPath = Join-Path $projectRoot "src\Nodes\Screens\NShinGetterCharacterSelectBackground.cs"
$fitterUidPath = "$fitterPath.uid"
$voicePatchPath = Join-Path $projectRoot "src\Patches\ShinGetterCombatStartVoicePatch.cs"
$voicePatchUidPath = "$voicePatchPath.uid"
$relicPath = Join-Path $projectRoot "src\Models\Relics\SGR_GetterFurnace.cs"

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Needle,
        [string]$Message
    )

    Assert-True -Condition $Text.Contains($Needle, [System.StringComparison]::Ordinal) -Message $Message
}

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Needle,
        [string]$Message
    )

    Assert-True -Condition (-not $Text.Contains($Needle, [System.StringComparison]::Ordinal)) -Message $Message
}

$scene = Get-Content -LiteralPath $scenePath -Raw
Assert-Contains $scene '[node name="ShinGetterBg" type="Control"]' "Character-select background root is missing."
Assert-Contains $scene 'anchors_preset = 15' "Character-select background must use the proven full-rect root layout."
Assert-Contains $scene 'anchor_right = 1.0' "Character-select background must fill the parent horizontally."
Assert-Contains $scene 'anchor_bottom = 1.0' "Character-select background must fill the parent vertically."
Assert-Contains $scene '[node name="Portrait" type="TextureRect" parent="."]' "Character-select background must expose the conventional Portrait node."
Assert-Contains $scene 'stretch_mode = 6' "Character-select portrait must preserve aspect ratio and cover the available area."
Assert-NotContains $scene 'NShinGetterCharacterSelectBackground.cs' "Character-select background must not depend on the failed viewport fitter."
Assert-True -Condition (-not (Test-Path -LiteralPath $fitterPath)) -Message "Obsolete character-select viewport fitter must be removed."
Assert-True -Condition (-not (Test-Path -LiteralPath $fitterUidPath)) -Message "Obsolete character-select viewport fitter UID must be removed."

Assert-True -Condition (Test-Path -LiteralPath $voicePatchPath -PathType Leaf) -Message "Preload-stage combat voice patch is missing."
Assert-True -Condition (Test-Path -LiteralPath $voicePatchUidPath -PathType Leaf) -Message "Preload-stage combat voice patch UID is missing."
$voicePatch = Get-Content -LiteralPath $voicePatchPath -Raw
$relic = Get-Content -LiteralPath $relicPath -Raw
Assert-Contains $voicePatch 'HarmonyPatch(typeof(CombatRoom), "StartCombat"' "Combat-start voice must hook the pre-load StartCombat stage."
Assert-Contains $voicePatch 'ShinGetterVoiceService.PlayCombatStart(player)' "Pre-load patch must play the switch-on cue for a Shin Getter player."
Assert-NotContains $relic 'ShinGetterVoiceService.PlayCombatStart(Owner)' "Relic BeforeCombatStart is too late for the switch-on cue."

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Assert-True -Condition ($manifest.version -eq "v0.9.38") -Message "Manifest version must be v0.9.38."

Write-Host "PASSED v0.9.38 prelaunch follow-up checks."
