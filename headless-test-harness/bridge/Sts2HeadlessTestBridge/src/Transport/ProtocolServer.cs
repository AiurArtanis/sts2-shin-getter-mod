using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Sts2HeadlessTestBridge.Contract;

namespace Sts2HeadlessTestBridge.Transport;

public sealed record HandshakeContext(
    string SessionId,
    string InstanceId,
    string ProcessEpoch,
    string ConnectionId,
    string NegotiatedProtocol,
    long ResumeFromSeq);

public sealed record ProtocolCaseFailure(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?> Details);

public sealed class ProtocolConnection
{
    private readonly ProtocolServer _server;

    internal ProtocolConnection(ProtocolServer server, HandshakeContext handshake)
    {
        _server = server;
        Handshake = handshake;
    }

    public HandshakeContext Handshake { get; }

    public Task<JsonElement> SendAsync(
        string type,
        string requestId,
        IReadOnlyDictionary<string, object?>? fields = null,
        bool replayed = false,
        bool waitForFlush = false,
        CancellationToken cancellationToken = default)
    {
        JsonElement element = _server.CreateEnvelope(type, requestId, fields, replayed);
        return _server.PublishAsync(element, waitForFlush, cancellationToken);
    }
}

public sealed class ProtocolServer
{
    private const int DefaultReplayCapacity = 2048;
    private const int DefaultOutboundCriticalCapacity = 512;
    private const int TerminalReplayCapacity = 2048;

    private readonly string _pipeName;
    private readonly string _sessionId;
    private readonly string _instanceId;
    private readonly byte[] _token;
    private readonly Func<HandshakeContext, JsonElement> _ackBodyFactory;
    private readonly Func<JsonElement, ProtocolConnection, CancellationToken, Task> _requestHandler;
    private readonly JsonLineCodec _codec;
    private readonly int _replayCapacity;
    private readonly int _outboundCriticalCapacity;
    private readonly Func<CancellationToken, Task>? _writerBarrier;
    private readonly Action<string>? _diagnosticSink;
    private readonly object _stateGate = new();
    private readonly LinkedList<JsonElement> _criticalReplay = new();
    private readonly Dictionary<string, JsonElement> _terminalReplay = new(StringComparer.Ordinal);
    private readonly Queue<string> _terminalOrder = new();
    private readonly Queue<string> _transcriptOrder = new();
    private readonly HashSet<string> _transcripts = new(StringComparer.Ordinal);
    private ProtocolSink? _activeSink;
    private ProtocolCaseFailure? _caseFailure;
    private long _replayFloorSequence;
    private long _sequence;
    private volatile bool _stopRequested;

    public ProtocolServer(
        string pipeName,
        string sessionId,
        string instanceId,
        byte[] token,
        Func<HandshakeContext, JsonElement> ackBodyFactory,
        Func<JsonElement, ProtocolConnection, CancellationToken, Task> requestHandler,
        string? processEpoch = null,
        int replayCapacity = DefaultReplayCapacity,
        int outboundCriticalCapacity = DefaultOutboundCriticalCapacity,
        ProtocolLimits? limits = null,
        Func<CancellationToken, Task>? writerBarrier = null,
        Action<string>? diagnosticSink = null)
    {
        if (token.Length != 32)
            throw new ArgumentException("token must contain exactly 256 bits", nameof(token));
        if (replayCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(replayCapacity));
        if (outboundCriticalCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(outboundCriticalCapacity));
        _pipeName = pipeName;
        _sessionId = sessionId;
        _instanceId = instanceId;
        _token = token.ToArray();
        _ackBodyFactory = ackBodyFactory;
        _requestHandler = requestHandler;
        _codec = new JsonLineCodec(limits);
        _replayCapacity = replayCapacity;
        _outboundCriticalCapacity = outboundCriticalCapacity;
        _writerBarrier = writerBarrier;
        _diagnosticSink = diagnosticSink;
        ProcessEpoch = processEpoch ?? Guid.NewGuid().ToString("D");
    }

