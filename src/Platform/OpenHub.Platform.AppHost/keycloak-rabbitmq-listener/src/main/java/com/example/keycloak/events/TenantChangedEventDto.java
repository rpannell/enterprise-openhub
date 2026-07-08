package com.example.keycloak.events;

/**
 * Slimmed wire shape published to the {@code tenant.changed} fanout exchange —
 * deliberately NOT Keycloak's raw AdminEvent JSON, so .NET consumers never take a
 * dependency on Keycloak's internal event model. Field names/casing (PascalCase)
 * and shape must match OpenHub.Platform.IdentityEvents.Contracts.TenantChangedEvent
 * exactly, since consumers deserialize this with System.Text.Json defaults.
 */
public class TenantChangedEventDto {

    public String EventType = "TenantChanged";
    public String TenantId;
    public String RealmSlug;
    public String Operation; // "Created" | "Updated" | "Deleted" — matches IdentityChangeOperation
    public String OccurredAtUtc; // ISO-8601, e.g. java.time.Instant.toString()
    public String Name;
}
