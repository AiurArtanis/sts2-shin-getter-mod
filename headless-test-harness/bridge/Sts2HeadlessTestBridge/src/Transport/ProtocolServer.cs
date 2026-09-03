using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using Sts2HeadlessTestBridge.Contract;

namespace Sts2HeadlessTestBridge.Transport;

public sealed record HandshakeContext(
    string SessionId,
    string InstanceId,
    string ProcessEpoch,
    string ConnectionId,
    string NegotiatedProtocol,
    long ResumeFromSeq);

public sealed class ProtocolConnection
{
    private readonly ProtocolServer _server;
    private readonly Stream _stream;
    private readonly SemaphoreSlim _writer = new(1, 1);

    internal ProtocolConnection(ProtocolServer server, Stream stream, HandshakeContext handshake)
    {
        _server = server;
        _stream = stream;
        Handshake = handshake;
    }

    public HandshakeContext Handshake { get; }

    public Task<JsonElement> SendAsync(
        string type,
        string requestId,
        IReadOnlyDictionary<string, object?>? fields = null,
        bool replayed = false,
        CancellationToken cancellationToken = default)
    {
        var envelope = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["protocol"] = "sts2-test/v1",
            ["schema_version"] = 1,
            ["type"] = type,
            ["seq"] = _server.NextSequence(),
            ["request_id"] = requestId,
            ["instance_id"] = Handshake.InstanceId,
        };
        if (replayed)
            envelope["replayed"] = true;
        if (fields is not null)
        {
            foreach ((string key, object? value) in fields)
                envelope[key] = value;
        }
        JsonElement element = JsonSerializer.SerializeToElement(envelope).Clone();
        return _server.PublishAsync(_stream, _writer, element, cancellationToken);
    }

    internal Task ReplayAsync(JsonElement element, CancellationToken cancellationToken) =>
        _server.WriteOnlyAsync(_stream, _writer, element, cancellationToken);
}

public sealed class ProtocolServer
{
    private const int CriticalReplayCapacity = 2048;
    private readonly string _pipeName;
    private readonly string _sessionId;
    private readonly string _instanceId;
    private readonly byte[] _token;
    private readonly Func<HandshakeContext, JsonElement> _ackBodyFactory;
    private readonly Func<JsonElement, ProtocolConnection, CancellationToken, Task> _requestHandler;
    private readonly JsonLineCodec _codec = new();
    private readonly object _stateGate = new();
    private readonly LinkedList<JsonElement> _criticalReplay = new();
    private readonly Queue<string> _transcriptOrder = new();
    private readonly HashSet<string> _transcripts = new(StringComparer.Ordinal);
    private long _sequence;
    private volatile bool _stopRequested;

    public ProtocolServer(
        string pipeName,
        string sessionId,
        string instanceId,
        byte[] token,
        Func<HandshakeContext, JsonElement> ackBodyFactory,
        Func<JsonElement, ProtocolConnection, CancellationToken, Task> requestHandler,
        string? processEpoch = null)
    {
        if (token.Length != 32)
            throw new ArgumentException("token must contain exactly 256 bits", nameof(token));
        _pipeName = pipeName;
        _sessionId = sessionId;
        _instanceId = instanceId;
        _token = token.ToArray();
        _ackBodyFactory = ackBodyFactory;
        _requestHandler = requestHandler;
        ProcessEpoch = processEpoch ?? Guid.NewGuid().ToString("D");
    }

    public string ProcessEpoch { get; }
    public bool StopRequested => _stopRequested;

