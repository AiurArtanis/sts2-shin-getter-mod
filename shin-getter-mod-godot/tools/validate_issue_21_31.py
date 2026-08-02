from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SERVICE_PATH = ROOT / "src" / "Audio" / "ShinGetterVoiceService.cs"
VOICE_DIR = ROOT / "audio" / "sfx" / "characters" / "shin_getter" / "voices"
PCK_VALIDATOR_PATH = ROOT / "tools" / "validate-mod-resources.gd"

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


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


service = SERVICE_PATH.read_text(encoding="utf-8")
rows = dict(re.findall(r'new\("(\d{3})",[^\n]*?"([^"\n]+\.wav)"', service))
require(rows == EXPECTED_AUDIO, "001-047 voice code mapping does not match the authoritative workbook")

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
require(actual_audio == set(EXPECTED_AUDIO.values()), "voice directory must contain exactly the 47 workbook WAVs")
actual_imports = {path.name for path in VOICE_DIR.glob("*.wav.import")}
require(
    actual_imports == {f"{name}.import" for name in EXPECTED_AUDIO.values()},
    "voice directory must contain exactly 47 matching WAV import sidecars",
)

pck_validator = PCK_VALIDATOR_PATH.read_text(encoding="utf-8")
pck_voice_resources = set(
    re.findall(
        r'"res://audio/sfx/characters/shin_getter/voices/([^"/]+\.wav)": false',
        pck_validator,
    )
)
require(
    pck_voice_resources == set(EXPECTED_AUDIO.values()),
    "PCK validator voice resources must match the 47 workbook WAVs exactly",
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
require('Args => "<001-047>"' in console_cmd and "TryPlayCode" in console_cmd, "sgs 001-047 command is incomplete")

service_keys = set(re.findall(r'"(SHIN_GETTER\.voice\.[A-Za-z0-9]+)"', service))
require(len(service_keys) == 46, f"expected 46 subtitle keys, found {len(service_keys)}")
language_voice_keys: dict[str, set[str]] = {}
for language in ("zhs", "eng", "jpn"):
    path = ROOT / "ShinGetterMod" / "localization" / language / "characters.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    language_voice_keys[language] = {key for key in data if key.startswith("SHIN_GETTER.voice.")}
    require(service_keys <= language_voice_keys[language], f"{language} is missing voice subtitle keys")

require(
    language_voice_keys["zhs"] == language_voice_keys["eng"] == language_voice_keys["jpn"],
    "trilingual voice key sets differ",
)

print("issue#21/#31 static validation PASS: 47 codes, 47 WAVs, triggers, masks, timing, sgs, trilingual keys")
