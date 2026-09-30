using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Events;

internal static class ShinGetterEventVisitIdentity
{
    internal static string Create(string seed, ulong owner, int act, string coordinate, int point, int room, ModelId model)
        => $"event-v1:{seed}:{owner}:{act}:{coordinate}:{point}:{room}:{model}";
}
