namespace OpenHub.RealtimeMessaging.Api.Models.Responses.V1;

/// <summary>Confirms the API accepted and dispatched the send — fire-and-forget, so this is not
/// a delivery receipt: it says nothing about whether any client actually received the message.</summary>
public class SendMessageAcceptedResponse
{
    public string? CorrelationId { get; set; }

    public DateTimeOffset SentAtUtc { get; set; }
}
