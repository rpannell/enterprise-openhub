namespace OpenHub.Administration.Api.Managers.Models;

/// <summary>Identity of the caller, resolved from their JWT — passed into Managers so same-org/escalation checks stay out of Controllers.</summary>
public record AdminCallerContext(bool IsPlatformAdmin, string? RealmSlug);
