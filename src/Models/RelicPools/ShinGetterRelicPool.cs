using System.Collections.Generic;
using Godot;
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
		yield return ModelDb.Relic<GetterFurnace>();
	}
}
