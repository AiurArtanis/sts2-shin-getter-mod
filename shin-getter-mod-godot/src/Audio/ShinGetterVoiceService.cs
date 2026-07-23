#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Config;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Audio;

internal enum ShinGetterVoiceCue
{
    ChangeGetterOne = 0,
    ChangeGetterOneSwitchOn = 1,
    ChangeGetterTwo = 2,
    ChangeGetterThree = 3,
    ChangeShinDragon = 4,
    CombineBlind = 5,
    GetterBeam = 6,
    GetterTomahawk = 7,
    OraOraOra = 8,
    ReturnTheFavor = 9,
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
}

internal static class ShinGetterVoiceService
{
    private const string AudioRoot = "res://audio/sfx/characters/shin_getter/voices/";
    private const float ShiningSparkIntroDurationSeconds = 1.578667f;
    private const float ShiningSparkFollowUpDurationSeconds = 1.728f;
    internal const string TransformSfxPath = AudioRoot + "transform.wav";

    private static readonly ConditionalWeakTable<Player, ShiningSparkVoiceState> ShiningSparkVoiceStates = new();

    private sealed record VoiceLine(
        ShinGetterVoiceCue Cue,
        string AudioFile,
        string? LocalizationKey,
        ShinGetterForm RequiredForm = ShinGetterForm.None,
        string? FollowUpAudioFile = null,
        string? FollowUpLocalizationKey = null,
        bool StartAtCardPlay = false);

