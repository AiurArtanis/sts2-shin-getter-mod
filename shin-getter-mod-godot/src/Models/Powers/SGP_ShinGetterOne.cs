#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Nodes.Combat;
using ShinGetterMod.Patches;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 一号机形态。变形时获得1活力。
/// </summary>
public sealed class SGP_ShinGetterOne : PowerModel
{
    private static readonly object OpeningApplicationsLock = new();
    private static readonly Dictionary<Creature, int> OpeningApplications =
        new(ReferenceEqualityComparer.Instance);

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public static async Task ApplyOpening(Creature owner)
    {
        BeginOpeningApplication(owner);
        try
        {
            await PowerCmd.Apply<SGP_ShinGetterOne>(
                new ThrowingPlayerChoiceContext(), owner, 1m, owner, null);
        }
        finally
        {
            EndOpeningApplication(owner);
        }
    }

    private static void BeginOpeningApplication(Creature owner)
    {
        lock (OpeningApplicationsLock)
        {
            OpeningApplications.TryGetValue(owner, out int depth);
            OpeningApplications[owner] = depth + 1;
        }
    }

    private static void EndOpeningApplication(Creature owner)
    {
        lock (OpeningApplicationsLock)
        {
            if (!OpeningApplications.TryGetValue(owner, out int depth) || depth <= 1)
                OpeningApplications.Remove(owner);
            else
                OpeningApplications[owner] = depth - 1;
        }
    }

    private static bool IsOpeningApplication(Creature owner)
    {
        lock (OpeningApplicationsLock)
            return OpeningApplications.ContainsKey(owner);
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (base.Owner != null && base.Amount > 0)
        {
            Flash();
            await PowerCmd.Apply<VigorPower>(new ThrowingPlayerChoiceContext(), base.Owner, 1m, base.Owner, null);
            bool isOpeningApplication = IsOpeningApplication(base.Owner);
            float speedScale = cardSource switch
            {
                SGC_ChangeAttack => SGC_ChangeAttack.TransformSpeedScale,
                SGC_TacticalRetreat => SGC_TacticalRetreat.TransformSpeedScale,
                _ => 1f,
            };
            // Relics start the opening fusion only after all combat-start setup is ready,
            // immediately alongside the prepared opening voice.
            if (isOpeningApplication)
                NShinGetterStaticVisuals.PrepareOpeningGetterOneFusion(base.Owner);
            else
                await NShinGetterStaticVisuals.ShowForm(
                    base.Owner,
                    ShinGetterForm.Getter1,
                    speedScale: speedScale);
            ShinGetterCardFramePatch.RefreshVisibleCards();
        }
    }
}
