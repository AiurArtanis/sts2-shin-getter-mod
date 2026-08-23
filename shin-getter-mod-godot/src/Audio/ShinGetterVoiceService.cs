#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Config;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Audio;

internal enum ShinGetterVoiceCue
{
    // Values 0-24 are persisted by released saves. Do not reorder them.
    ChangeGetterOne = 0,
    ChangeGetterOneSwitchOn = 1,
    ChangeGetterTwo = 2,
    ChangeGetterThree = 3,
    ChangeShinDragon = 4,
    CombineBlind = 5,
    GetterBeam = 6,
    GetterTomahawk = 7,
    OraOraOra = 8,
    GetterSquad = 9,
    Roar = 10,
    StayToTheEnd = 11,
    StarSlash = 12,
    ShiningSpark = 13,
    GetterShine = 14,
    HotBlood = 15,
    Avalanche = 16,
    GetterElectric = 17,
    GetterPower = 18,
    FireNow = 19,
    GetterDrill = 20,
    Supersonic = 21,
    DrillHurricane = 22,
    DrillArm = 23,
    SwitchOn = 24,
    BattleWing = 25,
    StonerSunshine = 26,
    SparkFollowUp = 27,
    GetterRaySurge = 28,
    RyomaKillFinish = 29,
    RyomaKillGrunts = 30,
    RyomaKillGuillotine = 31,
    EnemySummon = 32,
    FirstDamage = 33,
    LizardEncounter = 34,
    NoHpLoss = 35,
    EventCombat = 36,
    EliteRespect = 37,
    ElitePrepare = 38,
    EliteFinally = 39,
    BossBig = 40,
    GetterMissile = 41,
    MusashiSpecialMove = 42,
    MusashiKill = 43,
    DrillHurricaneLong = 44,
    HayatoKill = 45,
    OpenGetOne = 46,
    OpenGetTwo = 47,
    OpenGetThree = 48,
    StonerArrivalFeelPower = 49,
    StonerArrivalThreeHearts = 50,
    StonerArrivalOurWill = 51,
    StonerArrivalUniteHearts = 52,
    StonerArrivalUseSunshine = 53,
    HayatoNoHpLoss = 54,
    BenkeiNoHpLoss = 55,
}

internal static class ShinGetterVoiceService
{
    private const string AudioRoot = "res://audio/sfx/characters/shin_getter/voices/";
    private const float SubtitleTailSeconds = 0.5f;
    internal const string TransformSfxPath = AudioRoot + "transform.wav";

    private enum VoicePlaybackCategory
    {
        Opening,
        InterruptingNonCard,
        DamageResponse,
        Card,
    }

    private sealed record VoiceLine(
        string Code,
        ShinGetterVoiceCue? Cue,
        string AudioFile,
        string? LocalizationKey,
        ShinGetterForm RequiredForm = ShinGetterForm.None,
        VoicePlaybackCategory Category = VoicePlaybackCategory.Card,
        bool StartAtCardPlay = false);

