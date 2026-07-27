namespace OpenHub.RealtimeMessaging.Contracts;

public sealed record MessageEnvelope(
    string MessageType,
    string PayloadJson,
    string? CorrelationId,
    DateTimeOffset SentAtUtc);
