using System;
using System.Threading.Tasks;

namespace ShinGetterMod.Events;

// The native save manager swallows write errors. A completed Task is not proof of a write.
// Once Saved fires, readback failures must never reopen a possibly persisted reward route.
internal static class ShinGetterEventSaveVerification
{
    internal sealed class ConfirmedWriteReadbackException : Exception
    {
        internal ConfirmedWriteReadbackException(Exception inner)
            : base("Native write completed but event save readback could not be verified.", inner) { }
    }

    internal static async Task Verify(Func<Task> write, Func<bool> wasSaved, Action readback)
    {
        try
        {
            await write();
            if (!wasSaved()) throw new InvalidOperationException("Native event save did not report a completed write.");
            readback();
        }
        catch (Exception ex) when (wasSaved())
        {
            throw new ConfirmedWriteReadbackException(ex);
        }
    }
}