    private static readonly IReadOnlyDictionary<ShinGetterVoiceCue, VoiceLine> Lines =
        new Dictionary<ShinGetterVoiceCue, VoiceLine>
        {
            [ShinGetterVoiceCue.ChangeGetterOne] = new(
                ShinGetterVoiceCue.ChangeGetterOne,
                "change_getter_1.wav",
                "SHIN_GETTER.voice.changeGetterOne"),
            [ShinGetterVoiceCue.ChangeGetterOneSwitchOn] = new(
                ShinGetterVoiceCue.ChangeGetterOneSwitchOn,
                "change_getter_1_switch_on.wav",
                null),
            [ShinGetterVoiceCue.SwitchOn] = new(
                ShinGetterVoiceCue.SwitchOn,
                "switch_on.wav",
                null),
            [ShinGetterVoiceCue.ChangeGetterTwo] = new(
                ShinGetterVoiceCue.ChangeGetterTwo,
                "change_getter_2.wav",
                "SHIN_GETTER.voice.changeGetterTwo"),
            [ShinGetterVoiceCue.ChangeGetterThree] = new(
                ShinGetterVoiceCue.ChangeGetterThree,
                "change_getter_3.wav",
                "SHIN_GETTER.voice.changeGetterThree"),
            [ShinGetterVoiceCue.ChangeShinDragon] = new(
                ShinGetterVoiceCue.ChangeShinDragon,
                "change_shin_dragon.wav",
                "SHIN_GETTER.voice.changeShinDragon"),
            [ShinGetterVoiceCue.CombineBlind] = new(
                ShinGetterVoiceCue.CombineBlind,
                "ryoma_combine_blind.wav",
                "SHIN_GETTER.voice.combineBlind",
                ShinGetterForm.Getter1,
                StartAtCardPlay: true),
            [ShinGetterVoiceCue.GetterBeam] = new(
                ShinGetterVoiceCue.GetterBeam,
                "ryoma_getter_beam.wav",
                "SHIN_GETTER.voice.getterBeam",
                ShinGetterForm.Getter1,
                StartAtCardPlay: true),
            [ShinGetterVoiceCue.GetterTomahawk] = new(
                ShinGetterVoiceCue.GetterTomahawk,
                "ryoma_getter_tomahawk.wav",
                "SHIN_GETTER.voice.getterTomahawk",
                ShinGetterForm.Getter1),
            [ShinGetterVoiceCue.OraOraOra] = new(
                ShinGetterVoiceCue.OraOraOra,
                "ryoma_ora_ora_ora.wav",
                "SHIN_GETTER.voice.oraOraOra",
                ShinGetterForm.Getter1),
            [ShinGetterVoiceCue.ReturnTheFavor] = new(
                ShinGetterVoiceCue.ReturnTheFavor,
                "ryoma_return_the_favor.wav",
                "SHIN_GETTER.voice.returnTheFavor",
                ShinGetterForm.Getter1,
                StartAtCardPlay: true),
            [ShinGetterVoiceCue.Roar] = new(
                ShinGetterVoiceCue.Roar,
                "ryoma_roar.wav",
                "SHIN_GETTER.voice.roar",
                ShinGetterForm.Getter1,
                StartAtCardPlay: true),
            [ShinGetterVoiceCue.StayToTheEnd] = new(
                ShinGetterVoiceCue.StayToTheEnd,
                "ryoma_stay_to_the_end.wav",
                "SHIN_GETTER.voice.stayToTheEnd",
                ShinGetterForm.Getter1,
                StartAtCardPlay: true),
            [ShinGetterVoiceCue.StarSlash] = new(
                ShinGetterVoiceCue.StarSlash,
                "ryoma_star_slash.wav",
                "SHIN_GETTER.voice.starSlash",
                ShinGetterForm.Getter1),
            [ShinGetterVoiceCue.ShiningSpark] = new(
                ShinGetterVoiceCue.ShiningSpark,
                "ryoma_shining.wav",
                "SHIN_GETTER.voice.shining",
                FollowUpAudioFile: "team_spark.wav",
                FollowUpLocalizationKey: "SHIN_GETTER.voice.spark"),
            [ShinGetterVoiceCue.GetterShine] = new(
                ShinGetterVoiceCue.GetterShine,
                "ryoma_getter_shine.wav",
                "SHIN_GETTER.voice.getterShine",
                ShinGetterForm.Getter1,
                StartAtCardPlay: true),
            [ShinGetterVoiceCue.HotBlood] = new(
                ShinGetterVoiceCue.HotBlood,
                "hot_blood.wav",
                "SHIN_GETTER.voice.hotBlood",
                ShinGetterForm.Getter1,
                StartAtCardPlay: true),
            [ShinGetterVoiceCue.Avalanche] = new(
                ShinGetterVoiceCue.Avalanche,
                "musashi_avalanche.wav",
                "SHIN_GETTER.voice.avalanche",
                ShinGetterForm.Getter3,
                StartAtCardPlay: true),
            [ShinGetterVoiceCue.GetterElectric] = new(
                ShinGetterVoiceCue.GetterElectric,
                "musashi_getter_electric.wav",
                "SHIN_GETTER.voice.getterElectric",
                ShinGetterForm.Getter3,
                StartAtCardPlay: true),
            [ShinGetterVoiceCue.GetterPower] = new(
                ShinGetterVoiceCue.GetterPower,
                "musashi_getter_power.wav",
                "SHIN_GETTER.voice.getterPower",
                ShinGetterForm.Getter3,
                StartAtCardPlay: true),
            [ShinGetterVoiceCue.FireNow] = new(
                ShinGetterVoiceCue.FireNow,
                "musashi_fire_now.wav",
                "SHIN_GETTER.voice.fireNow",
                ShinGetterForm.Getter3,
                StartAtCardPlay: true),
            [ShinGetterVoiceCue.GetterDrill] = new(
                ShinGetterVoiceCue.GetterDrill,
                "hayato_getter_drill.wav",
                "SHIN_GETTER.voice.getterDrill",
                ShinGetterForm.Getter2),
            [ShinGetterVoiceCue.Supersonic] = new(
                ShinGetterVoiceCue.Supersonic,
                "hayato_supersonic.wav",
                "SHIN_GETTER.voice.supersonic",
                ShinGetterForm.Getter2),
            [ShinGetterVoiceCue.DrillHurricane] = new(
                ShinGetterVoiceCue.DrillHurricane,
                "hayato_drill_hurricane.wav",
                "SHIN_GETTER.voice.drillHurricane",
                ShinGetterForm.Getter2),
            [ShinGetterVoiceCue.DrillArm] = new(
                ShinGetterVoiceCue.DrillArm,
                "hayato_drill_arm.wav",
                "SHIN_GETTER.voice.drillArm",
                ShinGetterForm.Getter2,
                StartAtCardPlay: true),
        };

    internal static void TryPlayCardVoice(CardModel card)
    {
        TryPlayCardVoice(card, requireCardPlayStart: false);
    }

    internal static void TryPlayCardVoiceAtCardPlayStart(CardModel card)
    {
        TryPlayCardVoice(card, requireCardPlayStart: true);
    }

    private static void TryPlayCardVoice(CardModel card, bool requireCardPlayStart)
    {
        if (card.Owner is not { Character: ShinGetter } player)
            return;

        VoiceLine? line = ResolveCardVoice(card);
        if (line == null || (requireCardPlayStart && !line.StartAtCardPlay))
            return;

        if (line.RequiredForm != ShinGetterForm.None
            && !ShinGetterCardBase.IsInForm(player, line.RequiredForm))
        {
            return;
        }

        TryPlayOneTime(player, line);
    }

