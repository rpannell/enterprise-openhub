namespace OpenHub.Accessor.KeyCloak;

/// <summary>
/// Thrown when a Keycloak Admin REST call returns 409 Conflict — e.g. creating an
/// organization whose alias/domain already exists, or a user whose username/email
/// is already taken.
/// </summary>
public class KeyCloakAdminConflictException(string message) : Exception(message);
