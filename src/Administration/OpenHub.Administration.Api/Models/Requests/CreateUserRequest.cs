using System.ComponentModel;

namespace OpenHub.Administration.Api.Models.Requests;

public class CreateUserRequest
{
    [Description("Email address. Also used as the Keycloak username — there is no separate username field in this app. A temporary password is set and the account is flagged to require a password reset on first login.")]
    public string Email { get; set; } = null!;

    [Description("Given name. Optional.")]
    public string? FirstName { get; set; }

    [Description("Family name. Optional.")]
    public string? LastName { get; set; }

    [Description("Realm roles to grant on creation. Optional, any number. A tenant-admin caller cannot grant \"tenant-admin\" or \"platform-admin\" — only a platform-admin can.")]
    public List<string> InitialRoles { get; set; } = [];
}