    private static VoiceLine? ResolveCardVoice(CardModel card) => card switch
        {
            SGC_TripleUnity => Lines[ShinGetterVoiceCue.CombineBlind],
            SGC_GetterBeam or SGC_FinalGetterBeam => Lines[ShinGetterVoiceCue.GetterBeam],
            SGC_GetterTomahawk or SGC_GetterFlash or SGC_DiveStrike => Lines[ShinGetterVoiceCue.GetterTomahawk],
            SGC_TomahawkFury or SGC_GetterChop => Lines[ShinGetterVoiceCue.OraOraOra],
            SGC_BlackArmor or SGC_DarkCape => Lines[ShinGetterVoiceCue.ReturnTheFavor],
            SGC_Spirit or SGC_SuperKi or SGC_AwakenedSoul or SGC_GetterNova or SGC_GetterRayOverflow => Lines[ShinGetterVoiceCue.Roar],
            SGC_Desperation => Lines[ShinGetterVoiceCue.StayToTheEnd],
            SGC_StarSlash => Lines[ShinGetterVoiceCue.StarSlash],
            SGC_StonerSunshine => Lines[ShinGetterVoiceCue.GetterShine],
            SGC_HotBlood or SGC_FightingSpirit => Lines[ShinGetterVoiceCue.HotBlood],
            SGC_Avalanche => Lines[ShinGetterVoiceCue.Avalanche],
            SGC_PoseidonThunder => Lines[ShinGetterVoiceCue.GetterElectric],
            SGC_Indomitable or SGC_IronWall or SGC_HedgehogTactic => Lines[ShinGetterVoiceCue.GetterPower],
            SGC_ExpansionStrike or SGC_GetterElbow or SGC_GetterMissile => Lines[ShinGetterVoiceCue.FireNow],
            SGC_TornadoDrill or SGC_SpiralDrill => Lines[ShinGetterVoiceCue.GetterDrill],
            SGC_Acceleration or SGC_ShedLoad or SGC_BoldPlan => Lines[ShinGetterVoiceCue.Supersonic],
            SGC_HurricaneStrike or SGC_LigerAssault => Lines[ShinGetterVoiceCue.DrillHurricane],
            SGC_GetterClaw => Lines[ShinGetterVoiceCue.DrillArm],
            _ => null,
        };

    internal static void PlayTransform(Player player, ShinGetterForm targetForm)
    {
        PlayAudio(TransformSfxPath);

        ShinGetterVoiceCue? cue = targetForm switch
        {
            ShinGetterForm.Getter1 => ShinGetterVoiceCue.ChangeGetterOne,
            ShinGetterForm.Getter2 => ShinGetterVoiceCue.ChangeGetterTwo,
            ShinGetterForm.Getter3 => ShinGetterVoiceCue.ChangeGetterThree,
            _ => null,
        };

        if (cue is { } value)
            TryPlayOneTime(player, Lines[value]);
    }

    internal static void PlayShinDragonTransform(Player player)
    {
        PlayAudio(TransformSfxPath);
        TryPlayOneTime(player, Lines[ShinGetterVoiceCue.ChangeShinDragon]);
    }

    internal static void PlayCombatStart(Player player)
    {
        int combatStartVoiceCount = GetCombatStartVoiceCount(player);
        ShinGetterVoiceCue cue = combatStartVoiceCount == 0
            ? ShinGetterVoiceCue.ChangeGetterOneSwitchOn
            : ShinGetterVoiceCue.SwitchOn;
        TryPlayOneTime(player, Lines[cue]);
        SetCombatStartVoiceCount(player, combatStartVoiceCount + 1);
    }

    internal static void ResetCombatVoiceHistory(Player player)
    {
        foreach (Player runPlayer in player.RunState.Players)
        {
            if (runPlayer.Character is not ShinGetter)
                continue;

            SetPlayedVoiceMask(runPlayer, 0);
            ShiningSparkVoiceStates.GetOrCreateValue(runPlayer).ShouldPlayFollowUp = false;
        }
    }

    internal static async Task PlayShiningSparkIntro(Player player)
    {
        ShiningSparkVoiceState state = ShiningSparkVoiceStates.GetOrCreateValue(player);
        state.ShouldPlayFollowUp = false;

        VoiceLine line = Lines[ShinGetterVoiceCue.ShiningSpark];
        if (!TryClaimVoiceCue(player, line.Cue)
            || !TryPlayAudio(AudioRoot + line.AudioFile))
        {
            return;
        }

        PlaySubtitle(player, line.LocalizationKey);
        state.ShouldPlayFollowUp = true;
        await Cmd.Wait(ShiningSparkIntroDurationSeconds);
    }

