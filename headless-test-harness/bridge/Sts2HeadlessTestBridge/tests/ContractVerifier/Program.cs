using System.Text.Json;
using Sts2HeadlessTestBridge.Contract;

if (args.Length != 1)
    throw new ArgumentException("usage: ContractVerifier <headless-test-harness-root>");

string root = Path.GetFullPath(args[0]);
string golden = Path.Combine(root, "fixtures", "golden");
string[] protocolFiles =
[
    "challenge.json", "hello.json", "request-play-card.json", "choice-required.json",
];
foreach (string filename in protocolFiles)
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(golden, "protocol", filename)));
    ProtocolContract.ValidateEnvelope(document.RootElement);
    var codec = new JsonLineCodec();
    using JsonDocument roundTrip = codec.Decode(codec.Encode(document.RootElement));
    ProtocolContract.ValidateEnvelope(roundTrip.RootElement);
}

using JsonDocument hello = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(golden, "protocol", "hello.json")));
JsonElement h = hello.RootElement;
var transcript = new HandshakeTranscript(
    ProtocolContract.RequireString(h, "session_id"),
    ProtocolContract.RequireString(h, "instance_id"),
    ProtocolContract.RequireString(h, "process_epoch"),
    ProtocolContract.RequireString(h, "connection_id"),
    ProtocolContract.RequireString(h, "negotiated_protocol"),
    ProtocolContract.RequireLong(h, "resume_from_seq"),
    ProtocolContract.RequireString(h, "server_nonce"),
    ProtocolContract.RequireString(h, "client_nonce"));
byte[] token = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
string expectedProof = ProtocolContract.RequireString(h, "client_proof");
string actualProof = ProtocolCrypto.ClientProof(token, transcript);
if (!ProtocolCrypto.FixedTimeEqualsHex(expectedProof, actualProof))
    throw new InvalidDataException($"client proof mismatch: {actualProof}");

foreach (string relative in new[]
{
    Path.Combine("state", "minimal-state.json"),
    Path.Combine("scenario", "poc-1b.json"),
    Path.Combine("evidence", "minimal-manifest.json"),
})
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(golden, relative)));
    if (document.RootElement.ValueKind != JsonValueKind.Object)
        throw new InvalidDataException($"golden document is not an object: {relative}");
}

Console.WriteLine(JsonSerializer.Serialize(new
{
    ok = true,
    protocol = "sts2-test/v1",
    golden_protocol_files = protocolFiles.Length,
    client_proof = actualProof,
}));
