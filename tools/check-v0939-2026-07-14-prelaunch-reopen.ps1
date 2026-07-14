$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Failures = New-Object System.Collections.Generic.List[string]

function Read-RepoFile([string] $relativePath) {
    $path = Join-Path $Root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
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

function Assert-JsonNotContains([string] $name, [hashtable] $json, [string] $key, [string] $pattern) {
    if ($null -eq $json -or -not $json.ContainsKey($key) -or [string]$json[$key] -match $pattern) {
        $Failures.Add($name)
    }
}

function Assert-ImageSize([string] $relativePath, [int] $width, [int] $height) {
    $path = Join-Path $Root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $Failures.Add("Missing image: $relativePath")
        return
    }

    Add-Type -AssemblyName System.Drawing.Common
    $image = [System.Drawing.Image]::FromFile($path)
    try {
        if ($image.Width -ne $width -or $image.Height -ne $height) {
            $Failures.Add("Image size for $relativePath is $($image.Width)x$($image.Height), expected ${width}x${height}")
        }
    } finally {
        $image.Dispose()
    }
}

function Assert-FrameDirectorySize([string] $relativePath, [int] $width, [int] $height) {
    $path = Join-Path $Root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        $Failures.Add("Missing animation directory: $relativePath")
        return
    }

    foreach ($frame in Get-ChildItem -LiteralPath $path -File -Filter 'sprite_*.png') {
        Add-Type -AssemblyName System.Drawing.Common
        $image = [System.Drawing.Image]::FromFile($frame.FullName)
        try {
            if ($image.Width -ne $width -or $image.Height -ne $height) {
                $Failures.Add("Frame size for $relativePath/$($frame.Name) is $($image.Width)x$($image.Height), expected ${width}x${height}")
                return
            }
        } finally {
            $image.Dispose()
        }
    }
}

$scene = Read-RepoFile 'scenes\screens\char_select\char_select_bg_shin_getter.tscn'
Assert-Contains 'Character-select background mirrors the fixed 2560x1200 base layout' $scene 'anchors_preset\s*=\s*8[\s\S]*offset_left\s*=\s*-960\.0[\s\S]*offset_top\s*=\s*-523\.0[\s\S]*offset_right\s*=\s*1600\.0[\s\S]*offset_bottom\s*=\s*677\.0'
Assert-Contains 'Character-select portrait uses an explicit 2560x1200 rectangle' $scene 'Portrait[\s\S]{0,260}offset_right\s*=\s*2560\.0[\s\S]{0,120}offset_bottom\s*=\s*1200\.0'
Assert-NotContains 'Character-select background no longer nests a second full-rect cover layer' $scene 'Portrait[\s\S]{0,180}anchors_preset\s*=\s*15'

$voice = Read-RepoFile 'src\Audio\ShinGetterVoiceService.cs'
$combatVoice = Read-RepoFile 'src\Patches\ShinGetterCombatStartVoicePatch.cs'
$shining = Read-RepoFile 'src\Models\Cards\SGC_ShiningSpark.cs'
Assert-Contains 'Voice history has a per-combat reset entry point' $voice 'ResetCombatVoiceHistory'
Assert-Contains 'Combat start resets voice history before claiming switch-on' $combatVoice 'ResetCombatVoiceHistory[\s\S]{0,220}PlayCombatStart'
Assert-Contains 'Combat reset runs on the initial round' $combatVoice 'RoundNumber\s*==\s*1'
Assert-NotContains 'Combat reset does not use an unreachable pre-round guard' $combatVoice 'RoundNumber\s*<=\s*0'
Assert-Contains 'Combat reset is idempotent for the same combat state' $combatVoice 'ConditionalWeakTable<CombatState,\s*CombatVoiceResetState>'
Assert-Contains 'Shining and Spark use explicit awaitable voice APIs' $voice 'PlayShiningSparkIntro[\s\S]*PlayShiningSparkFollowUp'
Assert-NotContains 'Shining Spark is not routed through the generic card voice switch' $voice 'SGC_ShiningSpark\s*=>\s*Lines\[ShinGetterVoiceCue\.ShiningSpark\]'
Assert-Contains 'Shining Spark awaits flash plus Shining, then the pause, then dash plus Spark' $shining 'Task\.WhenAll[\s\S]{0,320}PlayShiningSparkIntro[\s\S]{0,320}Cmd\.Wait\(0\.2f\)[\s\S]{0,420}Task\.WhenAll[\s\S]{0,320}PlayShiningSparkFollowUp'
Assert-Contains 'Shining Spark suppresses the default attacker animation' $shining 'WithNoAttackerAnim'

