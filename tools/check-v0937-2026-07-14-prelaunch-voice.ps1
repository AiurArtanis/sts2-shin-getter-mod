$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $projectRoot "ShinGetterMod.json"
$servicePath = Join-Path $projectRoot "src\Audio\ShinGetterVoiceService.cs"
$relicPath = Join-Path $projectRoot "src\Models\Relics\SGR_GetterFurnace.cs"
$fragmentPath = Join-Path $projectRoot "src\Models\Relics\SGR_EmperorsFragment.cs"
$eventPath = Join-Path $projectRoot "src\Models\Events\SGE_GetterMandala.cs"
$ancientRewardPatchPath = Join-Path $projectRoot "src\Patches\ShinGetterAncientRewardPatch.cs"
$cardBasePath = Join-Path $projectRoot "src\Models\Cards\ShinGetterCardBase.cs"
$shinFormPath = Join-Path $projectRoot "src\Models\Cards\SGC_ShinForm.cs"
$characterPath = Join-Path $projectRoot "src\Models\Characters\ShinGetter.cs"
$selectPatchPath = Join-Path $projectRoot "src\Patches\ShinGetterCharacterSelectAudioPatch.cs"
$validatorPath = Join-Path $projectRoot "tools\validate-mod-resources.gd"
$audioRoot = Join-Path $projectRoot "audio\sfx\characters\shin_getter\voices"

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

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Assert-True -Condition ($manifest.version -eq "v0.9.37") -Message "Manifest version must be v0.9.37."

$requiredAudio = @(
    "transform.wav",
    "change_getter_1.wav",
    "change_getter_1_switch_on.wav",
    "change_getter_2.wav",
    "change_getter_3.wav",
    "change_shin_dragon.wav",
    "ryoma_combine_blind.wav",
    "ryoma_getter_beam.wav",
    "ryoma_getter_tomahawk.wav",
    "ryoma_ora_ora_ora.wav",
    "ryoma_return_the_favor.wav",
    "ryoma_roar.wav",
    "ryoma_stay_to_the_end.wav",
    "ryoma_star_slash.wav",
    "ryoma_shining.wav",
    "team_spark.wav",
    "ryoma_getter_shine.wav",
    "hot_blood.wav",
    "musashi_avalanche.wav",
    "musashi_getter_electric.wav",
    "musashi_getter_power.wav",
    "musashi_fire_now.wav",
    "hayato_getter_drill.wav",
    "hayato_supersonic.wav",
    "hayato_drill_hurricane.wav",
    "hayato_drill_arm.wav"
)

foreach ($audioName in $requiredAudio) {
    $audioPath = Join-Path $audioRoot $audioName
    Assert-True -Condition (Test-Path -LiteralPath $audioPath -PathType Leaf) -Message "Missing voice resource: $audioName"
}

Assert-True -Condition (Test-Path -LiteralPath $servicePath -PathType Leaf) -Message "Voice service source is missing."
$service = Get-Content -LiteralPath $servicePath -Raw
$relic = Get-Content -LiteralPath $relicPath -Raw
$fragment = Get-Content -LiteralPath $fragmentPath -Raw
$event = Get-Content -LiteralPath $eventPath -Raw
$ancientRewardPatch = Get-Content -LiteralPath $ancientRewardPatchPath -Raw
$cardBase = Get-Content -LiteralPath $cardBasePath -Raw
$shinForm = Get-Content -LiteralPath $shinFormPath -Raw
$character = Get-Content -LiteralPath $characterPath -Raw
$selectPatch = Get-Content -LiteralPath $selectPatchPath -Raw
$validator = Get-Content -LiteralPath $validatorPath -Raw

Assert-Contains $service "TalkCmd.Play" "Voice subtitles must use the native speech bubble command."
Assert-Contains $service "Cmd.Wait(0.2f)" "Shining Spark follow-up must wait 0.2 seconds."
Assert-Contains $service "ChangeGetterOne = 0" "Persisted voice cue values must be explicit and stable."
Assert-Contains $service "DrillArm = 23" "Persisted voice cue values must remain within the int mask."
Assert-Contains $service "GetVoiceBit" "Voice cue bit shifts must validate their range."
Assert-Contains $service "RunState.Players" "One-time cues must be shared across the run's Shin Getter players."
Assert-Contains $service "SGR_EmperorsFragment" "Voice history must remain available after the starter relic is upgraded."
Assert-Contains $service "TryClaimVoiceCue" "Synchronized gameplay must claim each one-time cue deterministically."
Assert-Contains $service "SGC_FinalGetterBeam" "Final Getter Beam must trigger the Getter Beam cue."
Assert-Contains $service "SGC_ShiningSpark" "Shining Spark must trigger its two-part cue."
Assert-Contains $service "ShinGetterForm.Getter1" "Getter One form gating is missing."
Assert-Contains $service "ShinGetterForm.Getter2" "Getter Two form gating is missing."
Assert-Contains $service "ShinGetterForm.Getter3" "Getter Three form gating is missing."
Assert-Contains $service "PlayTransform" "Repeatable transform audio entry point is missing."

