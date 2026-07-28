using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Encounters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;

namespace ShinGetterMod.Models.Encounters;

public sealed class SGEncounter_TrialKnightsElite : EncounterModel
{
    public override RoomType RoomType => RoomType.Elite;

    public override IEnumerable<EncounterTag> Tags => new[] { EncounterTag.Knights };

    public override IEnumerable<string> ExtraAssetPaths => new[]
    {
        ModelDb.Affliction<Hexed>().OverlayPath,
    };

    public override IEnumerable<MonsterModel> AllPossibleMonsters => new MonsterModel[]
    {
        ModelDb.Monster<SpectralKnight>(),
        ModelDb.Monster<MagiKnight>(),
    };

    public override float GetCameraScaling() => 0.87f;

    public override Vector2 GetCameraOffset() => Vector2.Down * 50f;

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        new (MonsterModel, string?)[]
        {
            (ModelDb.Monster<SpectralKnight>().ToMutable(), null),
            (ModelDb.Monster<MagiKnight>().ToMutable(), null),
        };
}
