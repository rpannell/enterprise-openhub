using OpenHub.Accessor.KeyCloak.Models;
using OpenHub.Administration.Api.Managers.Models;
using OpenHub.Administration.Api.Models.Requests;
using OpenHub.Administration.Api.Models.Responses.V1;
using Riok.Mapperly.Abstractions;

namespace OpenHub.Administration.Api.Mappers;

[Mapper]
public partial class UserMapper
{
    public partial CreateUserDto ToDto(CreateUserRequest request);

    public partial UpdateUserDto ToDto(UpdateUserRequest request);

    public partial UserResponse ToResponse(UserDto dto);

    // Hand-written (not Mapperly-generated): roles come from a separate role-mappings
    // call, not a field on KeyCloakUserInfo itself.
    public UserDto ToDto(KeyCloakUserInfo info, IReadOnlyList<string> roles) => new()
    {
        Id = info.Id,
        Username = info.Username,
        Email = info.Email,
        FirstName = info.FirstName,
        LastName = info.LastName,
        Enabled = info.Enabled,
        Roles = roles
    };
}