    private static readonly VoiceLine[] VoiceLines =
    {
        new("001", ShinGetterVoiceCue.ChangeGetterOne, "change_getter_1.wav", "SHIN_GETTER.voice.changeGetterOne"),
        new("002", ShinGetterVoiceCue.ChangeGetterOneSwitchOn, "change_getter_1_switch_on.wav", "SHIN_GETTER.voice.combatStartFirst", Category: VoicePlaybackCategory.Opening),
        new("003", ShinGetterVoiceCue.SwitchOn, "switch_on.wav", "SHIN_GETTER.voice.combatStart", Category: VoicePlaybackCategory.Opening),
        new("004", ShinGetterVoiceCue.ChangeGetterTwo, "change_getter_2.wav", "SHIN_GETTER.voice.changeGetterTwo"),
        new("005", ShinGetterVoiceCue.ChangeGetterThree, "change_getter_3.wav", "SHIN_GETTER.voice.changeGetterThree"),
        new("006", ShinGetterVoiceCue.ChangeShinDragon, "change_shin_dragon.wav", "SHIN_GETTER.voice.changeShinDragon"),
        new("007", null, "transform.wav", null),
        new("008", ShinGetterVoiceCue.CombineBlind, "ryoma_combine_blind.wav", "SHIN_GETTER.voice.combineBlind", ShinGetterForm.Getter1, StartAtCardPlay: true),
        new("009", ShinGetterVoiceCue.GetterBeam, "ryoma_getter_beam.wav", "SHIN_GETTER.voice.getterBeam", ShinGetterForm.Getter1, StartAtCardPlay: true),
        new("010", ShinGetterVoiceCue.GetterTomahawk, "ryoma_getter_tomahawk.wav", "SHIN_GETTER.voice.getterTomahawk", ShinGetterForm.Getter1),
        new("011", ShinGetterVoiceCue.OraOraOra, "ryoma_ora_ora_ora.wav", "SHIN_GETTER.voice.oraOraOra", ShinGetterForm.Getter1),
        new("012", ShinGetterVoiceCue.BattleWing, "ryoma_battle_wing.wav", "SHIN_GETTER.voice.battleWing", ShinGetterForm.Getter1),
        new("013", ShinGetterVoiceCue.GetterSquad, "ryoma_getter_squad.wav", "SHIN_GETTER.voice.getterSquad", ShinGetterForm.Getter1, StartAtCardPlay: true),
        new("014", ShinGetterVoiceCue.Roar, "ryoma_roar.wav", "SHIN_GETTER.voice.roar", ShinGetterForm.Getter1, StartAtCardPlay: true),
        new("015", ShinGetterVoiceCue.StayToTheEnd, "ryoma_stay_to_the_end.wav", "SHIN_GETTER.voice.stayToTheEnd", ShinGetterForm.Getter1, StartAtCardPlay: true),
        new("016", ShinGetterVoiceCue.StarSlash, "ryoma_star_slash.wav", "SHIN_GETTER.voice.starSlash", ShinGetterForm.Getter1),
        new("017", ShinGetterVoiceCue.StonerSunshine, "ryoma_stoner_sunshine.wav", "SHIN_GETTER.voice.stonerSunshine", ShinGetterForm.Getter1, StartAtCardPlay: true),
        new("018", ShinGetterVoiceCue.ShiningSpark, "ryoma_shining.wav", "SHIN_GETTER.voice.shining"),
        new("019", ShinGetterVoiceCue.SparkFollowUp, "team_spark.wav", "SHIN_GETTER.voice.spark"),
        new("020", ShinGetterVoiceCue.GetterRaySurge, "ryoma_getter_ray_surge.wav", "SHIN_GETTER.voice.getterRaySurge", ShinGetterForm.Getter1, StartAtCardPlay: true),
        new("021", ShinGetterVoiceCue.GetterShine, "ryoma_getter_shine.wav", "SHIN_GETTER.voice.getterShine", ShinGetterForm.Getter1, StartAtCardPlay: true),
        new("022", ShinGetterVoiceCue.RyomaKillFinish, "ryoma_kill_finish.wav", "SHIN_GETTER.voice.ryomaKillFinish", ShinGetterForm.Getter1, VoicePlaybackCategory.InterruptingNonCard),
        new("023", ShinGetterVoiceCue.RyomaKillGrunts, "ryoma_kill_grunts.wav", "SHIN_GETTER.voice.ryomaKillGrunts", ShinGetterForm.Getter1, VoicePlaybackCategory.InterruptingNonCard),
        new("024", ShinGetterVoiceCue.RyomaKillGuillotine, "ryoma_kill_guillotine.wav", "SHIN_GETTER.voice.ryomaKillGuillotine", ShinGetterForm.Getter1, VoicePlaybackCategory.InterruptingNonCard),
        new("025", ShinGetterVoiceCue.EnemySummon, "ryoma_enemy_summon.wav", "SHIN_GETTER.voice.enemySummon", ShinGetterForm.Getter1, VoicePlaybackCategory.InterruptingNonCard),
        new("026", ShinGetterVoiceCue.FirstDamage, "ryoma_first_damage.wav", "SHIN_GETTER.voice.firstDamage", ShinGetterForm.Getter1, VoicePlaybackCategory.DamageResponse),
        new("027", ShinGetterVoiceCue.LizardEncounter, "ryoma_lizard_encounter.wav", "SHIN_GETTER.voice.lizardEncounter", ShinGetterForm.Getter1, VoicePlaybackCategory.Opening),
        new("028", ShinGetterVoiceCue.NoHpLoss, "ryoma_no_hp_loss.wav", "SHIN_GETTER.voice.noHpLoss", ShinGetterForm.Getter1, VoicePlaybackCategory.DamageResponse),
        new("029", ShinGetterVoiceCue.EventCombat, "ryoma_event_combat.wav", "SHIN_GETTER.voice.eventCombat", ShinGetterForm.Getter1, VoicePlaybackCategory.Opening),
        new("030", ShinGetterVoiceCue.EliteRespect, "ryoma_elite_respect.wav", "SHIN_GETTER.voice.eliteRespect", ShinGetterForm.Getter1, VoicePlaybackCategory.Opening),
        new("031", ShinGetterVoiceCue.ElitePrepare, "ryoma_elite_prepare.wav", "SHIN_GETTER.voice.elitePrepare", ShinGetterForm.Getter1, VoicePlaybackCategory.Opening),
        new("032", ShinGetterVoiceCue.EliteFinally, "ryoma_elite_finally.wav", "SHIN_GETTER.voice.eliteFinally", ShinGetterForm.Getter1, VoicePlaybackCategory.Opening),
        new("033", ShinGetterVoiceCue.BossBig, "ryoma_boss_big.wav", "SHIN_GETTER.voice.bossBig", ShinGetterForm.Getter1, VoicePlaybackCategory.Opening),
        new("034", ShinGetterVoiceCue.HotBlood, "hot_blood.wav", "SHIN_GETTER.voice.hotBlood", ShinGetterForm.Getter1, StartAtCardPlay: true),
        new("035", ShinGetterVoiceCue.Avalanche, "musashi_avalanche.wav", "SHIN_GETTER.voice.avalanche", ShinGetterForm.Getter3, StartAtCardPlay: true),
        new("036", ShinGetterVoiceCue.GetterMissile, "musashi_getter_missile.wav", "SHIN_GETTER.voice.getterMissile", ShinGetterForm.Getter3, StartAtCardPlay: true),
        new("037", ShinGetterVoiceCue.GetterElectric, "musashi_getter_electric.wav", "SHIN_GETTER.voice.getterElectric", ShinGetterForm.Getter3, StartAtCardPlay: true),
        new("038", ShinGetterVoiceCue.GetterPower, "musashi_getter_power.wav", "SHIN_GETTER.voice.getterPower", ShinGetterForm.Getter3, StartAtCardPlay: true),
        new("039", ShinGetterVoiceCue.FireNow, "musashi_fire_now.wav", "SHIN_GETTER.voice.fireNow", ShinGetterForm.Getter3, StartAtCardPlay: true),
        new("040", ShinGetterVoiceCue.MusashiSpecialMove, "musashi_special_move.wav", "SHIN_GETTER.voice.musashiSpecialMove", ShinGetterForm.Getter3, StartAtCardPlay: true),
        new("041", ShinGetterVoiceCue.MusashiKill, "musashi_kill.wav", "SHIN_GETTER.voice.musashiKill", ShinGetterForm.Getter3, VoicePlaybackCategory.InterruptingNonCard),
        new("042", ShinGetterVoiceCue.GetterDrill, "hayato_getter_drill.wav", "SHIN_GETTER.voice.getterDrill", ShinGetterForm.Getter2),
        new("043", ShinGetterVoiceCue.Supersonic, "hayato_supersonic.wav", "SHIN_GETTER.voice.supersonic", ShinGetterForm.Getter2),
        new("044", ShinGetterVoiceCue.DrillHurricane, "hayato_drill_hurricane.wav", "SHIN_GETTER.voice.drillHurricane", ShinGetterForm.Getter2),
        new("045", ShinGetterVoiceCue.DrillHurricaneLong, "hayato_drill_hurricane_long.wav", "SHIN_GETTER.voice.drillHurricaneLong", ShinGetterForm.Getter2),
        new("046", ShinGetterVoiceCue.DrillArm, "hayato_drill_arm.wav", "SHIN_GETTER.voice.drillArm", ShinGetterForm.Getter2, StartAtCardPlay: true),
        new("047", ShinGetterVoiceCue.HayatoKill, "hayato_kill.wav", "SHIN_GETTER.voice.hayatoKill", ShinGetterForm.Getter2, VoicePlaybackCategory.InterruptingNonCard),
        new("049", ShinGetterVoiceCue.HayatoNoHpLoss, "hayato_no_hp_loss.wav", "SHIN_GETTER.voice.hayatoNoHpLoss", ShinGetterForm.Getter2, VoicePlaybackCategory.DamageResponse),
        new("050", ShinGetterVoiceCue.BenkeiNoHpLoss, "benkei_no_hp_loss.wav", "SHIN_GETTER.voice.benkeiNoHpLoss", ShinGetterForm.Getter3, VoicePlaybackCategory.DamageResponse),
        new("058", ShinGetterVoiceCue.OpenGetOne, "ryoma_open_get.wav", "SHIN_GETTER.voice.openGetOne", ShinGetterForm.Getter1, VoicePlaybackCategory.DamageResponse),
        new("059", ShinGetterVoiceCue.OpenGetTwo, "hayato_open_get.wav", "SHIN_GETTER.voice.openGetTwo", ShinGetterForm.Getter2, VoicePlaybackCategory.DamageResponse),
        new("060", ShinGetterVoiceCue.OpenGetThree, "benkei_open_get.wav", "SHIN_GETTER.voice.openGetThree", ShinGetterForm.Getter3, VoicePlaybackCategory.DamageResponse),
        new("061", ShinGetterVoiceCue.StonerArrivalFeelPower, "ryoma_feel_getter_power.wav", "SHIN_GETTER.voice.stonerArrivalFeelPower", ShinGetterForm.Getter1),
        new("062", ShinGetterVoiceCue.StonerArrivalThreeHearts, "ryoma_three_hearts_one.wav", "SHIN_GETTER.voice.stonerArrivalThreeHearts", ShinGetterForm.Getter1),
        new("063", ShinGetterVoiceCue.StonerArrivalOurWill, "ryoma_our_will_getter_power.wav", "SHIN_GETTER.voice.stonerArrivalOurWill", ShinGetterForm.Getter1),
        new("064", ShinGetterVoiceCue.StonerArrivalUniteHearts, "hayato_unite_hearts.wav", "SHIN_GETTER.voice.stonerArrivalUniteHearts", ShinGetterForm.Getter2),
        new("065", ShinGetterVoiceCue.StonerArrivalUseSunshine, "benkei_use_stoner_sunshine.wav", "SHIN_GETTER.voice.stonerArrivalUseSunshine", ShinGetterForm.Getter3),
    };

