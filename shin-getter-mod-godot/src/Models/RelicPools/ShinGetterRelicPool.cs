using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Models.RelicPools;

public sealed class ShinGetterRelicPool : RelicPoolModel
{
	public override string EnergyColorName => "shin_getter";
	public override Color LabOutlineColor => StsColors.aqua;

	protected override IEnumerable<RelicModel> GenerateAllRelics()
	{
		var relics = new RelicModel[]
		{
			ModelDb.Relic<SGR_GetterFurnace>(),
			ModelDb.Relic<SGR_EmperorsFragment>(),
			ModelDb.Relic<SGR_BattleInstinct>(),
			ModelDb.Relic<SGR_AlloyPlate>(),
			ModelDb.Relic<SGR_ResearchNotes>(),
			ModelDb.Relic<SGR_MusashiClone>(),
			ModelDb.Relic<SGR_GoodCitizenCard>(),
			ModelDb.Relic<SGR_GoNagaiSmile>(),
			ModelDb.Relic<SGR_KenIshikawaManuscript>(),
			ModelDb.Relic<SGR_YummyCookie>(),
		};

		return relics
			.Concat(WeightedShinGetterRelics(relics, weight: 2))
			.Append(ModelDb.Relic<SGR_TripleWoodCarving>());
	}

	private static IEnumerable<RelicModel> WeightedShinGetterRelics(IEnumerable<RelicModel> relics, int weight)
	{
		foreach (RelicModel relic in relics)
		{
			if (relic.Rarity != RelicRarity.Starter
				&& relic.Rarity != RelicRarity.Ancient
				&& relic.Rarity != RelicRarity.Event)
			{
				for (int i = 1; i < weight; i++)
					yield return relic;
			}
		}
	}
}
