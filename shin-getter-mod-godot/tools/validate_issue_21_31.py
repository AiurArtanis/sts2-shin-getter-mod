from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SERVICE_PATH = ROOT / "src" / "Audio" / "ShinGetterVoiceService.cs"
VOICE_DIR = ROOT / "audio" / "sfx" / "characters" / "shin_getter" / "voices"
PCK_VALIDATOR_PATH = ROOT / "tools" / "validate-mod-resources.gd"
RICH_TEXT_PATCH_PATH = ROOT / "src" / "Patches" / "RichTextWhitePatch.cs"
PINK_EFFECT_PATH = ROOT / "src" / "RichTextTags" / "RichTextPink.cs"
BLACK_EFFECT_PATH = ROOT / "src" / "RichTextTags" / "RichTextBlack.cs"

EXPECTED_AUDIO = {
    "001": "change_getter_1.wav",
    "002": "change_getter_1_switch_on.wav",
    "003": "switch_on.wav",
    "004": "change_getter_2.wav",
    "005": "change_getter_3.wav",
    "006": "change_shin_dragon.wav",
    "007": "transform.wav",
    "008": "ryoma_combine_blind.wav",
    "009": "ryoma_getter_beam.wav",
    "010": "ryoma_getter_tomahawk.wav",
    "011": "ryoma_ora_ora_ora.wav",
    "012": "ryoma_battle_wing.wav",
    "013": "ryoma_getter_squad.wav",
    "014": "ryoma_roar.wav",
    "015": "ryoma_stay_to_the_end.wav",
    "016": "ryoma_star_slash.wav",
    "017": "ryoma_stoner_sunshine.wav",
    "018": "ryoma_shining.wav",
    "019": "team_spark.wav",
    "020": "ryoma_getter_ray_surge.wav",
    "021": "ryoma_getter_shine.wav",
    "022": "ryoma_kill_finish.wav",
    "023": "ryoma_kill_grunts.wav",
    "024": "ryoma_kill_guillotine.wav",
    "025": "ryoma_enemy_summon.wav",
    "026": "ryoma_first_damage.wav",
    "027": "ryoma_lizard_encounter.wav",
    "028": "ryoma_no_hp_loss.wav",
    "029": "ryoma_event_combat.wav",
    "030": "ryoma_elite_respect.wav",
    "031": "ryoma_elite_prepare.wav",
    "032": "ryoma_elite_finally.wav",
    "033": "ryoma_boss_big.wav",
    "034": "hot_blood.wav",
    "035": "musashi_avalanche.wav",
    "036": "musashi_getter_missile.wav",
    "037": "musashi_getter_electric.wav",
    "038": "musashi_getter_power.wav",
    "039": "musashi_fire_now.wav",
    "040": "musashi_special_move.wav",
    "041": "musashi_kill.wav",
    "042": "hayato_getter_drill.wav",
    "043": "hayato_supersonic.wav",
    "044": "hayato_drill_hurricane.wav",
    "045": "hayato_drill_hurricane_long.wav",
    "046": "hayato_drill_arm.wav",
    "047": "hayato_kill.wav",
}

EXPECTED_LATER_AUDIO = {
    "058": "ryoma_open_get.wav",
    "059": "hayato_open_get.wav",
    "060": "benkei_open_get.wav",
    "061": "ryoma_feel_getter_power.wav",
    "062": "ryoma_three_hearts_one.wav",
    "063": "ryoma_our_will_getter_power.wav",
    "064": "hayato_unite_hearts.wav",
    "065": "benkei_use_stoner_sunshine.wav",
}
ALL_EXPECTED_AUDIO = EXPECTED_AUDIO | EXPECTED_LATER_AUDIO


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


service = SERVICE_PATH.read_text(encoding="utf-8")
rows = dict(re.findall(r'new\("(\d{3})",[^\n]*?"([^"\n]+\.wav)"', service))
require(
    {code: rows.get(code) for code in EXPECTED_AUDIO} == EXPECTED_AUDIO,
    "001-047 voice code mapping does not match the authoritative workbook",
)
require(rows == ALL_EXPECTED_AUDIO, "voice mapping must contain only 001-047 and issue#10 codes 058-065")

