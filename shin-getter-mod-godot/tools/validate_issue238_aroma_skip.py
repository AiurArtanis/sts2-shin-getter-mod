"""Narrow source contract for the owned Aroma candidate skip label; no UI acceptance."""
from pathlib import Path
import argparse
import json

ROOT = Path(__file__).resolve().parents[1]
KEY = '"SHIN_GETTER_EVENT_INVASION.AROMA_OF_CHAOS.pages.INITIAL.options.HAYATO"'


def validate(checkpoint, patch):
    start = checkpoint.index("internal static bool IsAromaCandidateSelection")
    predicate = checkpoint[start:checkpoint.index("internal static Task<T> BeforeSelectionResult", start)]
    for token in ("Selection.Value", "session.IsCurrent", 'session.model.Id.Entry == "AROMA_OF_CHAOS"', "session.option.TextKey == " + KEY):
        assert token in predicate, token
    adapter = patch.split("private static Task<IEnumerable<CardModel>> ChooseAfterPresentation", 1)[1].split("private static Task<IEnumerable<CardModel>> GridAfterPresentation", 1)[0]
    for token in (
        "if (ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection)",
        'screen.GetNode<MegaLabel>("SkipButton/Label").SetTextAutoSize(',
        'new LocString("card_reward_ui", "OPTION_SKIP.name").GetFormattedText()',
        "return ShinGetterEventInteractionCheckpoint.BeforeSelectionResult(screen.CardsSelected());",
    ):
        assert token in adapter, token
    assert adapter.index("IsAromaCandidateSelection") < adapter.index("SetTextAutoSize") < adapter.index("BeforeSelectionResult"), "localize only the owned visible screen before the unchanged selection adapter"
    assert '"Skip"' not in adapter and '"跳过"' not in adapter, "reuse native locale lookup, not a hardcoded language"
    grid = patch.split("private static Task<IEnumerable<CardModel>> GridAfterPresentation", 1)[1]
    assert "SetTextAutoSize" not in grid, "do not alter other selectors"


parser = argparse.ArgumentParser()
parser.add_argument("--native-source", type=Path)
args = parser.parse_args()
checkpoint = (ROOT / "src/Events/ShinGetterEventInteractionCheckpoint.cs").read_text(encoding="utf-8-sig")
patch = (ROOT / "src/Patches/ShinGetterEventInteractionCheckpointPatch.cs").read_text(encoding="utf-8-sig")
validate(checkpoint, patch)
mutations = (
    (checkpoint.replace("session.IsCurrent", "true"), patch),
    (checkpoint.replace('session.model.Id.Entry == "AROMA_OF_CHAOS"', "true"), patch),
    (checkpoint.replace("session.option.TextKey == " + KEY, "true"), patch),
    (checkpoint, patch.replace("if (ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection)", "if (true)")),
    (checkpoint, patch.replace('"SkipButton/Label"', '"Banner/Label"')),
    (checkpoint, patch.replace('"OPTION_SKIP.name"', '"OPTION_SKIP"')),
)
for changed_checkpoint, changed_patch in mutations:
    try:
        validate(changed_checkpoint, changed_patch)
    except (AssertionError, ValueError):
        continue
    raise AssertionError("negative mutation was accepted")
if args.native_source:
    for language, expected in {"zhs": "跳过", "eng": "Skip", "jpn": "スキップ"}.items():
        catalog = json.loads((args.native_source / "localization" / language / "card_reward_ui.json").read_text(encoding="utf-8-sig"))
        assert catalog["OPTION_SKIP.name"] == expected, language
    print("formal native zhs/eng/jpn catalog key values PASS (not rendered UI)")
print("issue#238 Aroma skip source PASS / 6 negative controls; GUI/cancel/payment runtime NOT_RUN")