    private static readonly IReadOnlyDictionary<ShinGetterVoiceCue, VoiceLine> Lines = VoiceLines
        .Where(line => line.Cue.HasValue)
        .ToDictionary(line => line.Cue!.Value);

    private static readonly IReadOnlyDictionary<string, VoiceLine> LinesByCode = VoiceLines
        .ToDictionary(line => line.Code, StringComparer.Ordinal);

    private static readonly ShinGetterVoiceCue[] RyomaKillPool =
    {
        ShinGetterVoiceCue.RyomaKillFinish,
        ShinGetterVoiceCue.RyomaKillGrunts,
        ShinGetterVoiceCue.RyomaKillGuillotine,
    };

    private static readonly ShinGetterVoiceCue[] EliteOpeningPool =
    {
        ShinGetterVoiceCue.EliteRespect,
        ShinGetterVoiceCue.ElitePrepare,
        ShinGetterVoiceCue.EliteFinally,
    };

    private static readonly ShinGetterVoiceCue[] BossOpeningPool =
    {
        ShinGetterVoiceCue.BossBig,
    };

    private static readonly ConditionalWeakTable<Player, VoicePlaybackState> PlaybackStates = new();

    internal static void TryPlayCardVoice(CardModel card) =>
        TryPlayCardVoice(card, requireCardPlayStart: false);

    internal static void TryPlayCardVoiceAtCardPlayStart(CardModel card) =>
        TryPlayCardVoice(card, requireCardPlayStart: true);

    private static void TryPlayCardVoice(CardModel card, bool requireCardPlayStart)
    {
        if (requireCardPlayStart && UsesCustomCardVoiceTiming(card))
            return;

        if (card.Owner is not { Character: ShinGetter } player)
            return;

        VoiceLine? line = ResolveCardVoice(card);
        if (line == null || line.StartAtCardPlay != requireCardPlayStart)
            return;

        TryPlayOneTime(player, line);
    }

    internal static bool TryPlayCardVoiceAtCustomTiming(CardModel card, out float durationSeconds)
    {
        durationSeconds = 0f;
        if (card.Owner is not { Character: ShinGetter } player
            || ResolveCardVoice(card) is not { } line)
        {
            return false;
        }

        return TryPlayOneTime(player, line, out durationSeconds);
    }

    private static bool UsesCustomCardVoiceTiming(CardModel card) =>
        card is SGC_GetterWill or SGC_HolyDragonRoar or SGC_PoseidonThunder
        || card is SGC_StonerSunshine
            && ShinGetterCardBase.IsInForm(card.Owner, ShinGetterForm.Getter1);

