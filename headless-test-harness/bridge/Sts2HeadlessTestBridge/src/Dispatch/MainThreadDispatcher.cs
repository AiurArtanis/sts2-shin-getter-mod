using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Sts2HeadlessTestBridge.Transport;

namespace Sts2HeadlessTestBridge.Dispatch;

public sealed record PendingRequest(JsonElement Request, ProtocolConnection Connection);

public sealed class MainThreadDispatcher(int capacity = 128)
{
    private readonly ConcurrentQueue<PendingRequest> _requests = new();
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public bool TryEnqueue(PendingRequest request)
    {
        int count = Interlocked.Increment(ref _count);
        if (count > capacity)
        {
            Interlocked.Decrement(ref _count);
            return false;
        }
        _requests.Enqueue(request);
        return true;
    }

    public IEnumerable<PendingRequest> Drain(int maxRequests = 8, double maxMilliseconds = 2.0)
    {
        long started = Stopwatch.GetTimestamp();
        int emitted = 0;
        while (emitted < maxRequests && Stopwatch.GetElapsedTime(started).TotalMilliseconds <= maxMilliseconds)
        {
            if (!_requests.TryDequeue(out PendingRequest? request))
                yield break;
            Interlocked.Decrement(ref _count);
            emitted++;
            yield return request;
        }
    }
}