$cardBase = Read-RepoFile 'src\Models\Cards\ShinGetterCardBase.cs'
Assert-Contains 'Transform audio starts after the target form power is applied' $cardBase 'ApplyFormPower[\s\S]{0,240}ShinGetterVoiceService\.PlayTransform'
Assert-Contains 'Movement VFX waits for the dash charge point' $cardBase 'WaitForDashCharge'
Assert-Contains 'Card base exposes accelerated follow-up animation support' $cardBase 'PlayAcceleratedFollowupAnimation'

$creatureScene = Read-RepoFile 'scenes\creature_visuals\shin_getter.tscn'
Assert-Contains 'Getter Two moves another five percent upward' $creatureScene 'GetterTwo[\s\S]{0,420}position\s*=\s*Vector2\(34,\s*-221\.76\)'
Assert-Contains 'Getter Two shrinks five percent' $creatureScene 'GetterTwo[\s\S]{0,420}scale\s*=\s*Vector2\(0\.627,\s*0\.627\)'
Assert-Contains 'Shin Dragon moves another five percent upward' $creatureScene 'ShinDragon[\s\S]{0,420}position\s*=\s*Vector2\(0,\s*-212\.52\)'

$spriteSequence = Read-RepoFile 'src\Nodes\Combat\NShinGetterSpriteSequence.cs'
$spriteState = Read-RepoFile 'src\Nodes\Combat\NShinGetterSpriteAnimationStateMachine.cs'
Assert-Contains 'Getter One block animation runs at 45 FPS' $spriteSequence 'GetterOneBlockFramesPerSecond\s*=\s*45d'
Assert-Contains 'Shin Dragon attack animation runs at 54 FPS' $spriteSequence 'ShinDragonAttackFramesPerSecond\s*=\s*54d'
Assert-Contains 'Shin Dragon block animation runs at 60 FPS' $spriteSequence 'ShinDragonBlockFramesPerSecond\s*=\s*60d'
Assert-Contains 'Shin Dragon block scales its 960px frames back to the 720px footprint' $spriteState 'ShinDragonBlockScale\s*=\s*0\.75f'
Assert-Contains 'Animation state supports queued follow-up speed' $spriteState 'QueueNextActionSpeed'
Assert-FrameDirectorySize 'images\characters\shin_getter\forms\shin_getter_dragon_block' 960 960
foreach ($directory in @(
    'getter_one_block',
    'getter_two_block',
    'getter_three_block',
    'shin_getter_dragon_attack',
    'getter_one_dash',
    'getter_two_dash',
    'getter_three_dash',
    'shin_getter_dragon_dash'
)) {
    Assert-FrameDirectorySize "images\characters\shin_getter\forms\$directory" 720 720
}

$awakenedVfx = Read-RepoFile 'src\Nodes\Vfx\ShinGetterCombatVfx.Extra.cs'
Assert-Contains 'Awakened Soul flash loads the provided 256px power icon' $awakenedVfx 'AwakenedSoulFlashTexturePath[\s\S]{0,220}s_g_p_awakened_soul\.png'
Assert-NotContains 'Obsolete procedural Awakened Soul symbol is removed' $awakenedVfx 'CreateNewtypeSign'

$consoleAlias = Read-RepoFile 'src\Patches\CardConsoleAliasPatch.cs'
Assert-Contains 'Card alias patch hooks argument completions' $consoleAlias 'GetArgumentCompletions'
Assert-Contains 'Saint Dragon Roar alias is offered as a completion candidate' $consoleAlias 's_g_c_saint_dragon_roar'

$ancient = Read-RepoFile 'src\Patches\ShinGetterAncientDialoguePatch.cs'
Assert-Contains 'Ancient dialogue visit indices are restored for deterministic progression' $ancient 'VisitIndex\s*=\s*dialogueIndex'
Assert-Contains 'Ancient dialogue has a final fallback for arbitrarily repeated visits' $ancient 'ShinGetterAncientDialogueFallbackPatch'

