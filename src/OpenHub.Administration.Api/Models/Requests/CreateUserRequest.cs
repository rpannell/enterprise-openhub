using System.ComponentModel;

namespace OpenHub.Administration.Api.Models.Requests;

public class CreateUserRequest
{
    [Description("Unique username within the realm. Max 100 characters.")]
    public string Username { get; set; } = null!;

    [Description("Email address. A temporary password is set and the account is flagged to require a password reset on first login.")]
    public string Email { get; set; } = null!;

    [Description("Given name. Optional.")]
    public string? FirstName { get; set; }

    [Description("Family name. Optional.")]
    public string? LastName { get; set; }

    [Description("Realm role to grant on creation. Optional. A tenant-admin caller cannot grant \"tenant-admin\" or \"platform-admin\" — only a platform-admin can.")]
    public string? InitialRole { get; set; }
}
