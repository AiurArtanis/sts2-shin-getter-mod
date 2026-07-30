using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Relics;

public abstract class ShinGetterRelicBase : RelicModel
{
}

internal interface IInfiniteEvolutionProgressStore
{
    bool InfiniteEvolutionProgressInitialized { get; set; }
    int InfiniteEvolutionStrengthGain { get; set; }
    int InfiniteEvolutionDexterityGain { get; set; }
    int InfiniteEvolutionMaxHpGain { get; set; }
}
