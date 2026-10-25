#!/usr/bin/env python3
"""Source-only guards for cheat aliases, native queue/persistence and effect ordering."""
import json
from pathlib import Path
from validate_b130_core import block, has, ordered, require

ROOT = Path(__file__).resolve().parents[1]


def source(relative):
    return (ROOT / relative).read_text(encoding="utf-8-sig")


def activation_contract(text):
    activate = block(text, "internal static CmdResult TryActivate(")
    for fragment in ("owner?.Character is not ShinGetter", "owner.PlayerCombatState == null",
                     "!IsActive(owner, combat)", "owner.Creature.GetPower<SGP_Seal>() != null"):
        has(activate, fragment, "Invalid context must reject before effects")
    ordered(activate, ["bool multiplayer = run.Players.Count > 1;",
                       "multiplayer && run.Modifiers.Any(modifier => modifier is SGM_PhysicsOverride)",
                       "state.Running = true;", "if (multiplayer)",
                       "ModelDb.Modifier<SGM_PhysicsOverride>().ToMutable()",
                       "marker.OnRunCreated(run);", "run.AddModifierDebug(marker);",
                       "return new(Execute(owner, combat, state), true,"],
            "Team claim is native, run-scoped and before the first await")
    require("File." not in text and "GetRelic" not in text,
            "No per-client file IO or removable-relic claim")


def effect_contract(text):
    execute = block(text, "private static async Task Execute(")
    ordered(execute, ["owner.Creature.GetPower<SGP_ShinForm>() != null",
                     "await ShinGetterCardBase.Transform(context, owner, null, playVoice: false);",
                     "await ShinGetterCardBase.TransformTo(context, owner, ShinGetterForm.Getter1, null, playVoice: false);",
                     "await ShinGetterVoiceService.PlayCheatRoar(owner);",
                     "await PlayerCmd.GainEnergy(10m, owner);",
                     "await PowerCmd.Apply<SGP_Ki>(context, owner.Creature, 10m, owner.Creature, null);",
                     "PileType.Hand.GetPile(owner).Cards.ToArray()", "await CardCmd.Exhaust(context, card);",
                     "combat.CreateCard<SGC_Desperation>(owner)",
                     "combat.CreateCard<SGC_Indomitable>(owner)",
                     "combat.CreateCard<SGC_GetterNova>(owner)",
                     "combat.CreateCard<SGC_AwakenedSoul>(owner)",
                     "combat.CreateCard<SGC_StarSlash>(owner)",
                     "await CardPileCmd.AddGeneratedCardToCombat(reward, PileType.Hand, owner);",
                     "ShinGetterExecutionMusicService.TryStartFromCheat(owner);",
                     "finally", "state.Running = false;"], "Effects stay serial and release the running guard")
    require(execute.count("combat.CreateCard<") == 5, "Exactly five combat rewards; permanent deck is untouched")
    require("PileType.Deck" not in execute and "while (" not in execute,
            "No permanent deck write or unbounded exhaustion loop")
    require(execute.count("if (!IsActive(owner, combat)) return;") >= 4,
            "End-of-combat checks guard asynchronous phases")


def main():
    service = source("src/Services/ShinGetterCheatService.cs")
    activation_contract(service)
    effect_contract(service)
    cmd = source("src/Diagnostics/ShinGetterCheatConsoleCmd.cs")
    has(cmd, "public override bool IsNetworked => true;", "Native console requests must be synchronized")
    patch = source("src/Patches/ShinGetterCheatConsolePatch.cs")
    for phrase in ("现在可不是计较物理法则的时候", "物理法則もあったもんじゃねえな",
                   "Now is not the time to worry about the laws of physics"):
        require(phrase in patch, "All three complete phrases must be present")
    has(patch, "nameof(DevConsole.ProcessCommand), new Type[] { typeof(string) }",
        "Normalize the public string input before native network lookup")
    has(patch, "inputValue = ShinGetterCheatConsoleCmd.CommandName;", "Aliases route to one networked command")
    require(patch.count("ShinGetterCheatConsoleRegistrationPatch.EnsureRegistered(__instance);") == 2,
            "Both public submission and remote dispatch cover pre-existing consoles")
    for fragment in ("input.Length < 3", "phrase.StartsWith(input, StringComparison.OrdinalIgnoreCase)",
                     'CommonPrefix = candidates[0] + " "', "if (candidates.Count == 0) return true;"):
        has(patch, fragment, "Whole-phrase tab completion with ordinary-command fallback")
    has(patch, "StringComparison.OrdinalIgnoreCase", "English casing is ignored")
    voice = source("src/Audio/ShinGetterVoiceService.cs")
    has(block(voice, "internal static Task PlayCheatRoar("), 'LinesByCode["014"]', "Play exactly workbook line 014")
    music = block(source("src/Audio/ShinGetterExecutionMusicService.cs"), "internal static void TryStartFromCheat(")
    ordered(music, ["!ShinGetterChunibyoConfigService.IsBgmEnabled", "state.IsActive", "player.Playing",
                    "state.HasTriggered = true;", "StartPlayback(state);"],
            "Honor master switch and avoid restarting the active execution track")
    for language in ("zhs", "eng", "jpn"):
        loc = json.loads(source(f"ShinGetterMod/localization/{language}/modifiers.json"))
        require(all(loc.get(f"SGM_PHYSICS_OVERRIDE.{field}") for field in ("title", "description")),
                "Saved marker has three-language UI text")
    rejected = 0
    for contract, old, new in [
        (activation_contract, "state.Running = true;", "state.Running = false;"),
        (activation_contract, "run.Modifiers.Any", "owner.Relics.Any"),
        (activation_contract, "run.AddModifierDebug(marker);", ""),
        (effect_contract, "await PlayerCmd.GainEnergy(10m, owner);", "await PlayerCmd.GainEnergy(1m, owner);"),
        (effect_contract, "await CardCmd.Exhaust(context, card);", ""),
        (effect_contract, "combat.CreateCard<SGC_StarSlash>(owner)", "combat.CreateCard<SGC_Strike>(owner)"),
        (effect_contract, "finally\n        {\n            state.Running = false;",
         "finally\n        {\n            state.Running = true;"),
    ]:
        require(old in service, "Negative variant must mutate production source")
        try:
            contract(service.replace(old, new, 1))
        except AssertionError:
            rejected += 1
        else:
            raise AssertionError(f"Unsafe variant accepted: {old}")
    print(f"issue#269 source gate PASS; {rejected} unsafe variants rejected. Build/network/gameplay NOT executed.")


if __name__ == "__main__":
    main()
