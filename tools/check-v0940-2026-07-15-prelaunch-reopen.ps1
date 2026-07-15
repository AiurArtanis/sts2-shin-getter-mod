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

function Assert-File([string] $relativePath, [long] $minimumBytes) {
    $path = Join-Path $Root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $Failures.Add("Missing file: $relativePath")
        return
    }
    if ((Get-Item -LiteralPath $path).Length -lt $minimumBytes) {
        $Failures.Add("File is unexpectedly small: $relativePath")
    }
}

$scene = Read-RepoFile 'scenes\screens\char_select\char_select_bg_shin_getter.tscn'
Assert-Contains 'Character-select root fills its parent' $scene 'ShinGetterBg[\s\S]{0,220}anchors_preset\s*=\s*15[\s\S]{0,120}anchor_right\s*=\s*1\.0[\s\S]{0,120}anchor_bottom\s*=\s*1\.0'
Assert-Contains 'Character-select portrait keeps aspect covered' $scene 'Portrait[\s\S]{0,340}anchors_preset\s*=\s*15[\s\S]{0,260}stretch_mode\s*=\s*6'
Assert-NotContains 'Character-select background has no obsolete 2560x1200 crop' $scene '2560\.0|1200\.0|-523\.0'

$ancient = Read-RepoFile 'src\Patches\ShinGetterAncientDialoguePatch.cs'
Assert-Contains 'Shin Getter exact Ancient visit dialogue takes priority' $ancient 'exactVisit[\s\S]{0,280}__result\s*=\s*exactVisit'
Assert-Contains 'Shin Getter repeating Ancient dialogue is used after exact visits' $ancient 'repeating[\s\S]{0,320}repeating\.Count\s*>\s*0'
Assert-Contains 'Shin Getter Ancient dialogue always has a cyclic fallback' $ancient 'dialogues\[charVisits\s*%\s*dialogues\.Count\]'

$creatureScene = Read-RepoFile 'scenes\creature_visuals\shin_getter.tscn'
Assert-Contains 'Getter Two is moved down five percent' $creatureScene 'GetterTwo[\s\S]{0,420}position\s*=\s*Vector2\(34,\s*-210\.672\)'
Assert-Contains 'Shin Dragon is enlarged five percent and moved up three percent' $creatureScene 'ShinDragon[\s\S]{0,420}position\s*=\s*Vector2\(0,\s*-218\.8956\)[\s\S]{0,120}scale\s*=\s*Vector2\(0\.72765,\s*0\.72765\)'

$beam = Read-RepoFile 'src\Nodes\Vfx\ShinGetterBeamVfx.cs'
Assert-Contains 'Final Getter Beam outer layer is ten percent wider than v0.9.39' $beam 'line\.Width\s*\*=\s*1\.43f'
Assert-Contains 'Final Getter Beam center is twice as wide' $beam 'Scale\s*=\s*new Vector2\(1f,\s*0\.70f\)'
$combatVfx = Read-RepoFile 'src\Nodes\Vfx\ShinGetterCombatVfx.cs'
Assert-Contains 'Star Slash adds a full-screen excessive shake' $combatVfx 'PlayHeavyCleave[\s\S]{0,1200}ScreenShake\(ShakeStrength\.TooMuch,\s*ShakeDuration\.Normal\)'

