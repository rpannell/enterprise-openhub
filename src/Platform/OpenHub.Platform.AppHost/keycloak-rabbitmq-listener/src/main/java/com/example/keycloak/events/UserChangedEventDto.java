package com.example.keycloak.events;

/**
 * Slimmed wire shape published to the {@code user.changed} fanout exchange — see
 * TenantChangedEventDto for why this isn't raw Keycloak AdminEvent JSON. Must match
 * OpenHub.Platform.IdentityEvents.Contracts.UserChangedEvent field-for-field.
 */
public class UserChangedEventDto {

    public String EventType = "UserChanged";
    public String UserId;
    public String TenantId; // present only for ORGANIZATION_MEMBERSHIP events, else null
    public String RealmSlug;
    public String Operation; // "Created" | "Updated" | "Deleted"
    public String OccurredAtUtc;
    public String Email;
    public String FirstName;
    public String LastName;
}