    private static VoiceLine? ResolveCardVoice(CardModel card) => card switch
    {
        SGC_TripleUnity => Lines[ShinGetterVoiceCue.CombineBlind],
        SGC_GetterBeam or SGC_FinalGetterBeam => Lines[ShinGetterVoiceCue.GetterBeam],
        SGC_GetterTomahawk => Lines[ShinGetterVoiceCue.GetterTomahawk],
        SGC_TomahawkFury or SGC_GetterChop => Lines[ShinGetterVoiceCue.OraOraOra],
        SGC_GetterFlash or SGC_DiveStrike => Lines[ShinGetterVoiceCue.BattleWing],
        SGC_BlackArmor or SGC_DarkCape => Lines[ShinGetterVoiceCue.GetterSquad],
        SGC_Spirit or SGC_SuperKi or SGC_AwakenedSoul => Lines[ShinGetterVoiceCue.Roar],
        SGC_Desperation => Lines[ShinGetterVoiceCue.StayToTheEnd],
        SGC_StarSlash => Lines[ShinGetterVoiceCue.StarSlash],
        SGC_StonerSunshine => Lines[ShinGetterVoiceCue.StonerSunshine],
        SGC_GetterWill or SGC_GetterRayOverflow => Lines[ShinGetterVoiceCue.GetterRaySurge],
        SGC_HolyDragonRoar or SGC_GetterNova => Lines[ShinGetterVoiceCue.GetterShine],
        SGC_HotBlood or SGC_FightingSpirit => Lines[ShinGetterVoiceCue.HotBlood],
        SGC_Avalanche => Lines[ShinGetterVoiceCue.Avalanche],
        SGC_GetterMissile => Lines[ShinGetterVoiceCue.GetterMissile],
        SGC_PoseidonThunder => Lines[ShinGetterVoiceCue.GetterElectric],
        SGC_Indomitable or SGC_IronWall or SGC_HedgehogTactic => Lines[ShinGetterVoiceCue.GetterPower],
        SGC_ExpansionStrike or SGC_GetterElbow => Lines[ShinGetterVoiceCue.FireNow],
        SGC_Grapple => Lines[ShinGetterVoiceCue.MusashiSpecialMove],
        SGC_TornadoDrill or SGC_SpiralDrill => Lines[ShinGetterVoiceCue.GetterDrill],
        SGC_Acceleration or SGC_ShedLoad or SGC_BoldPlan => Lines[ShinGetterVoiceCue.Supersonic],
        SGC_HurricaneStrike => Lines[ShinGetterVoiceCue.DrillHurricane],
        SGC_LigerAssault => Lines[ShinGetterVoiceCue.DrillHurricaneLong],
        SGC_GetterClaw => Lines[ShinGetterVoiceCue.DrillArm],
        _ => null,
    };

    internal static Task PlayTransform(Player player, ShinGetterForm targetForm, bool playVoice = true)
    {
        PlayAudio(TransformSfxPath);
        if (!playVoice)
            return Task.CompletedTask;

        ShinGetterVoiceCue? cue = targetForm switch
        {
            ShinGetterForm.Getter1 => ShinGetterVoiceCue.ChangeGetterOne,
            ShinGetterForm.Getter2 => ShinGetterVoiceCue.ChangeGetterTwo,
            ShinGetterForm.Getter3 => ShinGetterVoiceCue.ChangeGetterThree,
            _ => null,
        };

        if (cue is not { } value
            || !TryPlayOneTime(player, Lines[value], out float durationSeconds))
        {
            return Task.CompletedTask;
        }

        return Cmd.Wait(durationSeconds);
    }

    internal static Task PlayOpenGet(Player player)
    {
        ShinGetterVoiceCue? cue = player.Creature.GetPower<SGP_ShinForm>() != null
            ? ShinGetterVoiceCue.OpenGetOne
            : player.Creature.GetPower<SGP_ShinGetterOne>() != null
                ? ShinGetterVoiceCue.OpenGetOne
                : player.Creature.GetPower<SGP_ShinGetterTwo>() != null
                    ? ShinGetterVoiceCue.OpenGetTwo
                    : player.Creature.GetPower<SGP_ShinGetterThree>() != null
                        ? ShinGetterVoiceCue.OpenGetThree
                        : null;
        if (cue is { } selectedCue)
            TryPlayOneTime(player, Lines[selectedCue]);

        return Task.CompletedTask;
    }

    internal static void PlayStonerSunshineArrival(Player player)
    {
        bool isShinDragon = player.Creature.GetPower<SGP_ShinForm>() != null;
        ShinGetterVoiceCue[] candidates = isShinDragon
            ? new[]
            {
                ShinGetterVoiceCue.StonerArrivalThreeHearts,
                ShinGetterVoiceCue.StonerArrivalOurWill,
                ShinGetterVoiceCue.StonerArrivalUniteHearts,
                ShinGetterVoiceCue.StonerArrivalUseSunshine,
            }
            : player.Creature.GetPower<SGP_ShinGetterOne>() != null
                ? new[]
                {
                    ShinGetterVoiceCue.StonerArrivalFeelPower,
                    ShinGetterVoiceCue.StonerArrivalThreeHearts,
                    ShinGetterVoiceCue.StonerArrivalOurWill,
                }
                : player.Creature.GetPower<SGP_ShinGetterTwo>() != null
                    ? new[] { ShinGetterVoiceCue.StonerArrivalUniteHearts }
                    : player.Creature.GetPower<SGP_ShinGetterThree>() != null
                        ? new[] { ShinGetterVoiceCue.StonerArrivalUseSunshine }
                        : Array.Empty<ShinGetterVoiceCue>();
        if (candidates.Length == 0)
            return;

        ShinGetterVoiceCue[] available = candidates
            .Where(candidate => CanClaimVoiceCue(player, candidate))
            .ToArray();
        if (available.Length == 0)
            return;

        ShinGetterVoiceCue cue = available[MegaCrit.Sts2.Core.Random.Rng.Chaotic.NextInt(available.Length)];
        VoiceLine line = Lines[cue];
        if (!TryClaimVoiceCue(player, cue))
            return;

        TryPlayLine(player, line, out _, ignoreRequiredForm: isShinDragon);
    }

    /// <summary>
    /// Suppresses the low-HP threshold voice family (workbook codes 052-057) while a card
    /// deliberately sets HP. This prevents Desperation from masquerading as incoming damage.
    /// </summary>
    internal static IDisposable SuppressLowHpThresholdVoices(Player player)
    {
        VoicePlaybackState state = PlaybackStates.GetOrCreateValue(player);
        state.LowHpVoiceSuppressionDepth++;
        return new LowHpVoiceSuppression(state);
    }

    internal static bool AreLowHpThresholdVoicesSuppressed(Player player) =>
        PlaybackStates.GetOrCreateValue(player).LowHpVoiceSuppressionDepth > 0;

