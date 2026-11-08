#!/usr/bin/env python3
"""Source contracts for issue#273. No compilation or runtime acceptance."""
from pathlib import Path
from validate_b130_core import block, compact, has, ordered, require

ROOT = Path(__file__).resolve().parents[1]


def change_contract(text):
    play = block(text, "protected override async Task OnPlay(")
    has(text, "TransformSpeedScale = 2f", "Change Attack transformation is faster than 1.5x")
    has(text, "new DamageVar(7m, ValueProp.Move)", "Damage is unchanged")
    ordered(play, ["int x = ResolveEnergyXValue() + (IsUpgraded ? 1 : 0);",
                   "var voicedForms = new HashSet<ShinGetterForm>();", "for (int i = 0; i < x; i++)",
                   "if (!CanContinueChangeAttack(cardPlay.Target)) break;",
                   "await Transform(choiceContext, Owner, this, playVoice: voicedForms.Add(nextVoiceForm));",
                   "if (!CanContinueChangeAttack(cardPlay.Target)) break;",
                   "var attack = DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this)",
                   '.WithNoAttackerAnim().WithHitFx("vfx/vfx_attack_slash")',
                   "await NShinGetterStaticVisuals.PlayCompleteCreatureAttack(Owner.Creature,",
                   "() => CanContinueChangeAttack(cardPlay.Target)",
                   "? attack.Execute(choiceContext) : Task.CompletedTask,",
                   "speedScale: i < x - 1 ? 2f : 1f);"],
            "Transform, guarded impact and full recovery must remain serial")
    require(play.count("attack.Execute(choiceContext)") == 1, "Exactly one damage execution site per iteration")
    require("PlayAcceleratedFollowupAnimation" not in play and "PlayNormalFollowupAnimation" not in play,
            "Fixed pre-impact delays must not replace complete recovery")
    for fragment in ("forms.Length > 1 ? ShinGetterForm.None",
                     "forms[0] == ShinGetterForm.Getter1 ? ShinGetterForm.Getter2",
                     "forms[0] == ShinGetterForm.Getter2 ? ShinGetterForm.Getter3"):
        has(play, fragment, "Voice budget keys match native form cycling")


def main():
    change = (ROOT / "src/Models/Cards/SGC_ChangeAttack.cs").read_text(encoding="utf-8-sig")
    change_contract(change)
    base = (ROOT / "src/Models/Cards/ShinGetterCardBase.cs").read_text(encoding="utf-8-sig")
    transform = block(base, "public static async Task TransformTo(")
    has(transform, "if (cardSource is SGC_ChangeAttack or SGC_ShiftStrike) await transformVoiceTask;",
        "Both card timelines join transformation voices before attacking")
    for name in ("One", "Two", "Three"):
        power = (ROOT / f"src/Models/Powers/SGP_ShinGetter{name}.cs").read_text(encoding="utf-8-sig")
        has(power, "SGC_ShiftStrike => SGC_ShiftStrike.TransformSpeedScale,", "Shift speed applies in every atomic form")
        has(power, "SGC_TacticalRetreat => SGC_TacticalRetreat.TransformSpeedScale,", "Retreat speed stays scoped")
    shift = (ROOT / "src/Models/Cards/SGC_ShiftStrike.cs").read_text(encoding="utf-8-sig")
    has(shift, "TransformSpeedScale = 1.5f", "Shift transformation is faster; damage rules stay in B130 gate")
    visuals = (ROOT / "src/Nodes/Combat/NShinGetterStaticVisuals.cs").read_text(encoding="utf-8-sig")
    attack = block(visuals, "public static async Task PlayCompleteCreatureAttack(")
    has(attack, "frameUnits / fps / actionSpeed", "Speed scales the finite wait budget")
    ordered(attack, ["frameUnits * 0.5d, duration);", "await onImpact();", "frameUnits, duration);"],
            "Damage is awaited between midpoint and recovery")
    rejected = 0
    for old, new in [
        ("TransformSpeedScale = 2f", "TransformSpeedScale = 1.5f"),
        ("await Transform(choiceContext, Owner, this,", "Transform(choiceContext, Owner, this,"),
        ("playVoice: voicedForms.Add(nextVoiceForm)", "playVoice: true"),
        ("await NShinGetterStaticVisuals.PlayCompleteCreatureAttack", "_ = NShinGetterStaticVisuals.PlayCompleteCreatureAttack"),
        (".WithNoAttackerAnim()", ""),
        ("? attack.Execute(choiceContext) : Task.CompletedTask", "? Task.CompletedTask : attack.Execute(choiceContext)"),
        ("forms[0] == ShinGetterForm.Getter2 ? ShinGetterForm.Getter3", "forms[0] == ShinGetterForm.Getter2 ? ShinGetterForm.Getter1"),
    ]:
        require(old in change, "Mutation must change a production contract")
        try:
            change_contract(change.replace(old, new, 1))
        except AssertionError:
            rejected += 1
        else:
            raise AssertionError(f"Unsafe variant accepted: {old}")
    print(f"issue#273 source gate PASS; {rejected} unsafe variants rejected. Build/gameplay NOT executed.")


if __name__ == "__main__":
    main()