for code, file_name in EXPECTED_AUDIO.items():
    audio = VOICE_DIR / file_name
    require(audio.is_file(), f"missing audio for {code}: {file_name}")
    payload = audio.read_bytes()
    require(len(payload) > 44, f"empty WAV for {code}: {file_name}")
    require(payload[:4] == b"RIFF" and payload[8:12] == b"WAVE", f"invalid WAV for {code}: {file_name}")
    import_path = VOICE_DIR / f"{file_name}.import"
    require(import_path.is_file(), f"missing Godot import sidecar for {code}: {file_name}")
    import_text = import_path.read_text(encoding="utf-8")
    require(
        f'source_file="res://audio/sfx/characters/shin_getter/voices/{file_name}"' in import_text,
        f"incorrect Godot source path for {code}: {file_name}",
    )
    require("compress/mode=2" in import_text, f"unexpected WAV compression mode for {code}: {file_name}")

actual_audio = {path.name for path in VOICE_DIR.glob("*.wav")}
require(
    actual_audio == set(ALL_EXPECTED_AUDIO.values()),
    "voice directory must contain the 47 workbook WAVs plus issue#10 codes 058-065",
)
actual_imports = {path.name for path in VOICE_DIR.glob("*.wav.import")}
require(
    actual_imports == {f"{name}.import" for name in ALL_EXPECTED_AUDIO.values()},
    "voice directory must contain matching import sidecars for 001-047 and 058-065",
)

pck_validator = PCK_VALIDATOR_PATH.read_text(encoding="utf-8")
pck_voice_resources = set(
    re.findall(
        r'"res://audio/sfx/characters/shin_getter/voices/([^"/]+\.wav)": false',
        pck_validator,
    )
)
require(
    pck_voice_resources == set(ALL_EXPECTED_AUDIO.values()),
    "PCK validator voice resources must match 001-047 and issue#10 codes 058-065 exactly",
)

required_card_mappings = (
    "SGC_GetterTomahawk => Lines[ShinGetterVoiceCue.GetterTomahawk]",
    "SGC_GetterFlash or SGC_DiveStrike => Lines[ShinGetterVoiceCue.BattleWing]",
    "SGC_BlackArmor or SGC_DarkCape => Lines[ShinGetterVoiceCue.GetterSquad]",
    "SGC_Spirit or SGC_SuperKi or SGC_AwakenedSoul => Lines[ShinGetterVoiceCue.Roar]",
    "SGC_StonerSunshine => Lines[ShinGetterVoiceCue.StonerSunshine]",
    "SGC_GetterWill or SGC_GetterRayOverflow => Lines[ShinGetterVoiceCue.GetterRaySurge]",
    "SGC_HolyDragonRoar or SGC_GetterNova => Lines[ShinGetterVoiceCue.GetterShine]",
    "SGC_GetterMissile => Lines[ShinGetterVoiceCue.GetterMissile]",
    "SGC_ExpansionStrike or SGC_GetterElbow => Lines[ShinGetterVoiceCue.FireNow]",
    "SGC_Grapple => Lines[ShinGetterVoiceCue.MusashiSpecialMove]",
    "SGC_HurricaneStrike => Lines[ShinGetterVoiceCue.DrillHurricane]",
    "SGC_LigerAssault => Lines[ShinGetterVoiceCue.DrillHurricaneLong]",
)
for mapping in required_card_mappings:
    require(mapping in service, f"missing workbook card mapping: {mapping}")

for custom_card in ("SGC_GetterWill", "SGC_HolyDragonRoar", "SGC_PoseidonThunder"):
    require(custom_card in service.split("UsesCustomCardVoiceTiming", 1)[1], f"missing custom voice timing: {custom_card}")

getter_will = (ROOT / "src" / "Models" / "Cards" / "SGC_GetterWill.cs").read_text(encoding="utf-8")
require(
    getter_will.index("await CardPileCmd.Add")
    < getter_will.index("TryPlayCardVoiceAtCustomTiming")
    < getter_will.index("TryPlayCreatureActionAnimation")
    < getter_will.index("PowerCmd.Apply<SGP_Evolution>"),
    "Getter Will voice and Cast animation must play together after card selection and before its form bonus",
)
card_base = (ROOT / "src" / "Models" / "Cards" / "ShinGetterCardBase.cs").read_text(encoding="utf-8")
movement_timing_cards = card_base.split("MovementVfxTimingCards", 1)[1].split("BlockAnimationCards", 1)[0]
require('"SGC_GetterWill"' in movement_timing_cards, "Getter Will's automatic pre-selection animation must be deferred")

