#nullable enable
using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(TheArchitect), "AnimPlayerAttackIfNecessary")]
internal static class ShinGetterArchitectAttackPatch
{
    private static readonly AccessTools.FieldRef<TheArchitect, Creature?> ArchitectCreatureRef =
        AccessTools.FieldRefAccess<TheArchitect, Creature?>("_architectCreature");

    private static readonly AccessTools.FieldRef<TheArchitect, int> ScoreRef =
        AccessTools.FieldRefAccess<TheArchitect, int>("_score");

    private static readonly AccessTools.FieldRef<TheArchitect, NSpeechBubbleVfx?> SpeechBubbleRef =
        AccessTools.FieldRefAccess<TheArchitect, NSpeechBubbleVfx?>("_speechBubble");

    private static bool Prefix(TheArchitect __instance, ArchitectAttackers attackers, ref Task<bool> __result)
    {
        Player? player = __instance.Owner;
        if (player?.Character is not ShinGetter)
            return true;

        if (attackers is not (ArchitectAttackers.Player or ArchitectAttackers.Both))
            return true;

        Creature? architect = ArchitectCreatureRef(__instance);
        if (architect == null)
            return true;

        __result = PlayGetterArchitectAttack(__instance, player, architect, ScoreRef(__instance));
        return false;
    }

    private static async Task<bool> PlayGetterArchitectAttack(
        TheArchitect architectEvent,
        Player player,
        Creature architect,
        int score)
    {
        if (SpeechBubbleRef(architectEvent) is { } speechBubble)
        {
            await speechBubble.AnimOut();
            SpeechBubbleRef(architectEvent) = null;
        }

        Creature owner = player.Creature;
        int[] damageParts = DivideInOrder(Math.Max(3, score));

        await PlayGetterBeamHit(owner, architect, damageParts[0]);
        await Cmd.Wait(0.12f);
        await PlayTornadoDrillHit(owner, architect, damageParts[1]);
        await Cmd.Wait(0.12f);
        await PlayGetterMissileHit(owner, architect, damageParts[2]);
        await Cmd.Wait(2f);
        return true;
    }

    private static async Task PlayGetterBeamHit(Creature owner, Creature architect, int damage)
    {
        await PlayArchitectHit(
            owner,
            architect,
            damage,
            () => ShinGetterBeamVfx.Play(owner, new[] { architect }, ShinGetterBeamStyle.GetterBeam),
            ShakeStrength.Medium,
            ShakeDuration.Short);
    }

    private static async Task PlayTornadoDrillHit(Creature owner, Creature architect, int damage)
    {
        await PlayArchitectHit(
            owner,
            architect,
            damage,
            () =>
            {
                VfxCmd.PlayOnCreatureCenter(architect, "vfx/vfx_heavy_blunt");
                return Cmd.Wait(0.18f);
            },
            ShakeStrength.Strong,
            ShakeDuration.Short);
    }

    private static async Task PlayGetterMissileHit(Creature owner, Creature architect, int damage)
    {
        await ShinGetterCombatVfx.PlayBurningGrowl(owner);
        await PlayArchitectHit(
            owner,
            architect,
            damage,
            () =>
            {
                VfxCmd.PlayOnCreatureCenter(architect, "vfx/vfx_starry_impact");
                if (NFireBurstVfx.Create(architect, 1.15f) is { } burst)
                    NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(burst);

                return Cmd.Wait(0.22f);
            },
            ShakeStrength.Strong,
            ShakeDuration.Normal);
    }

    private static async Task PlayArchitectHit(
        Creature owner,
        Creature architect,
        int damage,
        Func<Task> playVfx,
        ShakeStrength shakeStrength,
        ShakeDuration shakeDuration)
    {
        await CreatureCmd.TriggerAnim(owner, "Attack", 0.1f);
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NDamageNumVfx.Create(architect, damage, requireInteractable: false));
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NHitSparkVfx.Create(architect, requireInteractable: false));
        await playVfx();
        await CreatureCmd.TriggerAnim(architect, "Hit", 0f);
        NGame.Instance?.ScreenShake(shakeStrength, shakeDuration);
    }

    private static int[] DivideInOrder(int total)
    {
        int beam = Math.Max(1, total / 3);
        int drill = Math.Max(1, total / 3);
        int missile = Math.Max(1, total - beam - drill);
        return new[] { beam, drill, missile };
    }
}
