from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SERVICE = (ROOT / "src/Events/ShinGetterEventInvasionService.cs").read_text(encoding="utf-8")


required = (
    "ModelDb.Encounter<KnightsElite>()",
    "ModelDb.Encounter<ByrdonisElite>()",
    "combatState.Encounter.CanonicalInstance, pending.Encounter",
    "EnterCombatWithoutExitingEventMethod.Invoke(",
)
for needle in required:
    if needle not in SERVICE:
        raise AssertionError(f"missing required issue#197 Beta guard: {needle}")

for forbidden in (
    "ModelDb.Encounter<KnightsElite>().ToMutable()",
    "ModelDb.Encounter<ByrdonisElite>().ToMutable()",
    "ReferenceEquals(combatState.Encounter, pending.Encounter)",
):
    if forbidden in SERVICE:
        raise AssertionError(f"stale 109 event-combat API usage remains in 111 Beta: {forbidden}")

print("issue#197 111 Beta static regression: PASS")
