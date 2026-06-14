using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Models.CardPools;

public sealed class ShinGetterCardPool : CardPoolModel
{
	public override string Title => "shin_getter";
	public override string CardFrameMaterialPath => "card_frame_shin_getter";
	public override Color DeckEntryCardColor => new Color("4BFEC4");
	public override Color EnergyOutlineColor => new Color("16B98D");
	public override string EnergyColorName => "shin_getter";
	public override bool IsColorless => false;

	protected override CardModel[] GenerateAllCards()
	{
		return new CardModel[]
		{
			ModelDb.Card<SGC_Strike>(),
			ModelDb.Card<SGC_Defend>(),
			ModelDb.Card<SGC_GetterBeam>(),
			ModelDb.Card<SGC_GetterLaunch>(),
			ModelDb.Card<SGC_DiveStrike>(),
			ModelDb.Card<SGC_HurricaneStrike>(),
			ModelDb.Card<SGC_GetterChop>(),
			ModelDb.Card<SGC_GetterElbow>(),
			ModelDb.Card<SGC_ChangeStrike>(),
			ModelDb.Card<SGC_FocusFire>(),
			ModelDb.Card<SGC_GetterTomahawk>(),
			ModelDb.Card<SGC_GetterDrill>(),
			ModelDb.Card<SGC_GetterRush>(),
			ModelDb.Card<SGC_Meltdown>(),
			ModelDb.Card<SGC_Ki>(),
			ModelDb.Card<SGC_Indomitable>(),
			ModelDb.Card<SGC_TacticalRetreat>(),
			ModelDb.Card<SGC_BlackArmor>(),
			ModelDb.Card<SGC_ShedLoad>(),
			ModelDb.Card<SGC_HedgehogTactic>(),
			ModelDb.Card<SGC_ChangeAttack>(),
			ModelDb.Card<SGC_Annihilation>(),
			ModelDb.Card<SGC_HotBlood>(),
			ModelDb.Card<SGC_TomahawkFury>(),
			ModelDb.Card<SGC_GetterRayOverflow>(),
			ModelDb.Card<SGC_TornadoDrill>(),
			ModelDb.Card<SGC_SpiralDrill>(),
			ModelDb.Card<SGC_ExpansionStrike>(),
			ModelDb.Card<SGC_GetterMissile>(),
			ModelDb.Card<SGC_EvolutionResonance>(),
			ModelDb.Card<SGC_SeizeFuture>(),
			ModelDb.Card<SGC_Spirit>(),
			ModelDb.Card<SGC_PartsSwap>(),
			ModelDb.Card<SGC_TripleUnity>(),
			ModelDb.Card<SGC_DarkCape>(),
			ModelDb.Card<SGC_BackupPlan>(),
			ModelDb.Card<SGC_Grapple>(),
			ModelDb.Card<SGC_Jammer>(),
			ModelDb.Card<SGC_Overload>(),
			ModelDb.Card<SGC_Acceleration>(),
			ModelDb.Card<SGC_Insight>(),
			ModelDb.Card<SGC_ChosenOne>(),
			ModelDb.Card<SGC_SaotomeBlueprint>(),
			ModelDb.Card<SGC_WarriorMedal>(),
			ModelDb.Card<SGC_FightingSpirit>(),
			ModelDb.Card<SGC_ChainReaction>(),
			ModelDb.Card<SGC_StarSlash>(),
			ModelDb.Card<SGC_FinalGetterBeam>(),
			ModelDb.Card<SGC_FlashBurst>(),
			ModelDb.Card<SGC_LigerAssault>(),
			ModelDb.Card<SGC_Avalanche>(),
			ModelDb.Card<SGC_PoseidonThunder>(),
			ModelDb.Card<SGC_SteelSpirit>(),
			ModelDb.Card<SGC_GetterWill>(),
			ModelDb.Card<SGC_Specialization>(),
			ModelDb.Card<SGC_IronWall>(),
			ModelDb.Card<SGC_Guts>(),
			ModelDb.Card<SGC_GetterNova>(),
			ModelDb.Card<SGC_BoldPlan>(),
			ModelDb.Card<SGC_GetterRayBurst>(),
			ModelDb.Card<SGC_AwakenedSoul>(),
			ModelDb.Card<SGC_SuperKi>(),
			ModelDb.Card<SGC_EvolutionEngine>(),
			ModelDb.Card<SGC_Enable>(),
			ModelDb.Card<SGC_AntiEvolution>(),
			ModelDb.Card<SGC_Desperation>(),
			ModelDb.Card<SGC_StonerShine>(),
			ModelDb.Card<SGC_ShinForm>(),
			ModelDb.Card<SGC_Radiated>(),
			ModelDb.Card<SGC_InsectVirus>(),
			ModelDb.Card<SGC_InfiniteEvolution>(),
		};
	}
}
