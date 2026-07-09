using System.ComponentModel;

namespace OpenHub.Administration.Api.Models.Responses.V1;

public class UserResponse
{
    [Description("Keycloak user id.")]
    public string Id { get; set; } = null!;

    [Description("Username.")]
    public string Username { get; set; } = null!;

    [Description("Email address.")]
    public string? Email { get; set; }

    [Description("Given name.")]
    public string? FirstName { get; set; }

    [Description("Family name.")]
    public string? LastName { get; set; }

    [Description("Whether the user is enabled.")]
    public bool Enabled { get; set; }

    [Description("Realm roles currently assigned to this user.")]
    public IReadOnlyList<string> Roles { get; set; } = [];

    [Description("Temporary password generated on creation. Only ever present in the response to POST /users — there's no SMTP integration yet, so this is the only way an admin can hand it to the new user. Never returned by GET, PUT, or any other call.")]
    public string? TemporaryPassword { get; set; }
}