$holy = Read-RepoFile 'src\Models\Cards\SGC_HolyDragonRoar.cs'
$combatVfx = Read-RepoFile 'src\Nodes\Vfx\ShinGetterCombatVfx.cs'
Assert-Contains 'Holy Dragon Roar filters by Getter in the runtime class name' $holy 'GetType\(\)\.Name\.Contains\("Getter"'
Assert-Contains 'Holy Dragon Roar scans draw, hand, and discard piles' $holy 'PileType\.Draw[\s\S]*PileType\.Hand[\s\S]*PileType\.Discard'
Assert-Contains 'Holy Dragon Roar exhausts eligible cards sequentially before attacking' $holy 'foreach[\s\S]{0,260}CardCmd\.Exhaust[\s\S]{0,420}DamageCmd\.Attack'
Assert-Contains 'Holy Dragon Roar uses the centered VFX entry point' $holy 'PlayHolyDragonRoarAtScreenCenter'
Assert-Contains 'Centered Holy Dragon Roar VFX is enlarged thirty percent' $combatVfx 'PlayHolyDragonRoarAtScreenCenter[\s\S]{0,420}1\.3f'

$beam = Read-RepoFile 'src\Nodes\Vfx\ShinGetterBeamVfx.cs'
Assert-Contains 'Final Getter Beam outer layer uses Getter Ray green' $beam 'GetterRay[\s\S]*FinalGetterBeam[\s\S]{0,320}TintCanvasItems\(beam, GetterRay'
Assert-Contains 'Final Getter Beam retains the centered pink Getter Beam core' $beam 'AddCenterGetterBeam[\s\S]{0,900}ShinGetterBeamStyle\.GetterBeam'

$spirit = Read-RepoFile 'src\Models\Cards\SGC_Spirit.cs'
Assert-Contains 'Spirit creates Ki in the current card scope' $spirit 'CardScope\.CreateCard<SGC_Ki>'
Assert-NotContains 'Spirit no longer creates a combat replacement from RunState' $spirit 'RunState\.CreateCard<SGC_Ki>'
Assert-Contains 'Upgraded Spirit hover previews upgraded Ki' $cardBase 'HoverTipFactory\.FromCard<SGC_Ki>\(card\s+is\s+SGC_Spirit\s+&&\s+card\.IsUpgraded\)'

$getterLaunch = Read-RepoFile 'src\Models\Cards\SGC_GetterLaunch.cs'
$getterBeam = Read-RepoFile 'src\Models\Cards\SGC_GetterBeam.cs'
$boldPlan = Read-RepoFile 'src\Models\Cards\SGC_BoldPlan.cs'
$mandala = Read-RepoFile 'src\Models\Events\SGE_GetterMandala.cs'
Assert-Contains 'Getter Launch grants one Ki before upgrade' $getterLaunch 'PowerVar<SGP_Ki>\(1m\)'
Assert-Contains 'Getter Launch upgrade grants one additional Ki' $getterLaunch 'SGP_Ki.*UpgradeValueBy\(1m\)'
Assert-Contains 'Getter Beam applies two Wane before upgrade' $getterBeam 'DynamicVar\("Wane",\s*2m\)'
Assert-Contains 'Getter Beam upgrade adds one Wane' $getterBeam '"Wane"\]\.UpgradeValueBy\(1m\)'
Assert-Contains 'Shining Spark starts at eleven main damage and six Ki damage' $shining 'DamageVar\(11m[\s\S]{0,120}DynamicVar\("KiDamage",\s*6m\)'
Assert-Contains 'Shining Spark upgrades both damage values by three' $shining 'Damage\.UpgradeValueBy\(3m\)[\s\S]{0,120}"KiDamage"\]\.UpgradeValueBy\(3m\)'
Assert-NotContains 'Bold Plan no longer grants energy' $boldPlan 'EnergyVar|GainEnergy'
Assert-Contains 'Bold Plan exposes the Adaptation enchantment hover tip' $boldPlan 'FromEnchantment<SGE_Adaptation>'
Assert-Contains 'Getter Two Bold Plan enchants one selected hand card with Adaptation' $boldPlan 'FromHand[\s\S]{0,520}Enchant<SGE_Adaptation>'
Assert-Contains 'Mandala enchantment branches allow zero to three cards' $mandala 'CardSelectorPrefs\(CardSelectorPrefs\.EnchantSelectionPrompt,\s*0,\s*3\)'
Assert-Contains 'Mandala applies its enchantment to every selected card' $mandala 'foreach\s*\(CardModel card in cards\)'

