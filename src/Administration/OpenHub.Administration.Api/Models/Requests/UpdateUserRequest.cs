using System.ComponentModel;

namespace OpenHub.Administration.Api.Models.Requests;

public class UpdateUserRequest
{
    [Description("Email address. Also updates the Keycloak username to match — username always equals email in this app.")]
    public string Email { get; set; } = null!;

    [Description("Given name. Optional.")]
    public string? FirstName { get; set; }

    [Description("Family name. Optional.")]
    public string? LastName { get; set; }

    [Description("Whether the account is enabled. Set to false to disable login without deleting the account.")]
    public bool Enabled { get; set; }
}
