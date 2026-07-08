namespace OpenHub.Administration.Api.Managers.Models;

public record RoleAssignmentDecision(bool Allowed, string? DenyReason);
