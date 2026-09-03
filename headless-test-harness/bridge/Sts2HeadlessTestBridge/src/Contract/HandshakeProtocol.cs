using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sts2HeadlessTestBridge.Contract;

public sealed record HandshakeTranscript(
    string SessionId,
    string InstanceId,
    string ProcessEpoch,
    string ConnectionId,
    string NegotiatedProtocol,
    long ResumeFromSeq,
    string ServerNonceBase64Url,
    string ClientNonceBase64Url)
{
    public byte[] ToBytes()
    {
        if (ResumeFromSeq < 0)
            throw new InvalidDataException("resume_from_seq must be non-negative");
        string[] values =
        [
            "sts2-test/handshake/v1", SessionId, InstanceId, ProcessEpoch,
            ConnectionId, NegotiatedProtocol, ResumeFromSeq.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ServerNonceBase64Url, ClientNonceBase64Url,
        ];
        using var stream = new MemoryStream();
        foreach (string value in values)
        {
            byte[] part = LengthPrefix(value);
            stream.Write(part);
        }
        return stream.ToArray();
    }

    public static byte[] LengthPrefix(string value)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(value);
        byte[] result = new byte[4 + utf8.Length];
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(0, 4), checked((uint)utf8.Length));
        utf8.CopyTo(result, 4);
        return result;
    }
}

public static class ProtocolCrypto
{
    public static string ClientProof(ReadOnlySpan<byte> token, HandshakeTranscript transcript) =>
        Proof(token, "sts2-test/client-proof/v1", transcript, []);

    public static string AckBodySha256(JsonElement ackBody) =>
        Convert.ToHexStringLower(SHA256.HashData(CanonicalJson.Serialize(ackBody)));

    public static string ServerProof(ReadOnlySpan<byte> token, HandshakeTranscript transcript, JsonElement ackBody) =>
        Proof(token, "sts2-test/server-proof/v1", transcript, HandshakeTranscript.LengthPrefix(AckBodySha256(ackBody)));

    public static bool FixedTimeEqualsHex(string left, string right)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Proof(
        ReadOnlySpan<byte> token,
        string label,
        HandshakeTranscript transcript,
        ReadOnlySpan<byte> suffix)
    {
        if (token.Length < 32)
            throw new InvalidDataException("handshake token must contain at least 256 bits");
        using var stream = new MemoryStream();
        stream.Write(HandshakeTranscript.LengthPrefix(label));
        stream.Write(transcript.ToBytes());
        stream.Write(suffix);
        byte[] digest = HMACSHA256.HashData(token, stream.ToArray());
        return Convert.ToHexStringLower(digest);
    }
}