    internal static void PlayShinDragonTransform(Player player)
    {
        PlayAudio(TransformSfxPath);
        TryPlayOneTime(player, Lines[ShinGetterVoiceCue.ChangeShinDragon]);
    }

    internal static void PrepareCombatStart(Player player, CombatRoom room)
    {
        ResetCombatVoiceHistory(player);
        var context = new CombatStartVoiceContext(room);
        foreach (Player runPlayer in player.RunState.Players)
        {
            if (runPlayer.Character is ShinGetter)
                PlaybackStates.GetOrCreateValue(runPlayer).CombatStartContext = context;
        }
    }

    internal static void PlayPreparedCombatStart(Player player)
    {
        VoicePlaybackState playbackState = PlaybackStates.GetOrCreateValue(player);
        CombatStartVoiceContext? context = playbackState.CombatStartContext;
        if (context == null || context.HasPlayed)
            return;

        context.HasPlayed = true;
        PlayCombatStart(player, context.Room);
    }

    private static void PlayCombatStart(Player player, CombatRoom room)
    {
        int combatStartVoiceCount = GetCombatStartVoiceCount(player);
        bool played = false;

        if (player.RunState.CurrentMapPointHistoryEntry?.MapPointType == MapPointType.Unknown)
        {
            played = TryPlayOneTime(player, Lines[ShinGetterVoiceCue.EventCombat]);
        }
        else if (room.Encounter.MonstersWithSlots.Any(monster =>
                     monster.Item1 is HunterKiller or TestSubject))
        {
            played = TryPlayOneTime(player, Lines[ShinGetterVoiceCue.LizardEncounter]);
        }
        else if (room.RoomType == RoomType.Boss)
        {
            played = TryPlayOpeningPool(player, BossOpeningPool);
        }
        else if (room.RoomType == RoomType.Elite)
        {
            played = TryPlayOpeningPool(player, EliteOpeningPool);
        }

        if (!played)
        {
            ShinGetterVoiceCue cue = combatStartVoiceCount == 0
                ? ShinGetterVoiceCue.ChangeGetterOneSwitchOn
                : ShinGetterVoiceCue.SwitchOn;
            TryPlayOneTime(player, Lines[cue]);
        }

        SetCombatStartVoiceCount(player, combatStartVoiceCount + 1);
    }

    internal static void ResetCombatVoiceHistory(Player player)
    {
        foreach (Player runPlayer in player.RunState.Players)
        {
            if (runPlayer.Character is not ShinGetter)
                continue;

            SetPlayedVoiceMasks(runPlayer, default);
            VoicePlaybackState state = PlaybackStates.GetOrCreateValue(runPlayer);
            state.ShouldPlayShiningSparkFollowUp = false;
            state.HasHandledFirstDamage = false;
            state.LowHpVoiceSuppressionDepth = 0;
            state.CombatStartContext = null;
            state.PendingKillVoiceLines.Clear();
            StopAllVoiceAudio(state);
            StopCurrentSubtitle(state);
        }
    }

    internal static async Task PlayShiningSparkIntro(Player player)
    {
        VoicePlaybackState state = PlaybackStates.GetOrCreateValue(player);
        state.ShouldPlayShiningSparkFollowUp = false;

        if (!TryPlayOneTime(player, Lines[ShinGetterVoiceCue.ShiningSpark], out float durationSeconds))
            return;

        state.ShouldPlayShiningSparkFollowUp = true;
        await Cmd.Wait(durationSeconds);
    }

    internal static async Task PlayShiningSparkFollowUp(Player player)
    {
        VoicePlaybackState state = PlaybackStates.GetOrCreateValue(player);
        if (!state.ShouldPlayShiningSparkFollowUp)
            return;

        state.ShouldPlayShiningSparkFollowUp = false;
        if (TryPlayLine(player, Lines[ShinGetterVoiceCue.SparkFollowUp], out float durationSeconds, ignoreRequiredForm: true))
            await Cmd.Wait(durationSeconds);
    }

    internal static void OnEnemySummoned(Creature creature)
    {
        if (creature.Side != CombatSide.Enemy
            || !CombatManager.Instance.IsInProgress
            || creature.CombatState == null)
        {
            return;
        }

        Player? player = creature.CombatState.Players.FirstOrDefault(candidate =>
            candidate.Character is ShinGetter);
        if (player != null)
            TryPlayOneTime(player, Lines[ShinGetterVoiceCue.EnemySummon]);
    }

    internal static void OnAfterDamageGiven(
        Player player,
        Creature? dealer,
        DamageResult result,
        Creature target)
    {
        if (dealer != player.Creature
            || target.Side != CombatSide.Enemy
            || !result.WasTargetKilled
            || target.CombatState == null
            || !target.CombatState.Enemies.Any(enemy => enemy.IsAlive))
        {
            return;
        }

        ShinGetterVoiceCue[] pool = GetKillVoicePool(player);
        TryQueueRandomOneTimeAfterCurrentVoice(player, pool);
    }

    internal static void OnAfterDamageReceived(
        Player player,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer)
    {
        if (target != player.Creature)
            return;

        VoicePlaybackState state = PlaybackStates.GetOrCreateValue(player);
        if (result.UnblockedDamage > 0)
        {
            if (state.HasHandledFirstDamage)
                return;

            state.HasHandledFirstDamage = true;
            TryPlayOneTime(player, Lines[ShinGetterVoiceCue.FirstDamage]);
            return;
        }

        if (dealer?.Side == CombatSide.Enemy && props.HasFlag(ValueProp.Move))
            TryPlayOneTime(player, Lines[GetNoHpLossVoiceCue(player)]);
    }

    private static ShinGetterVoiceCue GetNoHpLossVoiceCue(Player player)
    {
        ShinGetterForm activeForm = player.Creature.GetPower<SGP_ShinGetterTwo>() != null
            ? ShinGetterForm.Getter2
            : player.Creature.GetPower<SGP_ShinGetterThree>() != null
                ? ShinGetterForm.Getter3
                : ShinGetterForm.Getter1;

        return activeForm switch
        {
            ShinGetterForm.Getter2 => ShinGetterVoiceCue.HayatoNoHpLoss,
            ShinGetterForm.Getter3 => ShinGetterVoiceCue.BenkeiNoHpLoss,
            _ => ShinGetterVoiceCue.NoHpLoss,
        };
    }

