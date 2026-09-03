using System.Text;
using System.Text.Json;

namespace Sts2HeadlessTestBridge.Contract;

public sealed record ProtocolLimits(
    int MaxLineBytes = 1024 * 1024,
    int MaxDepth = 32,
    int MaxStringBytes = 256 * 1024,
    int MaxArrayItems = 10_000);

public sealed class JsonLineCodec(ProtocolLimits? limits = null)
{
    public ProtocolLimits Limits { get; } = limits ?? new ProtocolLimits();

    public JsonDocument Decode(ReadOnlySpan<byte> line)
    {
        if (line.Length < 2 || line[^1] != (byte)'\n' || line[..^1].Contains((byte)'\n') || line.Contains((byte)'\r'))
            throw new InvalidDataException("JSONL requires exactly one LF-terminated object");
        if (line.Length > Limits.MaxLineBytes)
            throw new InvalidDataException("JSONL line limit exceeded");
        ReadOnlySpan<byte> body = line[..^1];
        if (body.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) || body.Contains((byte)0))
            throw new InvalidDataException("JSONL BOM and NUL are forbidden");
        JsonDocument document = JsonDocument.Parse(body.ToArray(), new JsonDocumentOptions { MaxDepth = Limits.MaxDepth });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw new InvalidDataException("JSONL top-level value must be an object");
        }
        ValidateLimits(document.RootElement);
        return document;
    }

    public byte[] Encode(JsonElement element)
    {
        ValidateLimits(element);
        byte[] body = CanonicalJson.Serialize(element);
        if (body.Length + 1 > Limits.MaxLineBytes)
            throw new InvalidDataException("JSONL line limit exceeded");
        byte[] line = new byte[body.Length + 1];
        body.CopyTo(line, 0);
        line[^1] = (byte)'\n';
        return line;
    }

    private void ValidateLimits(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (Encoding.UTF8.GetByteCount(property.Name) > Limits.MaxStringBytes)
                        throw new InvalidDataException("JSON string limit exceeded");
                    ValidateLimits(property.Value);
                }
                break;
            case JsonValueKind.Array:
                if (element.GetArrayLength() > Limits.MaxArrayItems)
                    throw new InvalidDataException("JSON array limit exceeded");
                foreach (JsonElement item in element.EnumerateArray())
                    ValidateLimits(item);
                break;
            case JsonValueKind.String:
                if (Encoding.UTF8.GetByteCount(element.GetString() ?? "") > Limits.MaxStringBytes)
                    throw new InvalidDataException("JSON string limit exceeded");
                break;
        }
    }
}