    internal static async Task PlayShiningSparkFollowUp(Player player)
    {
        ShiningSparkVoiceState state = ShiningSparkVoiceStates.GetOrCreateValue(player);
        if (!state.ShouldPlayFollowUp)
            return;

        state.ShouldPlayFollowUp = false;
        VoiceLine line = Lines[ShinGetterVoiceCue.ShiningSpark];
        if (line.FollowUpAudioFile == null
            || !TryPlayAudio(AudioRoot + line.FollowUpAudioFile))
        {
            return;
        }

        PlaySubtitle(player, line.FollowUpLocalizationKey);
        await Cmd.Wait(ShiningSparkFollowUpDurationSeconds);
    }

    internal static void PlayAudio(string path, float volume = 1f)
    {
        TryPlayAudio(path, volume);
    }

    private static bool TryPlayAudio(string path, float volume = 1f)
    {
        if (NonInteractiveMode.IsActive)
            return false;

        AudioStream? stream = ResourceLoader.Load<AudioStream>(path);
        if (stream is null)
            return false;

        if (Engine.GetMainLoop() is not SceneTree sceneTree)
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
        return true;
    }

    private static bool TryPlayOneTime(Player player, VoiceLine line)
    {
        if (!TryClaimVoiceCue(player, line.Cue))
            return false;

        if (!TryPlayAudio(AudioRoot + line.AudioFile))
            return false;

        PlaySubtitle(player, line.LocalizationKey);

        return true;
    }

    private static bool TryClaimVoiceCue(Player player, ShinGetterVoiceCue cue)
    {
        ShinGetterVoiceMode voiceMode = ShinGetterChunibyoConfigService.Current.VoiceMode;
        if (voiceMode == ShinGetterVoiceMode.Silent)
            return false;
        if (voiceMode == ShinGetterVoiceMode.Always)
            return true;

        int bit = GetVoiceBit(cue);
        int playedMask = 0;
        bool hasVoiceHistory = false;

        foreach (Player runPlayer in player.RunState.Players)
        {
            if (runPlayer.Character is not ShinGetter)
                continue;

            if (TryGetPlayedVoiceMask(runPlayer, out int playerMask))
            {
                playedMask |= playerMask;
                hasVoiceHistory = true;
            }
        }

        if (!hasVoiceHistory || (playedMask & bit) != 0)
            return false;

        int updatedMask = playedMask | bit;
        foreach (Player runPlayer in player.RunState.Players)
        {
            if (runPlayer.Character is ShinGetter)
                SetPlayedVoiceMask(runPlayer, updatedMask);
        }

        return true;
    }

    private static int GetVoiceBit(ShinGetterVoiceCue cue)
    {
        int index = (int)cue;
        if (index is < 0 or >= 31)
            throw new ArgumentOutOfRangeException(nameof(cue), cue, "Voice cue must fit in the persisted int mask.");

        return 1 << index;
    }

    private static bool TryGetPlayedVoiceMask(Player player, out int playedMask)
    {
        SGR_GetterFurnace? furnace = player.GetRelic<SGR_GetterFurnace>();
        SGR_EmperorsFragment? fragment = player.GetRelic<SGR_EmperorsFragment>();
        playedMask = (furnace?.PlayedVoiceMask ?? 0) | (fragment?.PlayedVoiceMask ?? 0);
        return furnace != null || fragment != null;
    }

    private static void SetPlayedVoiceMask(Player player, int playedMask)
    {
        SGR_GetterFurnace? furnace = player.GetRelic<SGR_GetterFurnace>();
        if (furnace != null)
            furnace.PlayedVoiceMask = playedMask;

        SGR_EmperorsFragment? fragment = player.GetRelic<SGR_EmperorsFragment>();
        if (fragment != null)
            fragment.PlayedVoiceMask = playedMask;
    }

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

    private static void PlaySubtitle(Player player, string? localizationKey)
    {
        if (localizationKey == null)
            return;

        TalkCmd.Play(
            new LocString("characters", localizationKey),
            player.Creature,
            player.Character.SpeechBubbleColor);
    }

    private sealed class ShiningSparkVoiceState
    {
        public bool ShouldPlayFollowUp;
    }
}