    private static ShinGetterVoiceCue[] GetKillVoicePool(Player player)
    {
        if (player.Creature.GetPower<SGP_ShinForm>() != null
            || player.Creature.GetPower<SGP_ShinGetterOne>() != null)
        {
            return RyomaKillPool;
        }

        if (player.Creature.GetPower<SGP_ShinGetterTwo>() != null)
            return new[] { ShinGetterVoiceCue.HayatoKill };

        if (player.Creature.GetPower<SGP_ShinGetterThree>() != null)
            return new[] { ShinGetterVoiceCue.MusashiKill };

        return Array.Empty<ShinGetterVoiceCue>();
    }

    internal static bool TryPlayCode(Player? player, string code, out string message)
    {
        if (player?.Character is not ShinGetter)
        {
            message = "sgs requires a Shin Getter player in an active run.";
            return false;
        }

        if (!LinesByCode.TryGetValue(code, out VoiceLine? line))
        {
            message = "Usage: sgs <001-047|049-050|058-065>";
            return false;
        }

        if (!TryPlayLine(player, line, out _, ignoreRequiredForm: true))
        {
            message = $"Could not play Shin Getter sound {code}.";
            return false;
        }

        message = $"Playing Shin Getter sound {code}: {line.AudioFile}";
        return true;
    }

    internal static void PlayAudio(string path, float volume = 1f) =>
        TryPlayStandaloneAudio(path, out _, volume);

    private static bool TryPlayOneTime(Player player, VoiceLine line) =>
        TryPlayOneTime(player, line, out _);

    private static bool TryPlayOneTime(Player player, VoiceLine line, out float durationSeconds)
    {
        durationSeconds = 0f;
        if (line.Category == VoicePlaybackCategory.DamageResponse
            && HasActiveDamageResponse(player))
        {
            return false;
        }

        if (line.Cue is not { } cue
            || !IsRequiredFormActive(player, line)
            || !TryClaimVoiceCue(player, cue))
        {
            return false;
        }

        return TryPlayLine(player, line, out durationSeconds, ignoreRequiredForm: true);
    }

    private static bool TryQueueRandomOneTimeAfterCurrentVoice(
        Player player,
        IReadOnlyCollection<ShinGetterVoiceCue> cues)
    {
        List<VoiceLine> candidates = cues
            .Select(cue => Lines[cue])
            .Where(line => IsRequiredFormActive(player, line) && CanClaimVoiceCue(player, line.Cue!.Value))
            .ToList();

        if (candidates.Count == 0)
            return false;

        VoiceLine selected = candidates[Random.Shared.Next(candidates.Count)];
        if (selected.Cue is not { } cue || !TryClaimVoiceCue(player, cue))
            return false;

        VoicePlaybackState state = PlaybackStates.GetOrCreateValue(player);
        state.PendingKillVoiceLines.Enqueue(selected);
        TryStartNextQueuedKillVoice(player, state);
        return true;
    }

