namespace OpenHub.RealtimeMessaging.Contracts;

public interface INotificationClient
{
    Task ReceiveMessage(MessageEnvelope envelope);
}
