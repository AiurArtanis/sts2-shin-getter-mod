#nullable enable
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ShinGetterMod.Models.CardPools;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.PotionPools;
using ShinGetterMod.Models.Relics;
using ShinGetterMod.Models.RelicPools;
using ShinGetterMod.Nodes.Combat;

namespace ShinGetterMod.Models.Characters;

public sealed class ShinGetter : CharacterModel
{
	public override CharacterGender Gender => CharacterGender.Masculine;
	protected override CharacterModel? UnlocksAfterRunAs => null;
	public override Color NameColor => new Color("CB282B");
	public override int StartingHp => 72;
	public override int StartingGold => 99;

	public override CardPoolModel CardPool => ModelDb.CardPool<ShinGetterCardPool>();
	public override PotionPoolModel PotionPool => ModelDb.PotionPool<ShinGetterPotionPool>();
	public override RelicPoolModel RelicPool => ModelDb.RelicPool<ShinGetterRelicPool>();

	public override IEnumerable<CardModel> StartingDeck => new CardModel[]
	{
		ModelDb.Card<SGC_Strike>(),
		ModelDb.Card<SGC_Strike>(),
		ModelDb.Card<SGC_Strike>(),
		ModelDb.Card<SGC_Strike>(),
		ModelDb.Card<SGC_Defend>(),
		ModelDb.Card<SGC_Defend>(),
		ModelDb.Card<SGC_Defend>(),
		ModelDb.Card<SGC_Defend>(),
		ModelDb.Card<SGC_GetterLaunch>(),
		ModelDb.Card<SGC_GetterBeam>(),
	};
	public override IReadOnlyList<RelicModel> StartingRelics => new RelicModel[] { ModelDb.Relic<SGR_GetterFurnace>() };
	protected override IEnumerable<string> ExtraAssetPaths => NShinGetterSpriteSequence.GetAllFrameResourcePaths();

	protected override string CharacterSelectIconPath =>
		"res://images/packed/character_select/char_select_shin_getter.png";
	protected override string CharacterSelectLockedIconPath =>
		"res://images/packed/character_select/char_select_shin_getter_locked.png";
	protected override string IconPath =>
		"res://scenes/ui/character_icons/shin_getter_icon.tscn";
	protected override string MapMarkerPath =>
		"res://images/packed/map/icons/map_marker_shin_getter.png";

	public override float AttackAnimDelay => 0f;
	public override float CastAnimDelay => 0f;
	public override Color EnergyLabelOutlineColor => new Color("801212FF");
	public override Color DialogueColor => new Color("590700");
	public override VfxColor SpeechBubbleColor => VfxColor.Red;
	public override Color MapDrawingColor => new Color("4BFEC4");
	public override Color RemoteTargetingLineColor => new Color("E15847FF");
	public override Color RemoteTargetingLineOutline => new Color("801212FF");

	public override string CharacterSelectSfx =>
		"res://audio/sfx/characters/shin_getter/voices/transform.wav";
	public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_ironclad";

	public override List<string> GetArchitectAttackVfx()
	{
		return new List<string> { "vfx/vfx_attack_slash", "vfx/vfx_bloody_impact", "vfx/vfx_attack_blunt" };
	}

	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		var idle = new AnimState("idle_loop", isLooping: true);
		var animator = new CreatureAnimator(idle, controller);
		animator.AddAnyState("Idle", idle);
		return animator;
	}
}