holy_dragon_roar = (ROOT / "src" / "Models" / "Cards" / "SGC_HolyDragonRoar.cs").read_text(encoding="utf-8")
for timing_guard in (
    "if (getterCards.Count < 3)",
    "await CardCmd.Exhaust(choiceContext, getterCards[index])",
    "if (getterCards.Count >= 3 && index == getterCards.Count - 3)",
    "ShinGetterVoiceService.TryPlayCardVoiceAtCustomTiming(this, out _)",
):
    require(timing_guard in holy_dragon_roar, f"Holy Dragon Roar timing guard is missing: {timing_guard}")
require(
    holy_dragon_roar.index("if (getterCards.Count < 3)")
    < holy_dragon_roar.index("if (getterCards.Count >= 3 && index == getterCards.Count - 3)")
    < holy_dragon_roar.index("await CardCmd.Exhaust"),
    "Holy Dragon Roar voice must play immediately below three cards or when the third-to-last exhaust starts",
)
require("index == getterCards.Count - 1" not in holy_dragon_roar, "Holy Dragon Roar must not wait for the last exhaust")

poseidon_thunder = (ROOT / "src" / "Models" / "Cards" / "SGC_PoseidonThunder.cs").read_text(encoding="utf-8")
require(
    poseidon_thunder.index("TryPlayCardVoiceAtCustomTiming")
    < poseidon_thunder.index("voiceDurationSeconds * 3f / 5f")
    < poseidon_thunder.index("ShinGetterCombatVfx.PlayThunderField"),
    "Poseidon Thunder VFX must wait until three fifths of a triggered voice line",
)

require(
    "SGC_GetterTomahawk or SGC_GetterFlash" not in service,
    "Getter Tomahawk must not absorb Battle Wing cards",
)
require(
    "SGC_GetterNova or SGC_GetterRayOverflow => Lines[ShinGetterVoiceCue.Roar]" not in service,
    "Roar must not absorb Getter Shine or Getter Ray Surge cards",
)
require(
    "SGC_ExpansionStrike or SGC_GetterElbow or SGC_GetterMissile" not in service,
    "Getter Missile must use Musashi's dedicated line",
)

for trigger in (
    "result.WasTargetKilled",
    "target.CombatState.Enemies.Any(enemy => enemy.IsAlive)",
    "OnEnemySummoned",
    "HasHandledFirstDamage",
    "result.UnblockedDamage > 0",
    "ValueProp.Move",
    "MapPointType.Unknown",
    "HunterKiller or TestSubject",
    "RoomType.Elite",
    "RoomType.Boss",
):
    require(trigger in service, f"missing voice trigger boundary: {trigger}")

kill_handler = service.split("internal static void OnAfterDamageGiven", 1)[1].split(
    "internal static void OnAfterDamageReceived", 1
)[0]
require(
    "TryQueueRandomOneTimeAfterCurrentVoice(player, pool)" in kill_handler,
    "kill voices must enter the deferred queue",
)
require("TryPlayRandomOneTime(player, pool)" not in kill_handler, "kill voices must not play immediately")

queue_method = service.split("private static bool TryQueueRandomOneTimeAfterCurrentVoice", 1)[1].split(
    "private static void TryStartNextQueuedKillVoice", 1
)[0]
require(
    queue_method.index("TryClaimVoiceCue(player, cue)")
    < queue_method.index("PendingKillVoiceLines.Enqueue(selected)")
    < queue_method.index("TryStartNextQueuedKillVoice(player, state)"),
    "kill voice cues must be claimed before they are queued and drained",
)

drain_method = service.split("private static void TryStartNextQueuedKillVoice", 1)[1].split(
    "private static bool TryPlayOpeningPool", 1
)[0]
for queue_guard in (
    "CombatManager.Instance.IsInProgress",
    "state.PendingKillVoiceLines.Clear()",
    "!state.IsStoppingVoiceAudio",
    "state.ActiveVoicePlayers.Count == 0",
    "state.PendingKillVoiceLines.TryDequeue",
    "TryPlayLine(player, line, out _, ignoreRequiredForm: true)",
):
    require(queue_guard in drain_method, f"kill voice queue guard is missing: {queue_guard}")
