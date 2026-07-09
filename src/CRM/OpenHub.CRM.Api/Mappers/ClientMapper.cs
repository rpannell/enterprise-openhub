using OpenHub.CRM.Api.Accessors.Models;
using OpenHub.CRM.Api.Managers.Models;
using OpenHub.CRM.Api.Models.Requests;
using OpenHub.CRM.Api.Models.Responses.V1;
using Riok.Mapperly.Abstractions;

namespace OpenHub.CRM.Api.Mappers;

[Mapper]
public partial class ClientMapper
{
    // Accessor -> Manager
    [MapperIgnoreTarget(nameof(ClientDto.SalesPersonName))]
    [MapperIgnoreTarget(nameof(ClientDto.Addresses))]
    [MapperIgnoreTarget(nameof(ClientDto.Contacts))]
    public partial ClientDto ToDto(ClientEntity entity);

    public partial IReadOnlyList<ClientDto> ToDtoList(IEnumerable<ClientEntity> entities);

    public partial ClientAddressDto ToDto(ClientAddressEntity entity);
    public partial IReadOnlyList<ClientAddressDto> ToDtoList(IEnumerable<ClientAddressEntity> entities);

    [MapperIgnoreTarget(nameof(ClientContactDto.Phones))]
    public partial ClientContactDto ToDto(ClientContactEntity entity);
    public partial IReadOnlyList<ClientContactDto> ToDtoList(IEnumerable<ClientContactEntity> entities);

    public partial ClientContactPhoneDto ToDto(ClientContactPhoneEntity entity);
    public partial IReadOnlyList<ClientContactPhoneDto> ToDtoList(IEnumerable<ClientContactPhoneEntity> entities);

    // Manager -> Accessor
    [MapperIgnoreSource(nameof(ClientDto.SalesPersonName))]
    [MapperIgnoreSource(nameof(ClientDto.Addresses))]
    [MapperIgnoreSource(nameof(ClientDto.Contacts))]
    public partial ClientEntity ToEntity(ClientDto dto);

    [MapperIgnoreTarget(nameof(ClientAddressEntity.TenantId))]
    [MapperIgnoreTarget(nameof(ClientAddressEntity.CreatedAt))]
    [MapperIgnoreTarget(nameof(ClientAddressEntity.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ClientAddressEntity.DeletedAt))]
    public partial ClientAddressEntity ToEntity(ClientAddressDto dto);

    [MapperIgnoreSource(nameof(ClientContactDto.Phones))]
    [MapperIgnoreTarget(nameof(ClientContactEntity.TenantId))]
    [MapperIgnoreTarget(nameof(ClientContactEntity.CreatedAt))]
    [MapperIgnoreTarget(nameof(ClientContactEntity.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ClientContactEntity.DeletedAt))]
    public partial ClientContactEntity ToEntity(ClientContactDto dto);

    [MapperIgnoreTarget(nameof(ClientContactPhoneEntity.TenantId))]
    [MapperIgnoreTarget(nameof(ClientContactPhoneEntity.CreatedAt))]
    [MapperIgnoreTarget(nameof(ClientContactPhoneEntity.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ClientContactPhoneEntity.DeletedAt))]
    public partial ClientContactPhoneEntity ToEntity(ClientContactPhoneDto dto);

    // Controller -> Manager
    [MapperIgnoreTarget(nameof(ClientDto.Id))]
    [MapperIgnoreTarget(nameof(ClientDto.TenantId))]
    [MapperIgnoreTarget(nameof(ClientDto.SalesPersonName))]
    [MapperIgnoreTarget(nameof(ClientDto.KeycloakOrganizationId))]
    [MapperIgnoreTarget(nameof(ClientDto.KeycloakOrganizationAlias))]
    [MapperIgnoreTarget(nameof(ClientDto.LastActivityAt))]
    [MapperIgnoreTarget(nameof(ClientDto.CreateUserId))]
    [MapperIgnoreTarget(nameof(ClientDto.ModifyUserId))]
    [MapperIgnoreTarget(nameof(ClientDto.CreatedAt))]
    [MapperIgnoreTarget(nameof(ClientDto.UpdatedAt))]
    [MapperIgnoreSource(nameof(CreateClientRequest.Addresses))]
    [MapperIgnoreSource(nameof(CreateClientRequest.Contacts))]
    [MapperIgnoreTarget(nameof(ClientDto.Addresses))]
    [MapperIgnoreTarget(nameof(ClientDto.Contacts))]
    public partial ClientDto ToDto(CreateClientRequest request);

    [MapperIgnoreTarget(nameof(ClientAddressDto.Id))]
    [MapperIgnoreTarget(nameof(ClientAddressDto.ClientId))]
    public partial ClientAddressDto ToDto(ClientAddressRequest request);

    [MapperIgnoreTarget(nameof(ClientContactDto.Id))]
    [MapperIgnoreTarget(nameof(ClientContactDto.ClientId))]
    [MapperIgnoreTarget(nameof(ClientContactDto.Phones))]
    [MapperIgnoreSource(nameof(ClientContactRequest.Phones))]
    public partial ClientContactDto ToDto(ClientContactRequest request);

    [MapperIgnoreTarget(nameof(ClientContactPhoneDto.Id))]
    [MapperIgnoreTarget(nameof(ClientContactPhoneDto.ContactId))]
    public partial ClientContactPhoneDto ToDto(ContactPhoneRequest request);

    // Manager -> Controller
    [MapperIgnoreSource(nameof(ClientDto.TenantId))]
    [MapperIgnoreSource(nameof(ClientDto.CreateUserId))]
    [MapperIgnoreSource(nameof(ClientDto.ModifyUserId))]
    public partial ClientResponse ToResponse(ClientDto dto);

    public partial IReadOnlyList<ClientResponse> ToResponseList(IEnumerable<ClientDto> dtos);

    public partial ClientAddressResponse ToResponse(ClientAddressDto dto);
    public partial ClientContactResponse ToResponse(ClientContactDto dto);
    public partial ContactPhoneResponse ToResponse(ClientContactPhoneDto dto);
}
