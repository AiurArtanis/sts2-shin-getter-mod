namespace Sts2HeadlessTestBridge.Contract;

public static class ErrorCodes
{
    public const string InvalidArgument = "E_INVALID_ARGUMENT";
    public const string InvalidPhase = "E_INVALID_PHASE";
    public const string NotFound = "E_NOT_FOUND";
    public const string AmbiguousId = "E_AMBIGUOUS_ID";
    public const string StaleHandle = "E_STALE_HANDLE";
    public const string UnsupportedVersion = "E_UNSUPPORTED_VERSION";
    public const string CapabilityUnavailable = "E_CAPABILITY_UNAVAILABLE";
    public const string TimeoutAction = "E_TIMEOUT_ACTION";
    public const string TimeoutNetwork = "E_TIMEOUT_NETWORK";
    public const string ChoiceRequired = "E_CHOICE_REQUIRED";
    public const string PeerDisconnected = "E_PEER_DISCONNECTED";
    public const string StateDivergence = "E_STATE_DIVERGENCE";
    public const string ProcessExit = "E_PROCESS_EXIT";
    public const string Cancelled = "E_CANCELLED";
    public const string CancelUnsafe = "E_CANCEL_UNSAFE";
    public const string IdempotencyConflict = "E_IDEMPOTENCY_CONFLICT";
    public const string AuthFailed = "E_AUTH_FAILED";
    public const string ServerAuthFailed = "E_SERVER_AUTH_FAILED";
    public const string BrokerExit = "E_BROKER_EXIT";
    public const string MutationBusy = "E_MUTATION_BUSY";
    public const string ActionCorrelationFailed = "E_ACTION_CORRELATION_FAILED";
    public const string ObserverOverflow = "E_OBSERVER_OVERFLOW";
    public const string ResumeWindowExpired = "E_RESUME_WINDOW_EXPIRED";
    public const string ProcessIdentityMismatch = "E_PROCESS_IDENTITY_MISMATCH";
    public const string IsolationBreach = "E_ISOLATION_BREACH";
    public const string EvidenceTampered = "E_EVIDENCE_TAMPERED";
    public const string MainThreadViolation = "E_MAIN_THREAD_VIOLATION";
    public const string ObserverSideEffect = "E_OBSERVER_SIDE_EFFECT";
}
