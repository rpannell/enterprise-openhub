using System.Text.Json.Serialization;

namespace OpenHub.Platform.IdentityEvents.Contracts;

/// <summary>
/// Published to the <c>user.changed</c> durable fanout exchange by the Keycloak
/// RabbitMQ event listener whenever a user is created, updated, or deleted, or an
/// organization membership changes (see <c>keycloak-rabbitmq-listener/README.md</c>).
/// Any domain that needs to keep its own user data in sync binds its own queue to
/// this exchange.
/// </summary>
public sealed class UserChangedEvent
{
    /// <summary>Discriminator — always "UserChanged". Present for diagnostics/logging.</summary>
    public string EventType { get; set; } = "UserChanged";

    /// <summary>Keycloak user id — matches the "uniqueId" used by ProductManager.Api's TenantAccessor.</summary>
    public string UserId { get; set; } = null!;

    /// <summary>
    /// Populated only when this event originated from an organization-membership
    /// change (a user being added to/removed from a tenant) — null for a plain
    /// user create/update/delete, since a Keycloak user isn't tied to a single
    /// organization in its own admin event.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>Keycloak realm the user belongs to (e.g. "client-a").</summary>
    public string RealmSlug { get; set; } = null!;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IdentityChangeOperation Operation { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
