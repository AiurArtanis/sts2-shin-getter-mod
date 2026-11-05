using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Runs;

namespace ShinGetterMod.Events;

public struct ShinGetterTrialChallengeMessage : INetMessage, IRunLocationTargetedMessage
{
    public ulong OwnerId;
    public uint RunSeed;
    public RunLocation Location { get; set; }

    public bool ShouldBroadcast => false;
    public NetTransferMode Mode => NetTransferMode.Reliable;
    public LogLevel LogLevel => LogLevel.VeryDebug;
    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(OwnerId);
        writer.WriteUInt(RunSeed);
        writer.Write(Location);
    }

    public void Deserialize(PacketReader reader)
    {
        OwnerId = reader.ReadULong();
        RunSeed = reader.ReadUInt();
        Location = reader.Read<RunLocation>();
    }
}
