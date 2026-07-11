using System.ComponentModel;
using OpenHub.RealtimeMessaging.Contracts;

namespace OpenHub.RealtimeMessaging.Api.Models.Requests;

public class SendMessageRequest
{
    [Description("Who receives this message: User, Tenant, or Broadcast.")]
    public MessageTarget Target { get; set; }

    [Description("Realm slug of the target tenant. Required for Target=Tenant or Target=User. Ignored for Broadcast.")]
    public string? TenantSlug { get; set; }

    [Description("Keycloak 'sub' claim of the target user. Required for Target=User.")]
    public string? UserId { get; set; }

    [Description("Caller-defined message type, e.g. 'order.updated'. Delivered as-is to the client.")]
    public string MessageType { get; set; } = null!;

    [Description("Arbitrary JSON payload, delivered as-is to the client. Not validated or persisted.")]
    public string PayloadJson { get; set; } = null!;

    [Description("Optional caller-supplied correlation id, echoed back to the client and into logs.")]
    public string? CorrelationId { get; set; }
}
