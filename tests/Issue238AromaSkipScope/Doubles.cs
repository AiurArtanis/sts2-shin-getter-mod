namespace MegaCrit.Sts2.Core.Entities.Players { public sealed class Player { } }
namespace MegaCrit.Sts2.Core.Events { public sealed class EventOption(string textKey) { public string TextKey { get; } = textKey; } }
namespace MegaCrit.Sts2.Core.Models
{
    public readonly record struct ModelId(string Entry);
    public sealed class EventModel(string entry) { public ModelId Id { get; } = new(entry); }
}
namespace MegaCrit.Sts2.Core.Multiplayer.Game { public sealed class EventSynchronizer { } }
