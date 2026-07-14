using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Audio;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 闪光爆裂 | 攻击 | 稀有 | 2费 | 钢之魂流/输出终端/护盾特攻
/// 获得 2 易伤、2 脆弱，造成 10 伤害。每有 1 点气力就对随机敌人造成 5 伤害
/// </summary>
public sealed class SGC_ShiningSpark : ShinGetterCardBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
	{
		new DamageVar(11m, ValueProp.Move),
		new DynamicVar("KiDamage", 6m),
	};

	public SGC_ShiningSpark()
		: base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await PowerCmd.Apply<VulnerablePower>(choiceContext, Owner.Creature, 2m, Owner.Creature, this);
		await PowerCmd.Apply<FrailPower>(choiceContext, Owner.Creature, 2m, Owner.Creature, this);
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target)
			.WithNoAttackerAnim()
			.BeforeDamage(async () =>
			{
				await Task.WhenAll(
					ShinGetterCombatVfx.PlayWhiteFlash(Owner.Creature),
					ShinGetterVoiceService.PlayShiningSparkIntro(Owner));
				await Cmd.Wait(0.2f);
				await Task.WhenAll(
					PlayMovementVfx(() => ShinGetterCombatVfx.PlayRush(Owner.Creature, cardPlay.Target, whiteFlash: true)),
					ShinGetterVoiceService.PlayShiningSparkFollowUp(Owner));
			})
			.WithHitFx("vfx/vfx_starry_impact").Execute(choiceContext);
		int ki = Owner.Creature.GetPower<SGP_Ki>()?.Amount ?? 0;
		if (ki > 0)
		{
			await DamageCmd.Attack(DynamicVars["KiDamage"].BaseValue).WithHitCount(ki).FromCard(this)
				.TargetingRandomOpponents(CombatState)
				.AfterAttackerAnim(AccelerateFollowupAnimations(ki))
				.WithHitFx("vfx/vfx_attack_lightning").Execute(choiceContext);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
		DynamicVars["KiDamage"].UpgradeValueBy(3m);
	}
}