require("Cmd.Wait" not in drain_method, "kill voice queue must wait for actual player completion, not a fixed delay")
require(
    drain_method.index("CombatManager.Instance.IsInProgress")
    < drain_method.index("state.ActiveVoicePlayers.Count == 0"),
    "queued kill voices must be discarded before draining after combat ends",
)

finished_callback = service.split("audioPlayer.Finished += () =>", 1)[1].split("sceneTree.Root.AddChild", 1)[0]
require(
    finished_callback.index("state.ActiveVoicePlayers.Remove(audioPlayer)")
    < finished_callback.index("TryStartNextQueuedKillVoice(player, state)"),
    "the next kill voice must start only after the finished player is removed",
)
require(
    "state.PendingKillVoiceLines.Clear();\n            StopAllVoiceAudio(state);" in service,
    "combat reset must clear pending kill voices before stopping audio",
)
require(
    "public readonly Queue<VoiceLine> PendingKillVoiceLines = new();" in service,
    "per-player kill voice queue is missing",
)
stop_method = service.split("private static void StopAllVoiceAudio", 1)[1].split(
    "private static void PlaySubtitle", 1
)[0]
for stop_guard in (
    "state.IsStoppingVoiceAudio = true;",
    "finally",
    "state.ActiveVoicePlayers.Clear();",
    "state.IsStoppingVoiceAudio = false;",
):
    require(stop_guard in stop_method, f"voice stop reentrancy guard is missing: {stop_guard}")
require(
    stop_method.index("state.IsStoppingVoiceAudio = true;")
    < stop_method.index("audioPlayer.Stop();")
    < stop_method.index("state.ActiveVoicePlayers.Clear();")
    < stop_method.index("state.IsStoppingVoiceAudio = false;"),
    "voice stop guard must cover the entire player cleanup",
)
require("public bool IsStoppingVoiceAudio;" in service, "per-player voice stop guard is missing")

combat_patch = (ROOT / "src" / "Patches" / "ShinGetterCombatStartVoicePatch.cs").read_text(encoding="utf-8")
require("PrepareCombatStart" in combat_patch, "combat start must prepare voice state before the combat UI is ready")
require("CreatureCmd" in combat_patch and "OnEnemySummoned" in combat_patch, "enemy summon patch is missing")

for relic_name in ("SGR_GetterFurnace.cs", "SGR_EmperorsFragment.cs"):
    relic = (ROOT / "src" / "Models" / "Relics" / relic_name).read_text(encoding="utf-8")
    for member in ("PlayedVoiceMaskHigh", "OpeningVoiceMask", "PlayPreparedCombatStart", "OnAfterDamageGiven", "OnAfterDamageReceived"):
        require(member in relic, f"{relic_name} is missing {member}")

fragment = (ROOT / "src" / "Models" / "Relics" / "SGR_EmperorsFragment.cs").read_text(encoding="utf-8")
require(
    "fragment.PlayedVoiceMaskHigh = getterFurnace.PlayedVoiceMaskHigh" in fragment
    and "fragment.OpeningVoiceMask = getterFurnace.OpeningVoiceMask" in fragment,
    "Emperor's Fragment must inherit both new voice masks",
)

for timing_guard in (
    "VfxDuration.Forever",
    "audioDurationSeconds + SubtitleTailSeconds",
    "category == VoicePlaybackCategory.Opening",
    "StopAllVoiceAudio(state)",
    "SubtitleGeneration",
    "subtitle.AnimOut()",
):
    require(timing_guard in service, f"subtitle/audio lifecycle guard is missing: {timing_guard}")
require("ShiningSparkIntroDurationSeconds" not in service, "Shining Spark must use the imported stream duration")
require("ShiningSparkFollowUpDurationSeconds" not in service, "Spark follow-up must use the imported stream duration")

console_patch = (ROOT / "src" / "Patches" / "ShinGetterConsoleCommandPatch.cs").read_text(encoding="utf-8")
console_cmd = (ROOT / "src" / "Diagnostics" / "ShinGetterChunibyoConsoleCmd.cs").read_text(encoding="utf-8")
require('ShinGetterSoundCommandName = "sgs"' in console_patch, "sgs command routing is missing")
require(
    'Args => "<001-047|058-065>"' in console_cmd and "TryPlayCode" in console_cmd,
    "sgs 001-047 and 058-065 command is incomplete",
)

