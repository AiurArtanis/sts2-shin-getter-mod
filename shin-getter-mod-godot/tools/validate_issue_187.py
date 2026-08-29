#!/usr/bin/env python3
"""Static regression gate for issue#187 opening Getter One visibility."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def read(relative_path: str) -> str:
    return (ROOT / relative_path).read_text(encoding="utf-8-sig")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> None:
    getter_one = read("src/Models/Powers/SGP_ShinGetterOne.cs")
    getter_furnace = read("src/Models/Relics/SGR_GetterFurnace.cs")
    emperors_fragment = read("src/Models/Relics/SGR_EmperorsFragment.cs")
    triple_wood = read("src/Models/Relics/SGR_TripleWoodCarving.cs")
    card_base = read("src/Models/Cards/ShinGetterCardBase.cs")

    require("cardSource == null" not in getter_one,
            "null cardSource must not imply the combat-opening fusion")
    require("Dictionary<Creature, int> OpeningApplications" in getter_one
            and "ReferenceEqualityComparer.Instance" in getter_one
            and getter_one.count("lock (OpeningApplicationsLock)") >= 3,
            "opening applications must be thread-safe, reference-isolated, and counted per Creature")
    require("public static async Task ApplyOpening(Creature owner)" in getter_one,
            "Getter One must expose an explicit opening-only apply path")
    opening_start = getter_one.index("public static async Task ApplyOpening(Creature owner)")
    opening_end = getter_one.index("public override async Task AfterApplied", opening_start)
    opening_apply = getter_one[opening_start:opening_end]
    require("BeginOpeningApplication(owner);" in opening_apply
            and "try" in opening_apply
            and "finally" in opening_apply
            and "EndOpeningApplication(owner);" in opening_apply,
            "the explicit opening scope must always be cleaned up")
    require("IsOpeningApplication(base.Owner)" in getter_one,
            "AfterApplied must query the explicit owner-scoped opening marker")

    for relic_name, relic in (
        ("Getter Furnace", getter_furnace),
        ("Emperor's Fragment", emperors_fragment),
    ):
        require("await SGP_ShinGetterOne.ApplyOpening(Owner.Creature);" in relic,
                f"{relic_name} must explicitly mark its initial Getter One application")
        require("await PowerCmd.Apply<SGP_ShinGetterOne>" not in relic,
                f"{relic_name} must not bypass the explicit opening apply path")

    require("NextInt(1, 4)" in triple_wood,
            "Triple Wood Carving must retain one-to-three random transforms")
    require("playVoice: false" in triple_wood,
            "Triple Wood Carving must retain silent driver transform voices")
    require("await PowerCmd.Apply<SGP_ShinGetterOne>" in card_base
            and "creature, cardSource" in card_base,
            "ordinary transforms, including the third carving transform, must retain the normal Getter One apply path")

    print("issue#187 static validation passed")


if __name__ == "__main__":
    main()