$hurricaneIcon = Read-RepoFile 'images\atlases\power_atlas.sprites\s_g_p_hurricane_temporary_dexterity.tres'
Assert-Contains 'Hurricane temporary Dexterity uses custom status slot 36' $hurricaneIcon 'power_icons_atlas_shin_getter\.png[\s\S]*Rect2\(192,\s*256,\s*64,\s*64\)'
Assert-ImageSize 'images\powers\s_g_p_hurricane_temporary_dexterity.png' 256 256

$multiHitCards = @(
    'src\Models\Cards\SGC_GetterClaw.cs',
    'src\Models\Cards\SGC_LigerAssault.cs',
    'src\Models\Cards\SGC_SpiralDrill.cs',
    'src\Models\Cards\SGC_TomahawkFury.cs',
    'src\Models\Cards\SGC_ChangeAttack.cs',
    'src\Models\Cards\SGC_GetterChop.cs',
    'src\Models\Cards\SGC_GetterMissile.cs',
    'src\Models\Cards\SGC_FocusFire.cs'
)
foreach ($path in $multiHitCards) {
    $card = Read-RepoFile $path
    Assert-Contains "$path accelerates follow-up attack animations" $card 'Accelerat|Followup'
}

$powerTransition = Read-RepoFile 'src\Patches\ShinGetterPowerUiPatch.cs'
Assert-Contains 'Power transition intercepts container removal' $powerTransition 'HarmonyPatch\(typeof\(NPowerContainer\),\s*"Remove"\)'
Assert-Contains 'Power transition intercepts container addition' $powerTransition 'HarmonyPatch\(typeof\(NPowerContainer\),\s*"Add"\)'
Assert-Contains 'Power transition reuses the existing NPower node' $powerTransition 'retainedNode\.Model\s*=\s*power'
Assert-Contains 'Power transition crossfades old and new icons in place' $powerTransition 'PreviousFormIconOverlay[\s\S]*modulate:a'

foreach ($language in @('zhs', 'eng', 'jpn')) {
    $cards = Read-Json "ShinGetterMod\localization\$language\cards.json"
    $events = Read-Json "ShinGetterMod\localization\$language\events.json"
    Assert-JsonNotContains "$language Insect-Human Virus does not duplicate Unplayable" $cards 'S_G_C_INSECT_VIRUS.description' '不可打出|Unplayable|プレイ不可'
    Assert-JsonContains "$language Getter Launch uses the dynamic Ki value" $cards 'S_G_C_GETTER_LAUNCH.description' '\{SGP_Ki:diff\(\)\}'
    Assert-JsonNotContains "$language Bold Plan no longer describes energy gain" $cards 'S_G_C_BOLD_PLAN.description' 'energyIcons|能量|エナジー'
    Assert-JsonContains "$language Bold Plan describes Getter Two Adaptation" $cards 'S_G_C_BOLD_PLAN.description' '二号机|Getter 2|ゲッター2'
    Assert-JsonContains "$language Mandala Primal Getter says up to three" $events 'S_G_E_GETTER_MANDALA.pages.INITIAL.options.PRIMAL_GETTER.description' '3'
    Assert-JsonContains "$language Mandala First Evolution says up to three" $events 'S_G_E_GETTER_MANDALA.pages.INITIAL.options.FIRST_EVOLUTION.description' '3'
}

$zhsEvents = Read-Json 'ShinGetterMod\localization\zhs\events.json'
Assert-JsonContains 'User-authored Mandala opening copy is preserved' $zhsEvents 'S_G_E_GETTER_MANDALA.pages.INITIAL.description' '命运的无数分支'

$manifest = Read-Json 'ShinGetterMod.json'
Assert-JsonContains 'Manifest advances to v0.9.39' $manifest 'version' '^v0\.9\.39$'

if ($Failures.Count -gt 0) {
    Write-Host 'FAILED v0.9.39 2026-07-14 prelaunch reopen checks:' -ForegroundColor Red
    foreach ($failure in $Failures) { Write-Host " - $failure" -ForegroundColor Red }
    exit 1
}

Write-Host 'PASSED v0.9.39 2026-07-14 prelaunch reopen checks.' -ForegroundColor Green