    public string ProcessEpoch { get; }
    public bool StopRequested => _stopRequested;

    public ProtocolCaseFailure? CaseFailure
    {
        get
        {
            lock (_stateGate)
                return _caseFailure;
        }
    }

    public void RequestStop() => _stopRequested = true;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!_stopRequested && !cancellationToken.IsCancellationRequested)
        {
            try
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
                await HandleConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException exception)
            {
                Diagnose($"pipe connection closed: {exception.Message}");
            }
            catch (InvalidDataException exception)
            {
                Diagnose($"rejected malformed protocol frame: {exception.Message}");
            }
            catch (JsonException exception)
            {
                Diagnose($"rejected malformed JSON frame: {exception.Message}");
            }
        }
    }

    internal long NextSequence() => Interlocked.Increment(ref _sequence);

    internal JsonElement CreateEnvelope(
        string type,
        string requestId,
        IReadOnlyDictionary<string, object?>? fields,
        bool replayed)
    {
        var envelope = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["protocol"] = "sts2-test/v1",
            ["schema_version"] = 1,
            ["type"] = type,
            ["seq"] = NextSequence(),
            ["request_id"] = requestId,
            ["instance_id"] = _instanceId,
        };
        if (replayed)
            envelope["replayed"] = true;
        if (fields is not null)
        {
            foreach ((string key, object? value) in fields)
                envelope[key] = value;
        }
        return JsonSerializer.SerializeToElement(envelope).Clone();
    }

    internal Task<JsonElement> PublishAsync(
        JsonElement element,
        bool waitForFlush,
        CancellationToken cancellationToken)
    {
        ProtocolSink? sink;
        OutboundItem? outbound = null;
        JsonElement? overflowTerminal = null;
        lock (_stateGate)
        {
            RememberReplayLocked(element);
            RememberTerminalLocked(element);
            sink = _activeSink;
            if (sink is not null && sink.Accepting)
            {
                outbound = new OutboundItem(element.Clone());
                bool outOfBand = IsLatchedFailureTerminal(element);
                bool accepted = outOfBand
                    ? sink.TryWriteOutOfBand(outbound)
                    : sink.TryWriteCritical(outbound);
                if (!accepted && sink.Accepting)
                {
                    overflowTerminal = LatchOverflowLocked(element);
                    outbound = new OutboundItem(overflowTerminal.Value.Clone());
                    sink.TryWriteOutOfBand(outbound);
                }
            }
        }

        JsonElement published = overflowTerminal ?? element;
        if (outbound is null || !waitForFlush)
            return Task.FromResult(published);
        return AwaitPublishedAsync(outbound, published, cancellationToken);
    }

    private static async Task<JsonElement> AwaitPublishedAsync(
        OutboundItem outbound,
        JsonElement result,
        CancellationToken cancellationToken)
    {
        await outbound.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private void RememberReplayLocked(JsonElement element)
    {
        _criticalReplay.AddLast(element.Clone());
        while (_criticalReplay.Count > _replayCapacity)
        {
            JsonElement removed = _criticalReplay.First!.Value;
            if (removed.TryGetProperty("seq", out JsonElement sequence) && sequence.TryGetInt64(out long value))
                _replayFloorSequence = Math.Max(_replayFloorSequence, value);
            _criticalReplay.RemoveFirst();
        }
    }

    private void RememberTerminalLocked(JsonElement element)
    {
        if (!TryGetString(element, "type", out string? type) || type is not ("completed" or "failed")
            || !TryGetString(element, "request_id", out string? requestId))
        {
            return;
        }
        if (!_terminalReplay.ContainsKey(requestId!))
            _terminalOrder.Enqueue(requestId!);
        _terminalReplay[requestId!] = element.Clone();
        while (_terminalOrder.Count > TerminalReplayCapacity)
            _terminalReplay.Remove(_terminalOrder.Dequeue());
    }

    private JsonElement LatchOverflowLocked(JsonElement lost)
    {
        long? lostSequence = lost.TryGetProperty("seq", out JsonElement sequence) && sequence.TryGetInt64(out long value)
            ? value
            : null;
        string requestId = TryGetString(lost, "request_id", out string? identifier) ? identifier! : "__connection__";
        _caseFailure ??= new ProtocolCaseFailure(
            ErrorCodes.ObserverOverflow,
            "live critical outbound queue overflowed; mutation lane is frozen and the case is invalid",
            new Dictionary<string, object?>
            {
                ["first_lost_type"] = TryGetString(lost, "type", out string? type) ? type : null,
                ["first_lost_request_id"] = requestId,
                ["first_lost_seq"] = lostSequence,
                ["outbound_capacity"] = _outboundCriticalCapacity,
            });
        JsonElement terminal = CreateEnvelope(
            "failed",
            requestId,
            new Dictionary<string, object?>
            {
                ["out_of_band"] = true,
                ["case_invalid"] = true,
                ["error"] = Error(_caseFailure.Code, _caseFailure.Message, details: _caseFailure.Details),
            },
            replayed: false);
        RememberTerminalLocked(terminal);
        return terminal;
    }

    private bool IsLatchedFailureTerminal(JsonElement element)
    {
        if (_caseFailure is null || !TryGetString(element, "type", out string? type) || type != "failed")
            return false;
        return element.TryGetProperty("error", out JsonElement error)
            && TryGetString(error, "code", out string? code)
            && code == _caseFailure.Code;
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
        await WriteDirectAsync(pipe, JsonSerializer.SerializeToElement(challenge), cancellationToken).ConfigureAwait(false);

        using JsonDocument? helloDocument = await ReadDocumentAsync(pipe, cancellationToken).ConfigureAwait(false);
        if (helloDocument is null)
            return;
        JsonElement hello = helloDocument.RootElement;
        if (!TryGetString(hello, "protocol", out string? protocol) || protocol != "sts2-test/v1"
            || !TryGetString(hello, "type", out string? helloType) || helloType != "hello"
            || !TryGetString(hello, "session_id", out string? sessionId) || sessionId != _sessionId
            || !TryGetString(hello, "instance_id", out string? instanceId) || instanceId != _instanceId
            || !TryGetString(hello, "process_epoch", out string? processEpoch) || processEpoch != ProcessEpoch
            || !TryGetString(hello, "connection_id", out string? incomingConnection) || incomingConnection != connectionId
            || !TryGetString(hello, "server_nonce", out string? incomingServerNonce) || incomingServerNonce != serverNonce
            || !TryGetString(hello, "client_nonce", out string? clientNonce)
            || !TryGetString(hello, "negotiated_protocol", out string? negotiated) || negotiated != "sts2-test/v1"
            || !TryGetInt64(hello, "resume_from_seq", out long resumeFromSeq) || resumeFromSeq < 0
            || !TryGetString(hello, "client_proof", out string? incomingProof))
        {
            return;
        }

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
        List<JsonElement> replay;
        bool expired;
        long replayFloor;
        long earliestRetained;
        long latest;
        lock (_stateGate)
        {
            replayFloor = _replayFloorSequence;
            latest = _sequence;
            earliestRetained = _criticalReplay.First?.Value.GetProperty("seq").GetInt64() ?? latest + 1;
            expired = resumeFromSeq < replayFloor;
            replay = expired
                ? []
                : BuildReplayLocked(resumeFromSeq);
        }
        JsonElement body = AddResumeStatus(
            _ackBodyFactory(context),
            resumeFromSeq,
            replayFloor,
            earliestRetained,
            latest,
            expired);
        var acknowledgement = new Dictionary<string, object?>
        {
            ["protocol"] = "sts2-test/v1",
            ["schema_version"] = 1,
            ["type"] = "hello_ack",
            ["body"] = body,
            ["body_sha256"] = ProtocolCrypto.AckBodySha256(body),
            ["server_proof"] = ProtocolCrypto.ServerProof(_token, transcript, body),
        };
        await WriteDirectAsync(pipe, JsonSerializer.SerializeToElement(acknowledgement), cancellationToken).ConfigureAwait(false);

        if (expired)
        {
            JsonElement failure = CreateEnvelope(
                "failed",
                "__resume__",
                new Dictionary<string, object?>
                {
                    ["out_of_band"] = true,
                    ["error"] = Error(
                        ErrorCodes.ResumeWindowExpired,
                        "requested resume sequence is older than the retained replay window",
                        details: new Dictionary<string, object?>
                        {
                            ["resume_from_seq"] = resumeFromSeq,
                            ["replay_floor_seq"] = replayFloor,
                            ["latest_seq"] = latest,
                        }),
                },
                replayed: false);
            await WriteDirectAsync(pipe, failure, cancellationToken).ConfigureAwait(false);
            return;
        }

        var sink = new ProtocolSink(
            pipe,
            _codec,
            _outboundCriticalCapacity,
            _writerBarrier,
            cancellationToken);
        lock (_stateGate)
            _activeSink = sink;
        foreach (JsonElement item in replay)
            await WriteDirectAsync(pipe, MarkReplayed(item), cancellationToken).ConfigureAwait(false);
        sink.Start();
        var connection = new ProtocolConnection(this, context);
        bool gracefulStop = false;
        try
        {
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
                    string requestId = request.TryGetProperty("request_id", out JsonElement id)
                        && id.ValueKind == JsonValueKind.String
                        ? id.GetString() ?? "invalid"
                        : "invalid";
                    await connection.SendAsync(
                        "failed",
                        requestId,
                        new Dictionary<string, object?>
                        {
                            ["error"] = Error(ErrorCodes.InvalidArgument, "invalid request envelope"),
                        },
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    continue;
                }
                await _requestHandler(request, connection, cancellationToken).ConfigureAwait(false);
            }
            gracefulStop = _stopRequested;
        }
        finally
        {
            lock (_stateGate)
            {
                if (ReferenceEquals(_activeSink, sink))
                    _activeSink = null;
            }
            if (gracefulStop)
                await sink.CompleteAndDrainAsync().ConfigureAwait(false);
            else
                sink.Abort();
        }
    }

    private List<JsonElement> BuildReplayLocked(long resumeFromSeq)
    {
        var bySequence = new SortedDictionary<long, JsonElement>();
        foreach (JsonElement item in _criticalReplay.Concat(_terminalReplay.Values))
        {
            if (item.TryGetProperty("seq", out JsonElement seq) && seq.TryGetInt64(out long value) && value > resumeFromSeq)
                bySequence[value] = item.Clone();
        }
        return bySequence.Values.ToList();
    }

    private JsonElement AddResumeStatus(
        JsonElement baseBody,
        long requested,
        long replayFloor,
        long earliestRetained,
        long latest,
        bool expired)
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (JsonProperty property in baseBody.EnumerateObject())
            body[property.Name] = property.Value.Clone();
        body["resume"] = new Dictionary<string, object?>
        {
            ["status"] = expired ? "expired" : "ok",
            ["requested_seq"] = requested,
            ["replay_floor_seq"] = replayFloor,
            ["earliest_retained_seq"] = earliestRetained,
            ["latest_seq"] = latest,
            ["error"] = expired
                ? Error(
                    ErrorCodes.ResumeWindowExpired,
                    "requested resume sequence is older than the retained replay window",
                    details: new Dictionary<string, object?>
                    {
                        ["resume_from_seq"] = requested,
                        ["replay_floor_seq"] = replayFloor,
                        ["latest_seq"] = latest,
                    })
                : null,
        };
        body["case_invalid"] = CaseFailure is not null;
        return JsonSerializer.SerializeToElement(body).Clone();
    }

    private static JsonElement MarkReplayed(JsonElement original)
    {
        var value = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (JsonProperty property in original.EnumerateObject())
            value[property.Name] = property.Value.Clone();
        value["replayed"] = true;
        return JsonSerializer.SerializeToElement(value).Clone();
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

    private async Task WriteDirectAsync(Stream stream, JsonElement element, CancellationToken cancellationToken)
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

    private void Diagnose(string message)
    {
        if (_diagnosticSink is not null)
            _diagnosticSink(message);
        else
            Console.Error.WriteLine($"[Sts2HeadlessTestBridge] {message}");
    }

    public static Dictionary<string, object?> Error(
        string code,
        string message,
        bool retryable = false,
        IReadOnlyDictionary<string, object?>? details = null) =>
        new(StringComparer.Ordinal)
        {
            ["code"] = code,
            ["message"] = message,
            ["retryable"] = retryable,
            ["details"] = details is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(details, StringComparer.Ordinal),
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

    private sealed class OutboundItem(JsonElement element)
    {
        public JsonElement Element { get; } = element;
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ProtocolSink(
        Stream stream,
        JsonLineCodec codec,
        int criticalCapacity,
        Func<CancellationToken, Task>? writerBarrier,
        CancellationToken serverCancellation)
    {
        private readonly Channel<OutboundItem> _critical = Channel.CreateBounded<OutboundItem>(
            new BoundedChannelOptions(criticalCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            });
        private readonly Channel<OutboundItem> _outOfBand = Channel.CreateUnbounded<OutboundItem>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        private readonly SemaphoreSlim _available = new(0);
        private readonly CancellationTokenSource _cancellation = CancellationTokenSource.CreateLinkedTokenSource(serverCancellation);
        private Task? _writerTask;
        private volatile bool _accepting = true;

        public bool Accepting => _accepting;

        public bool TryWriteCritical(OutboundItem item)
        {
            if (!_accepting || !_critical.Writer.TryWrite(item))
                return false;
            _available.Release();
            return true;
        }

        public bool TryWriteOutOfBand(OutboundItem item)
        {
            if (!_accepting || !_outOfBand.Writer.TryWrite(item))
                return false;
            _available.Release();
            return true;
        }

        public void Start() => _writerTask = Task.Run(WriteLoopAsync);

        public async Task CompleteAndDrainAsync()
        {
            _accepting = false;
            _critical.Writer.TryComplete();
            _outOfBand.Writer.TryComplete();
            _available.Release();
            if (_writerTask is not null)
                await _writerTask.ConfigureAwait(false);
            _cancellation.Dispose();
            _available.Dispose();
        }

        public void Abort()
        {
            _accepting = false;
            _critical.Writer.TryComplete();
            _outOfBand.Writer.TryComplete();
            _cancellation.Cancel();
            _available.Release();
        }

        private async Task WriteLoopAsync()
        {
            try
            {
                while (true)
                {
                    await _available.WaitAsync(_cancellation.Token).ConfigureAwait(false);
                    OutboundItem? item;
                    if (_outOfBand.Reader.TryRead(out OutboundItem? urgent))
                        item = urgent;
                    else if (_critical.Reader.TryRead(out OutboundItem? normal))
                        item = normal;
                    else
                    {
                        if (!_accepting)
                            return;
                        continue;
                    }

                    if (writerBarrier is not null)
                        await writerBarrier(_cancellation.Token).ConfigureAwait(false);
                    byte[] line = codec.Encode(item.Element);
                    await stream.WriteAsync(line, _cancellation.Token).ConfigureAwait(false);
                    await stream.FlushAsync(_cancellation.Token).ConfigureAwait(false);
                    item.Completion.TrySetResult();
                }
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                FailPending(new IOException("protocol sink was cancelled"));
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidDataException)
            {
                FailPending(exception);
            }
            finally
            {
                _accepting = false;
                _critical.Writer.TryComplete();
                _outOfBand.Writer.TryComplete();
            }
        }

        private void FailPending(Exception exception)
        {
            while (_outOfBand.Reader.TryRead(out OutboundItem? urgent))
                urgent.Completion.TrySetException(exception);
            while (_critical.Reader.TryRead(out OutboundItem? normal))
                normal.Completion.TrySetException(exception);
        }
    }
}
