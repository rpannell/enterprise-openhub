using Microsoft.AspNetCore.Authorization;

namespace OpenHub.Administration.Api.Auth;

/// <summary>Caller must hold the "platform-admin" realm role from a token issued by the platform-admin realm.</summary>
public sealed class PlatformAdminRequirement : IAuthorizationRequirement;
