using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod.Nodes.Screens.Shops;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_GoodCitizenCard : ShinGetterRelicBase
{
    private int _lastFreeFloor = -1;
    private List<int> _freePurchaseActIndices = new();

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool IsUsedUp => IsUsedThisFloor;

    private bool IsUsedThisFloor => IsMutable && Owner != null && LastFreeFloor == Owner.RunState.TotalFloor;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastFreeFloor
    {
        get => _lastFreeFloor;
        set
        {
            AssertMutable();
            _lastFreeFloor = value;
            Status = IsUsedThisFloor ? RelicStatus.Disabled : RelicStatus.Normal;
        }
    }

    public List<int> FreePurchaseActIndices => _freePurchaseActIndices;

    [SavedProperty]
    private int[] SavedFreePurchaseActIndices
    {
        get => _freePurchaseActIndices.ToArray();
        set
        {
            AssertMutable();
            _freePurchaseActIndices.Clear();
            _freePurchaseActIndices.AddRange(value);
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _freePurchaseActIndices = new List<int>();
    }

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal originalPrice)
    {
        if (player != Owner || player.RunState.CurrentRoom is not MerchantRoom || IsUsedThisFloor || originalPrice <= 0m)
            return originalPrice;

        return 0m;
    }

    public override Task AfterItemPurchased(Player player, MerchantEntry itemPurchased, int goldSpent)
    {
        if (player != Owner || player.RunState.CurrentRoom is not MerchantRoom || IsUsedThisFloor)
            return Task.CompletedTask;

        Flash();
        if (goldSpent == 0)
            FreePurchaseActIndices.Add(Owner.RunState.CurrentActIndex);
        LastFreeFloor = Owner.RunState.TotalFloor;
        ShinGetterMerchantVisuals.RefreshCurrentRoom();
        return Task.CompletedTask;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (!IsUsedThisFloor && Status == RelicStatus.Disabled)
            Status = RelicStatus.Normal;

        return Task.CompletedTask;
    }
}