$voice = Read-RepoFile 'src\Audio\ShinGetterVoiceService.cs'
$voicePatch = Read-RepoFile 'src\Patches\ShinGetterCardPlayVoicePatch.cs'
$combatVoice = Read-RepoFile 'src\Patches\ShinGetterCombatStartVoicePatch.cs'
Assert-Contains 'Later combats use the new switch-on cue' $voice 'ShinGetterVoiceCue\.SwitchOn[\s\S]{0,160}"switch_on\.wav"'
Assert-Contains 'The first combat retains the original long switch-on cue' $voice 'combatStartVoiceCount\s*==\s*0[\s\S]{0,160}ChangeGetterOneSwitchOn[\s\S]{0,120}SwitchOn'
Assert-Contains 'Combat-start voice count advances after playback selection' $voice 'SetCombatStartVoiceCount\(player,\s*combatStartVoiceCount\s*\+\s*1\)'
Assert-Contains 'Long card voices expose the card-play-start flag' $voice 'bool StartAtCardPlay\s*=\s*false'
foreach ($cue in @(
    'CombineBlind',
    'GetterBeam',
    'ReturnTheFavor',
    'Roar',
    'StayToTheEnd',
    'StarSlash',
    'GetterShine',
    'HotBlood',
    'Avalanche',
    'GetterElectric',
    'GetterPower',
    'FireNow',
    'DrillArm'
)) {
    Assert-Contains "$cue is marked for immediate card-play-start playback" $voice "ShinGetterVoiceCue\.$cue[\s\S]{0,240}StartAtCardPlay:\s*true"
}
Assert-Contains 'Card OnPlayWrapper has an immediate voice prefix' $voicePatch 'HarmonyPatch\(typeof\(CardModel\),\s*nameof\(CardModel\.OnPlayWrapper\)\)[\s\S]{0,260}TryPlayCardVoiceAtCardPlayStart'
Assert-Contains 'Combat start is idempotent per CombatState' $combatVoice 'HasPlayedCombatStartVoice[\s\S]{0,220}PlayCombatStart'

$cardBase = Read-RepoFile 'src\Models\Cards\ShinGetterCardBase.cs'
$shinForm = Read-RepoFile 'src\Models\Cards\SGC_ShinForm.cs'
Assert-Contains 'Normal transform audio precedes form power removal' $cardBase 'PlayTransform\(player,\s*next\)[\s\S]{0,260}PowerCmd\.Remove'
Assert-Contains 'Shin Dragon transform audio precedes form power removal' $shinForm 'PlayShinDragonTransform\(Owner\)[\s\S]{0,700}PowerCmd\.Remove'

$execution = Read-RepoFile 'src\Audio\ShinGetterExecutionMusicService.cs'
Assert-Contains 'Execution music watches all three requested cards' $execution 'SGC_StonerSunshine\s+or\s+SGC_StarSlash\s+or\s+SGC_ShiningSpark'
Assert-Contains 'Execution music only triggers on entering the hand' $execution 'card\.Pile\?\.Type\s*!=\s*PileType\.Hand'
Assert-Contains 'Execution music triggers at most once per combat' $execution 'state\.HasTriggered[\s\S]{0,160}state\.HasTriggered\s*=\s*true'
Assert-Contains 'Execution music fades the original BGM' $execution 'SetBgmVol\(volume\)'
Assert-Contains 'Execution music loops until combat ends' $execution 'player\.Finished[\s\S]{0,220}player\.Play\(\)'
Assert-Contains 'Execution music uses the imported track' $execution 'execution_theme\.mp3'

foreach ($relicPath in @(
    'src\Models\Relics\SGR_GetterFurnace.cs',
    'src\Models\Relics\SGR_EmperorsFragment.cs'
)) {
    $relic = Read-RepoFile $relicPath
    Assert-Contains "$relicPath persists combat-start count" $relic 'SavedProperty[\s\S]{0,180}CombatStartVoiceCount'
    Assert-Contains "$relicPath observes hand entry for execution music" $relic 'AfterCardChangedPiles[\s\S]{0,260}ShinGetterExecutionMusicService\.TryStart'
    Assert-Contains "$relicPath restores BGM after combat" $relic 'AfterCombatEnd[\s\S]{0,200}StopAndRestore'
}
$fragment = Read-RepoFile 'src\Models\Relics\SGR_EmperorsFragment.cs'
Assert-Contains 'Ancient relic replacement preserves combat-start count' $fragment 'fragment\.CombatStartVoiceCount\s*=\s*getterFurnace\.CombatStartVoiceCount'

$spark = Read-RepoFile 'src\Models\Cards\SGC_ShiningSpark.cs'
$starSlash = Read-RepoFile 'src\Models\Cards\SGC_StarSlash.cs'
Assert-Contains 'Shining Spark applies one Vulnerable' $spark 'Apply<VulnerablePower>[^\r\n]*1m'
Assert-Contains 'Shining Spark applies one Frail' $spark 'Apply<FrailPower>[^\r\n]*1m'
Assert-Contains 'Shining Spark remains 11 to 14 main damage' $spark 'DamageVar\(11m[\s\S]*Damage\.UpgradeValueBy\(3m\)'
Assert-Contains 'Shining Spark remains 6 to 9 Ki damage' $spark 'DynamicVar\("KiDamage",\s*6m\)[\s\S]*"KiDamage"\]\.UpgradeValueBy\(3m\)'
Assert-Contains 'Star Slash base damage is 25' $starSlash 'DamageVar\(25m'
Assert-Contains 'Star Slash grants five Vigor per exhausted card in Getter One' $starSlash 'foreach[\s\S]{0,500}HasForm\(Owner,\s*ShinGetterForm\.Getter1\)[\s\S]{0,320}Apply<VigorPower>'
Assert-NotContains 'Star Slash no longer re-adds all Vigor gained this combat' $starSlash 'PowerReceivedEntry|vigorGained'

