using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_ActivatedSapphire : ShinGetterPlaceholderEventRelic
{
    private int _hits;
    public override bool ShowCounter => true;
    public override int DisplayAmount => _hits;

    public override Task BeforeCombatStart()
    {
        _hits = 0;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    // Invoked by the final HP-loss postfix, after block, all mitigation and Osty redirection.
    internal decimal InterceptFinalHpLoss(Creature target, decimal amount)
    {
        if (target != Owner.Creature || target.CombatState == null || _hits >= 4 || amount < 1m)
            return amount;
        _hits++;
        InvokeDisplayAmountChanged();
        if (_hits != 4) return amount;
        Flash();
        Status = RelicStatus.Disabled;
        return 0m;
    }
}
