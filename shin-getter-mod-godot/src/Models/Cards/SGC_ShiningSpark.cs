#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Audio;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Combat;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 闪光爆裂 | 攻击 | 稀有 | 2费 | 钢之魂流/输出终端/护盾特攻
/// 获得 1 易伤、1 脆弱，造成 11 伤害。每有 1 点气力就对随机敌人造成 6 伤害
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
		if (Owner.Creature.HasPower<SGP_ShinForm>())
			await PowerCmd.Apply<SGP_Ki>(choiceContext, Owner.Creature, 3m, Owner.Creature, this);
		await PowerCmd.Apply<VulnerablePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
		await PowerCmd.Apply<FrailPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
		NShinGetterShiningSparkSequence? sequence = Owner.Creature.HasPower<SGP_ShinForm>()
			? NShinGetterShiningSparkSequence.TryCreate(Owner.Creature, cardPlay.Target)
			: null;
		try
		{
			if (sequence != null)
				await sequence.PlayToImpact(
					() => ShinGetterVoiceService.PlayShiningSparkIntro(Owner),
					() => ShinGetterVoiceService.PlayShiningSparkFollowUp(Owner));
			else
				await PlayShiningSparkFallback(cardPlay.Target);

			await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
				.WithNoAttackerAnim()
				.WithHitFx("vfx/vfx_starry_impact").Execute(choiceContext);
			int ki = Owner.Creature.GetPower<SGP_Ki>()?.Amount ?? 0;
			if (ki > 0 && Owner.Creature.CombatState is { } combatState)
			{
				var followup = DamageCmd.Attack(DynamicVars["KiDamage"].BaseValue).WithHitCount(ki).FromCard(this, cardPlay)
					.TargetingRandomOpponents(combatState);
				if (sequence != null)
					followup.WithNoAttackerAnim();
				else
					followup.AfterAttackerAnim(AccelerateFollowupAnimations(ki));
				await followup.WithHitFx("vfx/vfx_attack_lightning").Execute(choiceContext);
			}
			if (sequence != null)
				await sequence.Recover();
		}
		finally
		{
			sequence?.Close();

		}
	}

	private async Task PlayShiningSparkFallback(Creature target)
	{
		if (!Owner.Creature.HasPower<SGP_ShinForm>())
		{
			await Task.WhenAll(
				ShinGetterCombatVfx.PlayWhiteFlash(Owner.Creature),
				ShinGetterVoiceService.PlayShiningSparkIntro(Owner));
			await Cmd.Wait(0.2f);
			await Task.WhenAll(
				PlayMovementVfx(() => ShinGetterCombatVfx.PlayRush(Owner.Creature, target, whiteFlash: true)),
				ShinGetterVoiceService.PlayShiningSparkFollowUp(Owner));
			return;
		}

		// Missing visual resources do not substitute the axe-carrying DashV2 for the new action.
		await ShinGetterVoiceService.PlayShiningSparkIntro(Owner);
		await ShinGetterCombatVfx.PlayWhiteFlash(Owner.Creature);
		_ = ShinGetterVoiceService.PlayShiningSparkFollowUp(Owner);
		await Cmd.CustomScaledWait(0.2f, 0.2f);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
		DynamicVars["KiDamage"].UpgradeValueBy(3m);
	}
}
