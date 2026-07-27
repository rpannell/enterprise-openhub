namespace OpenHub.RealtimeMessaging.Api.Managers.Models;

/// <summary>Plain data extracted from the caller's <see cref="System.Security.Claims.ClaimsPrincipal"/>
/// at the Controller layer — the Manager/Engine below never see HttpContext directly.</summary>
public sealed record CallerContextDto(string? RealmSlug, bool IsPlatformAdmin);
