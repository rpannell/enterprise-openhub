namespace OpenHub.Administration.Api.Managers;

/// <summary>
/// Thrown by a Manager when the caller holds a valid admin policy but the specific
/// operation is out of their scope — a tenant-admin acting on another org's realm, or
/// attempting to assign a protected role. Mapped to <c>403 Forbidden</c> at the endpoint.
/// </summary>
public class ForbiddenOperationException(string message) : Exception(message);