foreach ($language in @('zhs', 'eng', 'jpn')) {
    $cards = Read-Json "ShinGetterMod\localization\$language\cards.json"
    Assert-JsonContains "$language Shining Spark describes one Vulnerable and Frail" $cards 'S_G_C_SHINING_SPARK.description' '1'
    Assert-JsonContains "$language Star Slash exposes dynamic Vigor" $cards 'S_G_C_STAR_SLASH.description' '\{Vigor:diff\(\)\}'
}

Assert-File 'audio\sfx\characters\shin_getter\voices\switch_on.wav' 100000
Assert-File 'audio\sfx\characters\shin_getter\voices\switch_on.wav.import' 100
Assert-File 'audio\music\shin_getter\execution_theme.mp3' 2000000
$musicImport = Read-RepoFile 'audio\music\shin_getter\execution_theme.mp3.import'
Assert-Contains 'Execution music has a valid Godot imported stream' $musicImport 'type="AudioStreamMP3"[\s\S]{0,160}path="res://\.godot/imported/'
Assert-NotContains 'Execution music import is not marked invalid' $musicImport 'valid=false'
$resourceValidator = Read-RepoFile 'tools\validate-mod-resources.gd'
Assert-Contains 'PCK validation loads the later-combat switch-on cue' $resourceValidator 'audio/sfx/characters/shin_getter/voices/switch_on\.wav'
Assert-Contains 'PCK validation loads the execution track' $resourceValidator 'audio/music/shin_getter/execution_theme\.mp3'

$formsRoot = Join-Path $Root 'images\characters\shin_getter\forms'
$animationDirectories = @(Get-ChildItem -LiteralPath $formsRoot -Directory)
$animationPngs = @(Get-ChildItem -LiteralPath $formsRoot -Recurse -File -Filter '*.png')
if ($animationDirectories.Count -ne 24) {
    $Failures.Add("Animation directory count is $($animationDirectories.Count), expected 24")
}
if ($animationPngs.Count -ne 993) {
    $Failures.Add("Animation PNG count is $($animationPngs.Count), expected 993")
}
$animationBytes = ($animationPngs | Measure-Object Length -Sum).Sum
if ($animationBytes -ge 80000000) {
    $Failures.Add("Compressed animation PNGs total $animationBytes bytes, expected less than 80000000")
}

Add-Type -AssemblyName System.Drawing.Common
foreach ($frame in $animationPngs) {
    $image = [System.Drawing.Image]::FromFile($frame.FullName)
    try {
        $expectedSize = if ($frame.Directory.Name -eq 'shin_getter_dragon_block') { 960 } else { 720 }
        if ($image.Width -ne $expectedSize -or $image.Height -ne $expectedSize) {
            $Failures.Add("Unexpected animation dimensions: $($frame.FullName)")
            break
        }
        if (($image.Flags -band [System.Drawing.Imaging.ImageFlags]::HasAlpha) -eq 0) {
            $Failures.Add("Animation frame lost alpha: $($frame.FullName)")
            break
        }
    } finally {
        $image.Dispose()
    }
}

$manifest = Read-Json 'ShinGetterMod.json'
Assert-JsonContains 'Manifest advances to v0.9.40' $manifest 'version' '^v0\.9\.40$'

if ($Failures.Count -gt 0) {
    Write-Host 'FAILED v0.9.40 2026-07-15 prelaunch reopen checks:' -ForegroundColor Red
    foreach ($failure in $Failures) { Write-Host " - $failure" -ForegroundColor Red }
    exit 1
}

Write-Host 'PASSED v0.9.40 2026-07-15 prelaunch reopen checks.' -ForegroundColor Green