service_keys = set(re.findall(r'"(SHIN_GETTER\.voice\.[A-Za-z0-9]+)"', service))
require(len(service_keys) == 54, f"expected 54 subtitle keys, found {len(service_keys)}")
language_voice_keys: dict[str, set[str]] = {}
language_data: dict[str, dict[str, str]] = {}
for language in ("zhs", "eng", "jpn"):
    path = ROOT / "ShinGetterMod" / "localization" / language / "characters.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    language_data[language] = data
    language_voice_keys[language] = {key for key in data if key.startswith("SHIN_GETTER.voice.")}
    require(service_keys <= language_voice_keys[language], f"{language} is missing voice subtitle keys")

require(
    language_voice_keys["zhs"] == language_voice_keys["eng"] == language_voice_keys["jpn"],
    "trilingual voice key sets differ",
)

expected_shared_subtitles = {
    "SHIN_GETTER.voice.combatStartFirst": "CHANGE ! [red]GETTER ONE[/red] !\nSwitch On",
    "SHIN_GETTER.voice.getterBeam": "Getter [pink]Beeeeeeeeeam[/pink] !",
    "SHIN_GETTER.voice.battleWing": "Battle [black]Wiiiiing[/black]",
    "SHIN_GETTER.voice.stonerSunshine": "[red]Stoner[/red]\n[yellow]Sunshine[/yellow]",
    "SHIN_GETTER.voice.drillArm": "[white]Drill Arrrrrm[/white]",
}
for language, data in language_data.items():
    for key, expected in expected_shared_subtitles.items():
        require(data.get(key) == expected, f"{language} workbook subtitle mismatch: {key}")
    require("[gold]" in data["SHIN_GETTER.voice.combineBlind"], f"{language} combine subtitle must use [gold]")
    require("[/gold]" in data["SHIN_GETTER.voice.combineBlind"], f"{language} combine subtitle must close [gold]")
    require(
        [key for key, value in data.items() if key.startswith("SHIN_GETTER.voice.") and "[black]" in value]
        == ["SHIN_GETTER.voice.battleWing"],
        f"{language} voice subtitles must use [black] only for Battle Wing",
    )

pink_effect = PINK_EFFECT_PATH.read_text(encoding="utf-8")
require('Bbcode => "pink"' in pink_effect, "pink MegaText tag is not registered")
require('new Color("FF69B4")' in pink_effect, "pink MegaText color must remain #FF69B4")
rich_text_patch = RICH_TEXT_PATCH_PATH.read_text(encoding="utf-8")
require("RichTextPink PinkEffect" in rich_text_patch, "pink effect instance is missing")
require('effect.bbcode == "pink"' in rich_text_patch, "existing pink effects must be identified by bbcode")
require("!ReferenceEquals(effect, PinkEffect)" in rich_text_patch, "the mod pink effect must survive replacement")
require("CustomEffects.RemoveAt(i)" in rich_text_patch, "the built-in pink effect must be removed before replacement")
require("CustomEffects.Add(PinkEffect)" in rich_text_patch, "pink effect is not installed on MegaText labels")
require(
    rich_text_patch.index('effect.bbcode == "pink"') < rich_text_patch.index("CustomEffects.Add(PinkEffect)"),
    "existing pink effects must be removed before the mod pink effect is installed",
)

black_effect = BLACK_EFFECT_PATH.read_text(encoding="utf-8")
require('Bbcode => "black"' in black_effect, "black MegaText tag is not registered")
require('new Color("000000")' in black_effect, "black MegaText color must remain #000000")
require("RichTextBlack BlackEffect" in rich_text_patch, "black effect instance is missing")
require('effect.bbcode == "black"' in rich_text_patch, "existing black effects must be identified by bbcode")
require("!ReferenceEquals(effect, BlackEffect)" in rich_text_patch, "the mod black effect must survive replacement")
require("CustomEffects.Add(BlackEffect)" in rich_text_patch, "black effect is not installed on MegaText labels")
require(
    rich_text_patch.index('effect.bbcode == "black"') < rich_text_patch.index("CustomEffects.Add(BlackEffect)"),
    "existing black effects must be removed before the mod black effect is installed",
)

print("issue#21/#31 static validation PASS: 47 workbook codes plus issue#10 codes 058-065, triggers, timing, sgs, trilingual keys")
