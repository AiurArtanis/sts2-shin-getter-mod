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
    try { return (Read-RepoFile $relativePath) | ConvertFrom-Json -AsHashtable }
    catch {
        $Failures.Add("Invalid JSON: $relativePath ($($_.Exception.Message))")
        return $null
    }
}

function Assert-Contains([string] $name, [string] $text, [string] $pattern) {
    if ($text -notmatch $pattern) { $Failures.Add($name) }
}

function Assert-NotContains([string] $name, [string] $text, [string] $pattern) {
    if ($text -match $pattern) { $Failures.Add($name) }
}

function Assert-JsonContains([string] $name, [hashtable] $json, [string] $key, [string] $pattern) {
    if ($null -eq $json -or -not $json.ContainsKey($key) -or [string]$json[$key] -notmatch $pattern) {
        $Failures.Add($name)
    }
}

function Assert-FrameCount([string] $relativePath, [int] $expectedCount) {
    $path = Join-Path $Root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        $Failures.Add("Missing animation directory: $relativePath")
        return
    }

    $count = (Get-ChildItem -LiteralPath $path -File -Filter 'sprite_*.png' | Measure-Object).Count
    if ($count -ne $expectedCount) {
        $Failures.Add("Animation frame count for $relativePath is $count, expected $expectedCount")
    }
}

$selectScene = Read-RepoFile 'scenes\screens\char_select\char_select_bg_shin_getter.tscn'
Assert-Contains 'Character select root uses top-left anchors' $selectScene 'ShinGetterBg[\s\S]{0,180}anchors_preset\s*=\s*0'
Assert-NotContains 'Character select root no longer inherits full-rect anchors' $selectScene '\[node name="ShinGetterBg"[\s\S]*?anchor_right\s*=\s*1\.0[\s\S]*?(?=\[node name="Background")'
$selectFitter = Read-RepoFile 'src\Nodes\Screens\NShinGetterCharacterSelectBackground.cs'
Assert-Contains 'Character select fitter enforces top-left anchors' $selectFitter 'SetAnchorsPreset\(Control\.LayoutPreset\.TopLeft\)'
Assert-Contains 'Character select fitter still cancels parent overscan' $selectFitter 'AffineInverse\(\)'

$scene = Read-RepoFile 'scenes\creature_visuals\shin_getter.tscn'
Assert-Contains 'Getter One moves another ten percent higher' $scene 'GetterOne[\s\S]{0,420}position\s*=\s*Vector2\(38,\s*-220(?:\.0)?\)'
Assert-Contains 'Getter Two moves another ten percent higher' $scene 'GetterTwo[\s\S]{0,420}position\s*=\s*Vector2\(34,\s*-211\.2\)'
Assert-Contains 'Getter Three moves another ten percent higher' $scene 'GetterThree[\s\S]{0,420}position\s*=\s*Vector2\(22,\s*-232\.8\)'
Assert-Contains 'Shin Getter Dragon moves another ten percent higher' $scene 'ShinDragon[\s\S]{0,420}position\s*=\s*Vector2\(0,\s*-202\.4\)'

$staticVisuals = Read-RepoFile 'src\Nodes\Combat\NShinGetterStaticVisuals.cs'
Assert-Contains 'Shin Form ray wrap shifts left of the creature origin' $staticVisuals 'GlobalPosition\s*=\s*creatureNode\.GlobalPosition\s*\+\s*new Vector2\(-16f,\s*-205f\)'

$overload = Read-RepoFile 'src\Models\Powers\SGP_Overload.cs'
Assert-Contains 'Overload drains energy after all normal reset hooks' $overload 'override\s+async\s+Task\s+AfterEnergyResetLate\('
Assert-NotContains 'Overload no longer drains during the normal reset hook' $overload 'override\s+async\s+Task\s+AfterEnergyReset\('

$dialogue = Read-RepoFile 'src\Patches\ShinGetterAncientDialoguePatch.cs'
Assert-Contains 'Every Shin Getter ancient dialogue remains repeatable' $dialogue 'IsRepeating\s*=\s*true'
Assert-NotContains 'Ancient dialogue availability is not limited to visit indices' $dialogue 'VisitIndex\s*='

$holy = Read-RepoFile 'src\Models\Cards\SGC_HolyDragonRoar.cs'
Assert-Contains 'Holy Dragon Roar exhausts every Shin Getter card' $holy 'card\s+is\s+ShinGetterCardBase'
Assert-Contains 'Holy Dragon Roar suppresses its enqueue animation' $holy 'OnEnqueuePlayVfx[\s\S]{0,180}Task\.CompletedTask'
Assert-Contains 'Holy Dragon Roar exhausts cards before playing Cast and damage' $holy 'foreach[\s\S]{0,220}CardCmd\.Exhaust[\s\S]{0,260}TryPlayCreatureActionAnimation\([^;]+"Cast"\)[\s\S]{0,260}DamageCmd\.Attack'
Assert-Contains 'Holy Dragon Roar suppresses the default attack animation' $holy '\.WithNoAttackerAnim\(\)'

$beam = Read-RepoFile 'src\Nodes\Vfx\ShinGetterBeamVfx.cs'
Assert-Contains 'Final Getter Beam remaps blue into the established pink Getter palette' $beam 'FinalGetterBeam[\s\S]{0,260}RemapBlueToGetterPink'
Assert-Contains 'Final Getter Beam creates a complete centered Getter beam' $beam 'AddCenterGetterBeam\(owner,\s*livingTargets\.Last\(\)\)'
Assert-Contains 'Centered Getter beam is a second Hyperbeam VFX instance' $beam 'AddCenterGetterBeam[\s\S]{0,500}NHyperbeamVfx\.Create'
Assert-Contains 'Centered Getter beam keeps full length and narrows only its local height' $beam 'Scale\s*=\s*new Vector2\(1f,\s*0\.35f\)'

