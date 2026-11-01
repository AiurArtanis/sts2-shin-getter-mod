using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Saves;

namespace ShinGetterMod.Events;

// The native save manager swallows write errors. A completed Task is not proof of a write.
// Once Saved fires, readback failures must never reopen a possibly persisted reward route.
internal static class ShinGetterEventSaveVerification
{
    // Native ToSave is called again by SaveRun. Only these two clocks may advance
    // between the expected snapshot and that write; all gameplay fields must match.
    internal static string StableRunSnapshot(SerializableRun save)
    {
        JsonElement root = JsonSerializer.SerializeToElement(save, JsonSerializationUtility.GetTypeInfo<SerializableRun>());
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in root.EnumerateObject())
                if (property.Name is not ("save_time" or "run_time")) property.WriteTo(writer);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

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
