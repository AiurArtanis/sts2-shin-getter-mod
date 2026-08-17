#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Nodes.Combat;

/// <summary>
/// Small in-world selector used by Getter Landing. It intentionally uses the same power icons
/// as the form powers instead of opening a card-selection screen.
/// </summary>
internal static class NShinGetterFormChoice
{
    private const string GetterOneIconPath = "res://images/atlases/power_atlas.sprites/s_g_p_shin_getter_one.tres";
    private const string GetterTwoIconPath = "res://images/atlases/power_atlas.sprites/s_g_p_shin_getter_two.tres";
    private const string GetterThreeIconPath = "res://images/atlases/power_atlas.sprites/s_g_p_shin_getter_three.tres";

    public static async Task<ShinGetterForm> Select(
        PlayerChoiceContext choiceContext,
        Player player,
        IReadOnlyList<ShinGetterForm> choices)
    {
        if (choices.Count == 0)
            return ShinGetterForm.Getter1;

        // Replays and automated tests cannot click a world-space control; choose deterministically.
        if (NonInteractiveMode.IsActive)
            return choices[0];

        var synchronizer = RunManager.Instance.PlayerChoiceSynchronizer;
        uint choiceId = synchronizer.ReserveChoiceId(player);
        await choiceContext.SignalPlayerChoiceBegun(PlayerChoiceOptions.None);
        try
        {
            if (!ShouldSelectLocalForm(player))
            {
                int remoteIndex = (await synchronizer.WaitForRemoteChoice(player, choiceId)).AsIndex();
                return choices[Math.Clamp(remoteIndex, 0, choices.Count - 1)];
            }

            int selectedIndex = await SelectLocal(player.Creature, choices);
            synchronizer.SyncLocalChoice(player, choiceId, PlayerChoiceResult.FromIndex(selectedIndex));
            return choices[selectedIndex];
        }
        finally
        {
            await choiceContext.SignalPlayerChoiceEnded();
        }
    }

    private static async Task<int> SelectLocal(Creature creature, IReadOnlyList<ShinGetterForm> choices)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(creature) is not { } creatureNode
            || NCombatRoom.Instance.CombatVfxContainer is not { } vfxContainer)
        {
            return 0;
        }

        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Node2D root = new()
        {
            ZIndex = 140,
            GlobalPosition = creatureNode.GlobalPosition + new Vector2(0f, -178f),
        };
        vfxContainer.AddChild(root);

        var buttons = new List<TextureButton>();
        for (int index = 0; index < choices.Count; index++)
        {
            int choiceIndex = index;
            ShinGetterForm form = choices[index];
            Texture2D? texture = ResourceLoader.Load<Texture2D>(GetIconPath(form));
            if (texture == null)
                continue;

            TextureButton button = new()
            {
                TextureNormal = texture,
                TextureHover = texture,
                TexturePressed = texture,
                Position = new Vector2((index - (choices.Count - 1) / 2f) * 86f - 32f, -32f),
                Size = new Vector2(64f, 64f),
                TooltipText = GetTooltip(form),
                FocusMode = Control.FocusModeEnum.All,
                Modulate = new Color(1f, 1f, 1f, 0f),
            };
            root.AddChild(button);
            buttons.Add(button);
            Tween appear = button.CreateTween();
            appear.TweenProperty(button, "modulate:a", 1f, 0.14f)
                .SetDelay(index * 0.05f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            button.Pressed += () => completion.TrySetResult(choiceIndex);
        }

        // If a resource is unavailable, never leave an interactive combat command waiting.
        if (buttons.Count == 0)
            completion.TrySetResult(0);
        else
        {
            for (int index = 0; index < buttons.Count; index++)
            {
                buttons[index].FocusNeighborLeft = buttons[(index - 1 + buttons.Count) % buttons.Count].GetPath();
                buttons[index].FocusNeighborRight = buttons[(index + 1) % buttons.Count].GetPath();
            }

            // root and every button have entered the tree, so controller navigation can start here.
            buttons[0].TryGrabFocus();
        }

        int selectedIndex = await completion.Task;
        root.SetProcess(false);
        foreach (TextureButton button in buttons)
            button.Disabled = true;

        Tween disappear = root.CreateTween();
        disappear.TweenProperty(root, "modulate:a", 0f, 0.10f);
        await root.ToSignal(disappear, Tween.SignalName.Finished);
        root.QueueFree();
        await root.ToSignal(root, Node.SignalName.TreeExited);
        RestoreHandFocus();
        return selectedIndex;
    }

    private static void RestoreHandFocus()
    {
        if (NPlayerHand.Instance is not { } hand || !GodotObject.IsInstanceValid(hand))
            return;

        Control defaultFocusedControl = hand.DefaultFocusedControl;
        if (GodotObject.IsInstanceValid(defaultFocusedControl))
            defaultFocusedControl.TryGrabFocus();
    }

    // CardSelectCmd keeps its equivalent helper private. Reproduce the official predicate
    // here so this custom in-world selector follows the same multiplayer/replay behaviour.
    private static bool ShouldSelectLocalForm(Player player) =>
        LocalContext.IsMe(player) && RunManager.Instance.NetService.Type != NetGameType.Replay;

    private static string GetIconPath(ShinGetterForm form) => form switch
    {
        ShinGetterForm.Getter1 => GetterOneIconPath,
        ShinGetterForm.Getter2 => GetterTwoIconPath,
        ShinGetterForm.Getter3 => GetterThreeIconPath,
        _ => GetterOneIconPath,
    };

    private static string GetTooltip(ShinGetterForm form) => form switch
    {
        ShinGetterForm.Getter1 => "Getter One",
        ShinGetterForm.Getter2 => "Getter Two",
        ShinGetterForm.Getter3 => "Getter Three",
        _ => "Getter",
    };
}
