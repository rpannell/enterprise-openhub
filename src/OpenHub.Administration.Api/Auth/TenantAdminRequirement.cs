using Microsoft.AspNetCore.Authorization;

namespace OpenHub.Administration.Api.Auth;

/// <summary>Caller must hold the "tenant-admin" or "platform-admin" realm role. Same-org enforcement happens in the Manager layer, not here.</summary>
public sealed class TenantAdminRequirement : IAuthorizationRequirement;