$beamPower = Read-RepoFile 'src\Models\Powers\SGP_FinalGetterBeamStrengthDown.cs'
Assert-NotContains 'Final Getter Beam debuff title comes from its source card' $beamPower 'override\s+LocString\s+Title'
$beamIcon = Read-RepoFile 'images\atlases\power_atlas.sprites\s_g_p_final_getter_beam_strength_down.tres'
Assert-Contains 'Final Getter Beam debuff uses its new atlas icon' $beamIcon 'power_icons_atlas_shin_getter\.png[\s\S]*region\s*=\s*Rect2\(128,\s*256,\s*64,\s*64\)'
$beamBigIconPath = Join-Path $Root 'images\powers\s_g_p_final_getter_beam_strength_down.png'
if (-not (Test-Path -LiteralPath $beamBigIconPath)) {
    $Failures.Add('Missing Final Getter Beam debuff big icon')
} elseif ((Get-FileHash -Algorithm SHA256 -LiteralPath $beamBigIconPath).Hash -eq '80C8FC229B63528D95CF0936E08B8B6C19D5E15B76D029A9D368070E2903D9E7') {
    $Failures.Add('Final Getter Beam debuff still uses the old placeholder big icon')
}

$ki = Read-RepoFile 'src\Models\Cards\SGC_Ki.cs'
Assert-Contains 'Ki aura runs independently from card resolution' $ki 'TaskHelper\.RunSafely\(ShinGetterCombatVfx\.PlayKiAura'
Assert-NotContains 'Ki no longer waits on its cosmetic aura' $ki 'await\s+ShinGetterCombatVfx\.PlayKiAura'

$spriteSequence = Read-RepoFile 'src\Nodes\Combat\NShinGetterSpriteSequence.cs'
foreach ($expectation in @(
    'GetterOneDashFrameDirectory',
    'GetterOneBlockFrameDirectory',
    'GetterTwoDeathFrameDirectory',
    'ShinDragonAttackFrameDirectory',
    'ShinDragonBlockFrameDirectory'
)) {
    Assert-Contains "Sprite loader includes $expectation" $spriteSequence $expectation
}
Assert-FrameCount 'images\characters\shin_getter\forms\getter_one_dash' 48
Assert-FrameCount 'images\characters\shin_getter\forms\getter_one_block' 24
Assert-FrameCount 'images\characters\shin_getter\forms\getter_two_death' 48
Assert-FrameCount 'images\characters\shin_getter\forms\shin_getter_dragon_attack' 60
Assert-FrameCount 'images\characters\shin_getter\forms\shin_getter_dragon_block' 121

$resourceValidator = Read-RepoFile 'tools\validate-mod-resources.gd'
foreach ($representative in @(
    'getter_one_dash/sprite_000121.png',
    'getter_one_block/sprite_000121.png',
    'getter_two_death/sprite_000121.png',
    'shin_getter_dragon_attack/sprite_000121.png',
    'shin_getter_dragon_block/sprite_000121.png',
    'power_atlas.sprites/s_g_p_final_getter_beam_strength_down.tres',
    'powers/s_g_p_final_getter_beam_strength_down.png'
)) {
    Assert-Contains "PCK validator includes $representative" $resourceValidator ([regex]::Escape($representative))
}

foreach ($language in @('zhs', 'eng', 'jpn')) {
    $cards = Read-Json "ShinGetterMod\localization\$language\cards.json"
    $powers = Read-Json "ShinGetterMod\localization\$language\powers.json"
    $potions = Read-Json "ShinGetterMod\localization\$language\potions.json"

    Assert-JsonContains "$language Spirit names upgraded Ki" $cards 'S_G_C_SPIRIT.description' '\{IfUpgraded:show:\+\}'
    Assert-JsonContains "$language Holy Dragon Roar uses the revised description" $cards 'S_G_C_HOLY_DRAGON_ROAR.description' '\{Damage:diff\(\)\}[\s\S]*\{BurnDamage:diff\(\)\}[\s\S]*(击晕|Stun|スタン)'
    Assert-JsonContains "$language Overload uses an energy icon" $powers 'S_G_P_OVERLOAD.description' '\{Amount:energyIcons\(\)\}'
    Assert-JsonContains "$language Evolution Engine uses an energy icon" $powers 'S_G_P_EVOLUTION_ENGINE.description' '\{Amount:energyIcons\(\)\}'
    Assert-JsonContains "$language Acceleration uses an energy icon" $powers 'S_G_P_ACCELERATION.description' '\{Amount:energyIcons\(\)\}'
    Assert-JsonContains "$language Transformation Lube uses an energy icon" $potions 'S_G_R_TRANSFORM_POTION.description' '\{Energy:energyIcons\(\)\}'
}

$manifest = Read-Json 'ShinGetterMod.json'
Assert-JsonContains 'Manifest advances to v0.9.36' $manifest 'version' '^v0\.9\.36$'

if ($Failures.Count -gt 0) {
    Write-Host 'FAILED v0.9.36 2026-07-13 second playtest follow-up checks:' -ForegroundColor Red
    foreach ($failure in $Failures) { Write-Host " - $failure" -ForegroundColor Red }
    exit 1
}

Write-Host 'PASSED v0.9.36 2026-07-13 second playtest follow-up checks.' -ForegroundColor Green