    public void RequestStop() => _stopRequested = true;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!_stopRequested && !cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                64 * 1024,
                64 * 1024);
            await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await HandleConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // A disconnected broker may reconnect with resume_from_seq. All
                // critical messages are cached before a pipe write is attempted.
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    internal long NextSequence() => Interlocked.Increment(ref _sequence);

    internal async Task<JsonElement> PublishAsync(
        Stream stream,
        SemaphoreSlim writer,
        JsonElement element,
        CancellationToken cancellationToken)
    {
        lock (_stateGate)
        {
            _criticalReplay.AddLast(element.Clone());
            while (_criticalReplay.Count > CriticalReplayCapacity)
                _criticalReplay.RemoveFirst();
        }
        await WriteOnlyAsync(stream, writer, element, cancellationToken).ConfigureAwait(false);
        return element;
    }

    internal async Task WriteOnlyAsync(
        Stream stream,
        SemaphoreSlim writer,
        JsonElement element,
        CancellationToken cancellationToken)
    {
        byte[] line = _codec.Encode(element);
        await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                await stream.WriteAsync(line, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // Preserve the cached critical event; the broker will resume it.
            }
        }
        finally
        {
            writer.Release();
        }
    }

    private async Task HandleConnectionAsync(Stream pipe, CancellationToken cancellationToken)
    {
        string connectionId = Guid.NewGuid().ToString("D");
        string serverNonce = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = new Dictionary<string, object?>
        {
            ["protocol"] = "sts2-test/v1",
            ["schema_version"] = 1,
            ["type"] = "challenge",
            ["session_id"] = _sessionId,
            ["instance_id"] = _instanceId,
            ["process_epoch"] = ProcessEpoch,
            ["connection_id"] = connectionId,
            ["protocol_min"] = "sts2-test/v1",
            ["protocol_max"] = "sts2-test/v1",
            ["server_nonce"] = serverNonce,
        };
        await WriteHandshakeAsync(pipe, JsonSerializer.SerializeToElement(challenge), cancellationToken).ConfigureAwait(false);

        using JsonDocument? helloDocument = await ReadDocumentAsync(pipe, cancellationToken).ConfigureAwait(false);
        if (helloDocument is null)
            return;
        JsonElement hello = helloDocument.RootElement;
        if (!TryGetString(hello, "protocol", out string? protocol) || protocol != "sts2-test/v1"
            || !TryGetString(hello, "type", out string? type) || type != "hello"
            || !TryGetString(hello, "session_id", out string? sessionId) || sessionId != _sessionId
            || !TryGetString(hello, "instance_id", out string? instanceId) || instanceId != _instanceId
            || !TryGetString(hello, "process_epoch", out string? processEpoch) || processEpoch != ProcessEpoch
            || !TryGetString(hello, "connection_id", out string? incomingConnection) || incomingConnection != connectionId
            || !TryGetString(hello, "server_nonce", out string? incomingServerNonce) || incomingServerNonce != serverNonce
            || !TryGetString(hello, "client_nonce", out string? clientNonce)
            || !TryGetString(hello, "negotiated_protocol", out string? negotiated) || negotiated != "sts2-test/v1"
            || !TryGetInt64(hello, "resume_from_seq", out long resumeFromSeq) || resumeFromSeq < 0
            || !TryGetString(hello, "client_proof", out string? incomingProof))
            return;

        var transcript = new HandshakeTranscript(
            _sessionId, _instanceId, ProcessEpoch, connectionId,
            "sts2-test/v1", resumeFromSeq, serverNonce, clientNonce!);
        string transcriptDigest = Convert.ToHexStringLower(SHA256.HashData(transcript.ToBytes()));
        lock (_stateGate)
        {
            if (_transcripts.Contains(transcriptDigest))
                return;
        }
        string expectedProof = ProtocolCrypto.ClientProof(_token, transcript);
        if (!ProtocolCrypto.FixedTimeEqualsHex(expectedProof, incomingProof!))
            return;
        RememberTranscript(transcriptDigest);

        var context = new HandshakeContext(
            _sessionId, _instanceId, ProcessEpoch, connectionId, "sts2-test/v1", resumeFromSeq);
        JsonElement body = _ackBodyFactory(context).Clone();
        string bodyHash = ProtocolCrypto.AckBodySha256(body);
        var acknowledgement = new Dictionary<string, object?>
        {
            ["protocol"] = "sts2-test/v1",
            ["schema_version"] = 1,
            ["type"] = "hello_ack",
            ["body"] = body,
            ["body_sha256"] = bodyHash,
            ["server_proof"] = ProtocolCrypto.ServerProof(_token, transcript, body),
        };
        await WriteHandshakeAsync(pipe, JsonSerializer.SerializeToElement(acknowledgement), cancellationToken).ConfigureAwait(false);

        var connection = new ProtocolConnection(this, pipe, context);
        List<JsonElement> replay;
        lock (_stateGate)
        {
            replay = _criticalReplay
                .Where(item => item.TryGetProperty("seq", out JsonElement seq) && seq.GetInt64() > resumeFromSeq)
                .Select(item => item.Clone())
                .ToList();
        }
        foreach (JsonElement item in replay)
            await connection.ReplayAsync(item, cancellationToken).ConfigureAwait(false);

        while (!_stopRequested && !cancellationToken.IsCancellationRequested)
        {
            using JsonDocument? requestDocument = await ReadDocumentAsync(pipe, cancellationToken).ConfigureAwait(false);
            if (requestDocument is null)
                return;
            JsonElement request = requestDocument.RootElement.Clone();
            try
            {
                ProtocolContract.ValidateEnvelope(request);
            }
            catch (InvalidDataException)
            {
                string requestId = request.TryGetProperty("request_id", out JsonElement id) ? id.GetString() ?? "invalid" : "invalid";
                await connection.SendAsync(
                    "failed", requestId,
                    new Dictionary<string, object?> { ["error"] = Error(ErrorCodes.InvalidArgument, "invalid request envelope") },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                continue;
            }
            await _requestHandler(request, connection, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<JsonDocument?> ReadDocumentAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[]? line = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
        return line is null ? null : _codec.Decode(line);
    }

    private async Task<byte[]?> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        byte[] single = new byte[1];
        while (buffer.Length < _codec.Limits.MaxLineBytes)
        {
            int count = await stream.ReadAsync(single, cancellationToken).ConfigureAwait(false);
            if (count == 0)
                return buffer.Length == 0 ? null : throw new IOException("pipe closed mid-frame");
            buffer.WriteByte(single[0]);
            if (single[0] == (byte)'\n')
                return buffer.ToArray();
        }
        throw new InvalidDataException("JSONL line limit exceeded");
    }

    private async Task WriteHandshakeAsync(Stream stream, JsonElement element, CancellationToken cancellationToken)
    {
        byte[] line = _codec.Encode(element);
        await stream.WriteAsync(line, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private void RememberTranscript(string digest)
    {
        lock (_stateGate)
        {
            _transcripts.Add(digest);
            _transcriptOrder.Enqueue(digest);
            while (_transcriptOrder.Count > 2048)
                _transcripts.Remove(_transcriptOrder.Dequeue());
        }
    }

    public static Dictionary<string, object?> Error(string code, string message, bool retryable = false) =>
        new(StringComparer.Ordinal)
        {
            ["code"] = code,
            ["message"] = message,
            ["retryable"] = retryable,
            ["details"] = new Dictionary<string, object?>(),
        };

    private static string Base64Url(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryGetString(JsonElement value, string name, out string? result)
    {
        if (value.TryGetProperty(name, out JsonElement child) && child.ValueKind == JsonValueKind.String)
        {
            result = child.GetString();
            return true;
        }
        result = null;
        return false;
    }

    private static bool TryGetInt64(JsonElement value, string name, out long result)
    {
        result = 0;
        return value.TryGetProperty(name, out JsonElement child) && child.TryGetInt64(out result);
    }
}
