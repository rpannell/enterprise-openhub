using System.Text.Json.Serialization;

namespace OpenHub.Platform.IdentityEvents.Contracts;

/// <summary>
/// Published to the <c>tenant.changed</c> durable fanout exchange by the Keycloak
/// RabbitMQ event listener whenever an organization is created, updated, or deleted
/// (see <c>keycloak-rabbitmq-listener/README.md</c>). Any domain that needs to keep
/// its own tenant data in sync binds its own queue to this exchange.
/// </summary>
public sealed class TenantChangedEvent
{
    /// <summary>Discriminator — always "TenantChanged". Present for diagnostics/logging.</summary>
    public string EventType { get; set; } = "TenantChanged";

    /// <summary>Keycloak organization id, used as the tenant id throughout this platform.</summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Keycloak realm the organization belongs to (e.g. "client-a"). Lets consumers
    /// request an M2M token for the right tenant realm without maintaining a
    /// separate tenant-to-realm lookup — same convention as ProductChangedEvent.
    /// </summary>
    public string RealmSlug { get; set; } = null!;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IdentityChangeOperation Operation { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>Organization display name at the time of the event, if available.</summary>
    public string? Name { get; set; }
}