Assert-Contains $relic "[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]" "Voice history must survive save/load."
Assert-Contains $relic "PlayedVoiceMask" "Starter relic must persist the played voice bit mask."
Assert-Contains $relic "public int PlayedVoiceMask" "SavedProperty only supports an int voice bit mask."
Assert-Contains $relic "PlayCombatStart" "Combat-start voice hook is missing."
Assert-Contains $fragment "[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]" "Upgraded relic must preserve voice history across save/load."
Assert-Contains $fragment "public int PlayedVoiceMask" "Upgraded relic must use the supported int voice bit mask."
Assert-Contains $fragment "CreateFrom" "Upgraded relic must provide a history-preserving replacement factory."
Assert-Contains $event "SGR_EmperorsFragment.CreateFrom(getterFurnace)" "Getter Mandala replacement must copy voice history."
Assert-Contains $ancientRewardPatch "SGR_EmperorsFragment.CreateFrom(getterFurnace)" "Ancient reward replacement must copy voice history."
Assert-Contains $cardBase "TryPlayCardVoice" "Card animation entry points must trigger card voices."
Assert-Contains $cardBase "public override Task OnEnqueuePlayVfx(Creature? target) => Task.CompletedTask;" "Local-only enqueue VFX must not mutate run-global voice history."
Assert-Contains $cardBase "if (!MovementVfxTimingCards.Contains(GetType().Name))" "Ordinary card voice and animation must run from the synchronized BeforeCardPlayed hook."
Assert-True -Condition ([regex]::IsMatch(
    $cardBase,
    'MovementVfxTimingCards[\s\S]*?"SGC_HolyDragonRoar"[\s\S]*?};',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) -Message "Holy Dragon Roar must keep its manually timed Cast animation out of the common card animation path."
Assert-Contains $cardBase "PlayTransform" "Normal transformations must trigger transform audio."
Assert-Contains $shinForm "PlayShinDragonTransform" "Shin Dragon transformation voice hook is missing."

$transformResourcePath = "res://audio/sfx/characters/shin_getter/voices/transform.wav"
Assert-Contains $character $transformResourcePath "Character selection must use transform.wav."
Assert-Contains $selectPatch $transformResourcePath "Character-selection audio patch must intercept transform.wav."

foreach ($audioName in $requiredAudio) {
    Assert-Contains $validator "res://audio/sfx/characters/shin_getter/voices/$audioName" "Resource validator does not cover $audioName."
}

$localizationKeys = @(
    "SHIN_GETTER.voice.changeGetterOne",
    "SHIN_GETTER.voice.changeGetterTwo",
    "SHIN_GETTER.voice.changeGetterThree",
    "SHIN_GETTER.voice.changeShinDragon",
    "SHIN_GETTER.voice.combineBlind",
    "SHIN_GETTER.voice.getterBeam",
    "SHIN_GETTER.voice.getterTomahawk",
    "SHIN_GETTER.voice.oraOraOra",
    "SHIN_GETTER.voice.returnTheFavor",
    "SHIN_GETTER.voice.roar",
    "SHIN_GETTER.voice.stayToTheEnd",
    "SHIN_GETTER.voice.starSlash",
    "SHIN_GETTER.voice.shining",
    "SHIN_GETTER.voice.spark",
    "SHIN_GETTER.voice.getterShine",
    "SHIN_GETTER.voice.hotBlood",
    "SHIN_GETTER.voice.avalanche",
    "SHIN_GETTER.voice.getterElectric",
    "SHIN_GETTER.voice.getterPower",
    "SHIN_GETTER.voice.fireNow",
    "SHIN_GETTER.voice.getterDrill",
    "SHIN_GETTER.voice.supersonic",
    "SHIN_GETTER.voice.drillHurricane",
    "SHIN_GETTER.voice.drillArm"
)

foreach ($locale in @("eng", "jpn", "zhs")) {
    $charactersPath = Join-Path $projectRoot "ShinGetterMod\localization\$locale\characters.json"
    $characters = Get-Content -LiteralPath $charactersPath -Raw | ConvertFrom-Json -AsHashtable
    foreach ($key in $localizationKeys) {
        Assert-True -Condition $characters.ContainsKey($key) -Message "Missing $locale localization key: $key"
        Assert-True -Condition (-not [string]::IsNullOrWhiteSpace([string]$characters[$key])) -Message "Blank $locale localization value: $key"
    }
}

Write-Host "PASSED v0.9.37 prelaunch voice checks."
