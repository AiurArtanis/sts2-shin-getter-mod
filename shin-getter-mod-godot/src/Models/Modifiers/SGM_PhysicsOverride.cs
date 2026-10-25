using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Modifiers;

// A native saved/networked run marker, independent of removable relics and profiles.
// It is not included in the selectable GoodModifiers/BadModifiers lists.
public sealed class SGM_PhysicsOverride : ModifierModel
{
    protected override string IconPath => "res://images/packed/character_select/char_select_shin_getter.png";
}
