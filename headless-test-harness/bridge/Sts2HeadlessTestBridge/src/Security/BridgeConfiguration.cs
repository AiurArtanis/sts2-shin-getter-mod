using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Sts2HeadlessTestBridge.Security;

public sealed record BridgeConfiguration(
    string SessionId,
    string InstanceId,
    string PipeName,
    byte[] Token,
    string OutputRoot)
{
    private static readonly Regex SafeId = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant);

    public static BridgeConfiguration? Load()
    {
        if (!StringComparer.Ordinal.Equals(Environment.GetEnvironmentVariable("STS2_TEST_ENABLE"), "1"))
            return null;
        string session = Required("STS2_TEST_SESSION_ID");
        string instance = Required("STS2_TEST_INSTANCE_ID");
        string pipe = Required("STS2_TEST_PIPE");
        string tokenValue = Required("STS2_TEST_TOKEN");
        string output = Required("STS2_TEST_OUTPUT_ROOT");
        if (!SafeId.IsMatch(session) || !SafeId.IsMatch(instance) || !SafeId.IsMatch(pipe))
            throw new InvalidDataException("test bridge session, instance, or pipe identifier is unsafe");
        byte[] token = DecodeBase64Url(tokenValue);
        if (token.Length != 32)
            throw new InvalidDataException("STS2_TEST_TOKEN must contain exactly 256 bits");
        string outputRoot = SessionRootGuard.Validate(output);
        return new BridgeConfiguration(session, instance, pipe, token, outputRoot);
    }

    public void DestroySecret() => CryptographicOperations.ZeroMemory(Token);

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) ?? throw new InvalidDataException($"missing required test bridge variable: {name}");

    private static byte[] DecodeBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
