#!/usr/bin/env python3
"""113 source/reference gates only; not native gameplay acceptance."""

import hashlib
import json
from pathlib import Path
import re
import unittest


PROJECT = Path(__file__).resolve().parents[1]
SOURCE = PROJECT / "src"
OFFICIAL_SHA = "62ECBE91093E1FA862DE4329AAC865209DCB297804CAF0F0520D92D8C0B4A9C3"


def read(relative):
    return (SOURCE / relative).read_text(encoding="utf-8-sig")


class Issue287SourceContracts(unittest.TestCase):
    def test_official_reference(self):
        with (PROJECT / "lib/sts2.dll").open("rb") as stream:
            actual = hashlib.file_digest(stream, "sha256").hexdigest()
        self.assertEqual(actual.upper(), OFFICIAL_SHA)

    def test_manifest_target(self):
        manifest = json.loads((PROJECT / "ShinGetterMod.json").read_text(encoding="utf-8-sig"))
        self.assertEqual(manifest["version"], "v1.4.0-beta.113")
        self.assertEqual(manifest["min_game_version"], "0.113.0")

    def test_event_relic_rng(self):
        source = read("Events/ShinGetterEventInvasionService.cs")
        self.assertEqual(source.count("PullNextRelicFromFront(owner, eventModel.Rng)"), 4)
        self.assertNotRegex(source, r"PullNextRelicFromFront\(owner\)")

    def test_native_dread_character_short_title(self):
        for language in ("eng", "zhs", "jpn"):
            path = PROJECT / "ShinGetterMod/localization" / language / "characters.json"
            entries = json.loads(path.read_text(encoding="utf-8-sig"))
            self.assertEqual(entries["SHIN_GETTER.shortTitle"], entries["SHIN_GETTER.title"], language)

    def test_event_hp_sources(self):
        for relative, expected in (
            ("Events/ShinGetterEventInvasionService.cs", 2),
            ("Events/ShinGetterEventInvasionService.Issue89.cs", 1),
            ("Events/ShinGetterEventInvasionService.Issue238.cs", 1),
        ):
            source = read(relative)
            calls = re.findall(r"CreatureCmd\.LoseMaxHp\((.*?)\);", source, re.S)
            self.assertEqual(len(calls), expected, relative)
            self.assertTrue(all("cardSource: null" in call for call in calls), relative)

    def test_brain_reward_native_ownership(self):
        source = read("Events/ShinGetterEventInvasionService.Issue238.cs")
        route = source.split('case ("BRAIN_LEECH", _):', 1)[1].split('case ("DOLL_ROOM", _):', 1)[0]
        self.assertIn("canonical.Select(c => owner.RunState.CreateCard(c, owner))", route)
        self.assertIn("CardCmd.Upgrade(card, CardPreviewStyle.None)", route)
        self.assertIn("CardPileCmd.Add(selected, PileType.Deck)", route)
        self.assertIn("!result.success", route)
        self.assertIn("ReferenceEquals(result.cardAdded.Owner, owner)", route)
        self.assertIn("owner.Deck.Cards.Contains(result.cardAdded)", route)
        self.assertNotIn("owner.Deck.Cards.Contains(selected)", route)
        self.assertNotIn(".Owner =", route)
        self.assertNotIn(".AddCard(", route)
        self.assertLess(route.index("RunState.CreateCard"), route.index("CardCmd.Upgrade"))
        self.assertLess(route.index("CardCmd.Upgrade"), route.index("CardSelectCmd.FromSimpleGrid"))

    def test_owner_selector(self):
        source = read("Services/ShinGetterAnnotationsCardChoice.cs")
        self.assertIn("CardSelectorStack.GetSelector(owner)", source)
        self.assertIn("local && manager.CardSelectorStack.LocalSelector", source)
        self.assertIn("WaitForRemoteChoice(owner, choiceId)", source)
        self.assertIn("SyncLocalChoice(owner, choiceId", source)
        self.assertIn("Cancelable = true", source)

    def test_native_star_slash_selection(self):
        source = read("Models/Cards/SGC_StarSlash.cs")
        self.assertIn("CardSelectorStack.GetSelector(Owner)", source)
        self.assertIn("!LocalContext.IsMe(Owner) ||", source)
        self.assertIn("CardSelectCmd.FromCombatPile(choiceContext, pile, Owner, prefs)", source)

    def test_no_global_selector_install(self):
        for relative in ("Models/Cards/SGC_StarSlash.cs", "Services/ShinGetterAnnotationsCardChoice.cs"):
            source = read(relative)
            self.assertNotIn("CardSelectCmd.Selector", source)
            self.assertNotRegex(source, r"\.(?:UseSelector|SetTestSelector|PushSelector)\(")

    def test_native_purchase_target(self):
        source = read("Patches/ShinGetterMerchantReactionPatch.cs")
        self.assertIn("nameof(MerchantEntry.OnTryPurchaseUnsynchronized)", source)
        self.assertIn("new[] { typeof(MerchantInventory), typeof(bool) }", source)
        self.assertNotIn("OnTryPurchaseWrapper", source)

    def test_manual_annotations_cancel_keeps_native_action_lifecycle(self):
        source = read("Patches/ShinGetterAnnotationsPreparationPatch.cs")
        self.assertIn("RestorePreparationPresentation(card, action)", source)
        self.assertIn("RemoveCardFromQueueForCancellation(action)", source)
        self.assertIn("LocalContext.IsMe(action.Player)", source)
        self.assertIn("TryCancelCardPlay(card)", source)
        self.assertIn("manager.IsCleaningUp", source)
        self.assertIn("ReferenceEquals(card.Owner, action.Player)", source)
        self.assertIn("ReferenceEquals(manager.DebugOnlyGetState(), action.Player.RunState)", source)
        self.assertIn("GodotObject.IsInstanceValid(queue)", source)
        self.assertIn("GodotObject.IsInstanceValid(hand)", source)
        self.assertIn("catch (Exception cleanupError)", source)
        self.assertIn("Console.Error.WriteLine", source)
        self.assertIn("annotations.ConsumeManualCancellation()", source)
        self.assertIn("return Task.CompletedTask", source)
        self.assertNotIn("action.Cancel()", source)
        self.assertNotRegex(source, r"action\.State\s*=")
        self.assertNotIn("GameActionState.Canceled", source)

    def test_welcome_async_owner(self):
        source = read("Patches/ShinGetterMerchantReactionPatch.cs")
        self.assertIn("nameof(NMerchantInventory.ShowOpenInventory)", source)
        self.assertIn("GetCustomAttribute<AsyncStateMachineAttribute>()", source)
        self.assertIn('AccessTools.Method(machine, "MoveNext")', source)
        self.assertIn("field.FieldType == typeof(NMerchantInventory)", source)
        self.assertIn("OpCodes.Ldfld, inventoryField", source)
        self.assertNotIn("nameof(NMerchantInventory.Open)", source)

    def test_blitz_does_not_consume_certificate(self):
        source = read("Models/Relics/SGR_GoodCitizenCard.cs")
        self.assertGreaterEqual(source.count("GameMode.Blitz"), 2)
        self.assertIn("itemPurchased is BlitzMerchantEntry", source)
        self.assertIn("entry is BlitzMerchantEntry", source)
        self.assertIn("FreePurchaseActIndices.Add(Owner.RunState.CurrentActIndex)", source)

    def test_blitz_does_not_roll_certificate(self):
        source = read("Models/Relics/SGR_GoodCitizenCard.cs")
        self.assertRegex(source, r"override bool IsAllowedInternal\(IRunState runState\)\s*=>\s*"
                         r"runState.GameMode != GameMode.Blitz && base.IsAllowedInternal\(runState\)")

    def test_blitz_direct_and_random_background(self):
        source = read("Patches/ShinGetterVisualsPatch.cs")
        patch = source.split("[HarmonyPatch(typeof(NBlitzScreen))]", 1)[1].split(
            "[HarmonyPatch(typeof(NMultiplayerLoadGameScreen)", 1)[0]
        self.assertIn('FieldRefAccess<NBlitzScreen, Control>("_bgContainer")', patch)
        self.assertIn("nameof(NBlitzScreen.SelectCharacter), new[] { typeof(NCharacterSelectButton) }", patch)
        self.assertIn('"DoRandomCharacterChange", new[] { typeof(Player) }', patch)
        self.assertIn("if (charSelectButton.Character is ShinGetter)", patch)
        self.assertIn("if (player.Character is ShinGetter)", patch)
        self.assertIn("RefreshMarkedBackgrounds(BackgroundContainer(screen))", patch)

    def test_mod_ui_atlas_uses_its_own_path(self):
        path = "res://images/atlases/shin_getter_ui_atlas.png"
        atlas = PROJECT / "images/atlases"
        self.assertFalse((atlas / "ui_atlas.png").exists())
        self.assertFalse((atlas / "ui_atlas.png.import").exists())
        self.assertTrue((atlas / "shin_getter_ui_atlas.png").is_file())
        self.assertIn(f'source_file="{path}"', (atlas / "shin_getter_ui_atlas.png.import").read_text())
        self.assertIn(f'path="{path}"', (atlas / "ui_atlas.sprites/card/energy_shin_getter.tres").read_text())
        self.assertIn(path, read("Nodes/Config/NShinGetterBgmPreviewControls.cs"))

    def test_blitz_native_presentation(self):
        source = read("Nodes/Screens/Shops/ShinGetterMerchantReactions.cs")
        self.assertGreaterEqual(source.count("GameMode.Blitz"), 2)
        self.assertIn("dialogue.ShowOnInventoryOpen()", source)
        self.assertIn("dialogue.ShowForPurchaseAttempt(status)", source)

    def test_event_selection_screen_113(self):
        source = read("Patches/ShinGetterEventInteractionCheckpointPatch.cs")
        self.assertIn("typeof(NCardGridSelectionScreen)", source)
        self.assertIn("GridAfterPresentation(NCardGridSelectionScreen screen)", source)
        self.assertNotIn("AccessTools.Method(typeof(NSimpleCardSelectScreen)", source)
        self.assertIn("BeforeSelectionResult(screen.CardsSelected())", source)

    def test_custom_standard_not_solo_progress(self):
        source = read("Services/ShinGetterBondSession.cs")
        self.assertIn("&& !owner.RunState.IsCustomGame", source)
        self.assertIn("|| history.IsCustomGame", source)
        console = read("Diagnostics/ShinGetterDialogueConsoleCmd.cs")
        self.assertIn("|| state.IsCustomGame", console)

    def test_character_selection_callback_113(self):
        source = read("Patches/ShinGetterVisualsPatch.cs")
        self.assertIn("NCharacterSelectButton charSelectButton", source)
        self.assertIn("charSelectButton.Character is ShinGetter", source)
        self.assertNotIn("bool isRandomCharacterResolution", source)
        self.assertIn("StartRunLobbyPlayer player)", source)

    def test_ancient_relic_option_113(self):
        source = read("Patches/ShinGetterTezcataraPatch.cs")
        self.assertIn('"RelicOption", new[] { typeof(RelicModel), typeof(string) }', source)
        self.assertNotIn("typeof(string), typeof(string)", source)


if __name__ == "__main__":
    print("issue#287 source/reference gates; runtime acceptance is separate.", flush=True)
    unittest.main(verbosity=2)