    private static void TryStartNextQueuedKillVoice(Player player, VoicePlaybackState state)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            state.PendingKillVoiceLines.Clear();
            return;
        }

        while (!state.IsStoppingVoiceAudio
               && state.ActiveVoicePlayers.Count == 0
               && state.PendingKillVoiceLines.TryDequeue(out VoiceLine? line))
        {
            if (line != null && TryPlayLine(player, line, out _, ignoreRequiredForm: true))
                return;
        }
    }

    private static bool TryPlayOpeningPool(Player player, IReadOnlyCollection<ShinGetterVoiceCue> cues)
    {
        ShinGetterVoiceMode mode = ShinGetterChunibyoConfigService.Current.VoiceMode;
        if (mode == ShinGetterVoiceMode.Silent)
            return false;

        int playedOpeningMask = GetOpeningVoiceMask(player);
        List<VoiceLine> candidates = cues
            .Select(cue => Lines[cue])
            .Where(line => mode == ShinGetterVoiceMode.Always
                           || (playedOpeningMask & GetOpeningVoiceBit(line.Cue!.Value)) == 0)
            .ToList();

        if (candidates.Count == 0)
            return false;

        VoiceLine selected = candidates[Random.Shared.Next(candidates.Count)];
        if (!TryPlayOneTime(player, selected))
            return false;

        if (mode == ShinGetterVoiceMode.OncePerCombat)
            SetOpeningVoiceMask(player, playedOpeningMask | GetOpeningVoiceBit(selected.Cue!.Value));

        return true;
    }

    private static bool TryPlayLine(
        Player player,
        VoiceLine line,
        out float durationSeconds,
        bool ignoreRequiredForm = false)
    {
        durationSeconds = 0f;
        if (!ignoreRequiredForm && !IsRequiredFormActive(player, line))
            return false;

        if (!TryPlayVoiceAudio(player, AudioRoot + line.AudioFile, line.Category, out durationSeconds))
            return false;

        PlaySubtitle(player, line.LocalizationKey, durationSeconds, line.Category);
        return true;
    }

    private static bool IsRequiredFormActive(Player player, VoiceLine line) =>
        line.RequiredForm == ShinGetterForm.None
        || ShinGetterCardBase.IsInForm(player, line.RequiredForm);

    private static bool TryPlayVoiceAudio(
        Player player,
        string path,
        VoicePlaybackCategory category,
        out float durationSeconds,
        float volume = 1f)
    {
        durationSeconds = 0f;
        if (NonInteractiveMode.IsActive)
            return false;

        AudioStream? stream = ResourceLoader.Load<AudioStream>(path);
        if (stream == null || Engine.GetMainLoop() is not SceneTree sceneTree)
            return false;

        VoicePlaybackState state = PlaybackStates.GetOrCreateValue(player);
        if (category == VoicePlaybackCategory.DamageResponse)
        {
            if (HasActiveDamageResponse(state))
                return false;

            StopAllVoiceAudio(state);
        }
        else if (category is VoicePlaybackCategory.Opening or VoicePlaybackCategory.InterruptingNonCard)
        {
            StopAllVoiceAudio(state);
        }

        var audioPlayer = new AudioStreamPlayer
        {
            Stream = stream,
            Bus = "SFX",
            VolumeDb = Mathf.LinearToDb(volume),
        };
        state.ActiveVoicePlayers.Add(audioPlayer);
        if (category == VoicePlaybackCategory.DamageResponse)
            state.ActiveDamageResponsePlayer = audioPlayer;

        audioPlayer.Finished += () =>
        {
            state.ActiveVoicePlayers.Remove(audioPlayer);
            if (state.ActiveDamageResponsePlayer == audioPlayer)
                state.ActiveDamageResponsePlayer = null;

            if (GodotObject.IsInstanceValid(audioPlayer))
                audioPlayer.QueueFree();

            TryStartNextQueuedKillVoice(player, state);
        };
        sceneTree.Root.AddChild(audioPlayer);
        audioPlayer.Play();
        durationSeconds = (float)stream.GetLength();
        return true;
    }

    private static bool HasActiveDamageResponse(Player player) =>
        HasActiveDamageResponse(PlaybackStates.GetOrCreateValue(player));

    private static bool HasActiveDamageResponse(VoicePlaybackState state)
    {
        AudioStreamPlayer? audioPlayer = state.ActiveDamageResponsePlayer;
        if (audioPlayer == null)
            return false;
        if (GodotObject.IsInstanceValid(audioPlayer) && audioPlayer.Playing)
            return true;

        state.ActiveDamageResponsePlayer = null;
        return false;
    }

    private static bool TryPlayStandaloneAudio(string path, out float durationSeconds, float volume = 1f)
    {
        durationSeconds = 0f;
        if (NonInteractiveMode.IsActive)
            return false;

        AudioStream? stream = ResourceLoader.Load<AudioStream>(path);
        if (stream == null || Engine.GetMainLoop() is not SceneTree sceneTree)
            return false;

        var audioPlayer = new AudioStreamPlayer
        {
            Stream = stream,
            Bus = "SFX",
            VolumeDb = Mathf.LinearToDb(volume),
        };
        audioPlayer.Finished += audioPlayer.QueueFree;
        sceneTree.Root.AddChild(audioPlayer);
        audioPlayer.Play();
        durationSeconds = (float)stream.GetLength();
        return true;
    }

    private static void StopAllVoiceAudio(VoicePlaybackState state)
    {
        state.IsStoppingVoiceAudio = true;
        try
        {
            foreach (AudioStreamPlayer audioPlayer in state.ActiveVoicePlayers.ToArray())
            {
                if (!GodotObject.IsInstanceValid(audioPlayer))
                    continue;

                audioPlayer.Stop();
                audioPlayer.QueueFree();
            }
        }
        finally
        {
            state.ActiveVoicePlayers.Clear();
            state.ActiveDamageResponsePlayer = null;
            state.IsStoppingVoiceAudio = false;
        }
    }

    private static void PlaySubtitle(
        Player player,
        string? localizationKey,
        float audioDurationSeconds,
        VoicePlaybackCategory category)
    {
        if (localizationKey == null)
            return;

        VoicePlaybackState state = PlaybackStates.GetOrCreateValue(player);
        StopCurrentSubtitle(state);

        NSpeechBubbleVfx? subtitle = TalkCmd.Play(
            new LocString("characters", localizationKey),
            player.Creature,
            player.Character.SpeechBubbleColor,
            VfxDuration.Forever);
        if (subtitle == null)
            return;

        state.CurrentSubtitle = subtitle;
        int generation = ++state.SubtitleGeneration;
        float displaySeconds = category == VoicePlaybackCategory.Opening
            ? audioDurationSeconds
            : audioDurationSeconds + SubtitleTailSeconds;
        TaskHelper.RunSafely(HideSubtitleAfterDelay(state, subtitle, generation, displaySeconds));
    }

    private static async Task HideSubtitleAfterDelay(
        VoicePlaybackState state,
        NSpeechBubbleVfx subtitle,
        int generation,
        float displaySeconds)
    {
        await Cmd.Wait(Math.Max(displaySeconds, 0.01f));
        if (state.SubtitleGeneration != generation || state.CurrentSubtitle != subtitle)
            return;

        state.CurrentSubtitle = null;
        await subtitle.AnimOut();
    }

    private static void StopCurrentSubtitle(VoicePlaybackState state)
    {
        state.SubtitleGeneration++;
        NSpeechBubbleVfx? subtitle = state.CurrentSubtitle;
        state.CurrentSubtitle = null;
        if (subtitle != null && GodotObject.IsInstanceValid(subtitle))
            TaskHelper.RunSafely(subtitle.AnimOut());
    }

    private static bool CanClaimVoiceCue(Player player, ShinGetterVoiceCue cue)
    {
        ShinGetterVoiceMode voiceMode = ShinGetterChunibyoConfigService.Current.VoiceMode;
        if (voiceMode == ShinGetterVoiceMode.Silent)
            return false;
        if (voiceMode == ShinGetterVoiceMode.Always)
            return true;

        return TryGetPlayedVoiceMasks(player, out VoiceHistoryMasks playedMasks)
               && !playedMasks.Contains(cue);
    }

    private static bool TryClaimVoiceCue(Player player, ShinGetterVoiceCue cue)
    {
        ShinGetterVoiceMode voiceMode = ShinGetterChunibyoConfigService.Current.VoiceMode;
        if (voiceMode == ShinGetterVoiceMode.Silent)
            return false;
        if (voiceMode == ShinGetterVoiceMode.Always)
            return true;

        VoiceHistoryMasks playedMasks = default;
        bool hasVoiceHistory = false;
        foreach (Player runPlayer in player.RunState.Players)
        {
            if (runPlayer.Character is not ShinGetter)
                continue;

            if (TryGetPlayedVoiceMasks(runPlayer, out VoiceHistoryMasks playerMasks))
            {
                playedMasks |= playerMasks;
                hasVoiceHistory = true;
            }
        }

        if (!hasVoiceHistory || playedMasks.Contains(cue))
            return false;

        VoiceHistoryMasks updatedMasks = playedMasks.Add(cue);
        foreach (Player runPlayer in player.RunState.Players)
        {
            if (runPlayer.Character is ShinGetter)
                SetPlayedVoiceMasks(runPlayer, updatedMasks);
        }

        return true;
    }

    private static bool TryGetPlayedVoiceMasks(Player player, out VoiceHistoryMasks playedMasks)
    {
        SGR_GetterFurnace? furnace = player.GetRelic<SGR_GetterFurnace>();
        SGR_EmperorsFragment? fragment = player.GetRelic<SGR_EmperorsFragment>();
        playedMasks = new VoiceHistoryMasks(
            (furnace?.PlayedVoiceMask ?? 0) | (fragment?.PlayedVoiceMask ?? 0),
            (furnace?.PlayedVoiceMaskHigh ?? 0) | (fragment?.PlayedVoiceMaskHigh ?? 0));
        return furnace != null || fragment != null;
    }

    private static void SetPlayedVoiceMasks(Player player, VoiceHistoryMasks playedMasks)
    {
        if (player.GetRelic<SGR_GetterFurnace>() is { } furnace)
        {
            furnace.PlayedVoiceMask = playedMasks.Low;
            furnace.PlayedVoiceMaskHigh = playedMasks.High;
        }

        if (player.GetRelic<SGR_EmperorsFragment>() is { } fragment)
        {
            fragment.PlayedVoiceMask = playedMasks.Low;
            fragment.PlayedVoiceMaskHigh = playedMasks.High;
        }
    }

    private static int GetOpeningVoiceMask(Player player)
    {
        int mask = 0;
        foreach (Player runPlayer in player.RunState.Players)
        {
            if (runPlayer.Character is not ShinGetter)
                continue;

            mask |= runPlayer.GetRelic<SGR_GetterFurnace>()?.OpeningVoiceMask
                    ?? runPlayer.GetRelic<SGR_EmperorsFragment>()?.OpeningVoiceMask
                    ?? 0;
        }

        return mask;
    }

    private static void SetOpeningVoiceMask(Player player, int mask)
    {
        foreach (Player runPlayer in player.RunState.Players)
        {
            if (runPlayer.Character is not ShinGetter)
                continue;

            if (runPlayer.GetRelic<SGR_GetterFurnace>() is { } furnace)
                furnace.OpeningVoiceMask = mask;
            if (runPlayer.GetRelic<SGR_EmperorsFragment>() is { } fragment)
                fragment.OpeningVoiceMask = mask;
        }
    }

    private static int GetOpeningVoiceBit(ShinGetterVoiceCue cue) => cue switch
    {
        ShinGetterVoiceCue.EliteRespect => 1 << 0,
        ShinGetterVoiceCue.ElitePrepare => 1 << 1,
        ShinGetterVoiceCue.EliteFinally => 1 << 2,
        ShinGetterVoiceCue.BossBig => 1 << 3,
        _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Cue is not in an opening voice pool."),
    };

    private static int GetCombatStartVoiceCount(Player player)
    {
        int combatStartVoiceCount = 0;
        foreach (Player runPlayer in player.RunState.Players)
        {
            if (runPlayer.Character is not ShinGetter)
                continue;

            combatStartVoiceCount = Math.Max(
                combatStartVoiceCount,
                runPlayer.GetRelic<SGR_GetterFurnace>()?.CombatStartVoiceCount
                    ?? runPlayer.GetRelic<SGR_EmperorsFragment>()?.CombatStartVoiceCount
                    ?? 0);
        }

        return combatStartVoiceCount;
    }

    private static void SetCombatStartVoiceCount(Player player, int combatStartVoiceCount)
    {
        foreach (Player runPlayer in player.RunState.Players)
        {
            if (runPlayer.Character is not ShinGetter)
                continue;

            if (runPlayer.GetRelic<SGR_GetterFurnace>() is { } furnace)
                furnace.CombatStartVoiceCount = combatStartVoiceCount;
            if (runPlayer.GetRelic<SGR_EmperorsFragment>() is { } fragment)
                fragment.CombatStartVoiceCount = combatStartVoiceCount;
        }
    }

    private readonly record struct VoiceHistoryMasks(int Low, int High)
    {
        public bool Contains(ShinGetterVoiceCue cue)
        {
            int index = (int)cue;
            return index < 31
                ? (Low & (1 << index)) != 0
                : (High & (1 << (index - 31))) != 0;
        }

        public VoiceHistoryMasks Add(ShinGetterVoiceCue cue)
        {
            int index = (int)cue;
            if (index is < 0 or >= 62)
                throw new ArgumentOutOfRangeException(nameof(cue), cue, "Voice cue must fit in two persisted int masks.");

            return index < 31
                ? this with { Low = Low | (1 << index) }
                : this with { High = High | (1 << (index - 31)) };
        }

        public static VoiceHistoryMasks operator |(VoiceHistoryMasks left, VoiceHistoryMasks right) =>
            new(left.Low | right.Low, left.High | right.High);
    }

    private sealed class VoicePlaybackState
    {
        public readonly List<AudioStreamPlayer> ActiveVoicePlayers = new();
        public readonly Queue<VoiceLine> PendingKillVoiceLines = new();
        public bool IsStoppingVoiceAudio;
        public AudioStreamPlayer? ActiveDamageResponsePlayer;
        public NSpeechBubbleVfx? CurrentSubtitle;
        public int SubtitleGeneration;
        public bool ShouldPlayShiningSparkFollowUp;
        public bool HasHandledFirstDamage;
        public int LowHpVoiceSuppressionDepth;
        public CombatStartVoiceContext? CombatStartContext;
    }

    private sealed class LowHpVoiceSuppression : IDisposable
    {
        private VoicePlaybackState? _state;

        internal LowHpVoiceSuppression(VoicePlaybackState state)
        {
            _state = state;
        }

        public void Dispose()
        {
            if (_state is not { } state)
                return;

            _state = null;
            state.LowHpVoiceSuppressionDepth = Math.Max(0, state.LowHpVoiceSuppressionDepth - 1);
        }
    }

    private sealed class CombatStartVoiceContext
    {
        public CombatStartVoiceContext(CombatRoom room)
        {
            Room = room;
        }

        public CombatRoom Room { get; }
        public bool HasPlayed { get; set; }
    }
}
