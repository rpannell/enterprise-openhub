using Microsoft.AspNetCore.Http;
using OpenHub.Accessor.KeyCloak;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Models;
using OpenHub.Accessor.RabbitMq;
using OpenHub.CRM.Api.Accessors.Interfaces;
using OpenHub.CRM.Api.Accessors.Models;
using OpenHub.CRM.Api.Managers.Interfaces;
using OpenHub.CRM.Api.Managers.Models;
using OpenHub.CRM.Api.Mappers;
using OpenHub.CRM.Api.Models.Requests;
using OpenHub.CRM.Contracts;

namespace OpenHub.CRM.Api.Managers;

public class ClientManager(
    IClientAccessor clientAccessor,
    ISalesOrgApiAccessor salesOrgApiAccessor,
    IKeyCloakOrganizationAccessor keyCloakOrganizationAccessor,
    IKeyCloakRealmAccessor keyCloakRealmAccessor,
    ClientMapper mapper,
    IHttpContextAccessor http,
    IMessagePublisher messagePublisher) : IClientManager
{
    private Guid TenantId =>
        http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    private string TenantSlug =>
        http.HttpContext?.Items["TenantSlug"] as string
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    public async Task<IReadOnlyList<ClientDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await clientAccessor.GetAllAsync(ct);
        var dtos = mapper.ToDtoList(entities);
        return await Task.WhenAll(dtos.Select(d => HydrateAsync(d, ct)));
    }

    public async Task<ClientDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await clientAccessor.GetByIdAsync(id, ct);
        if (entity is null) return null;

        var dto = mapper.ToDto(entity);
        return await HydrateAsync(dto, ct);
    }

    public async Task<ClientDto> CreateAsync(CreateClientRequest request, Guid createUserId, CancellationToken ct = default)
    {
        if (await salesOrgApiAccessor.GetSalesPersonByIdAsync(request.SalesPersonId, ct) is null)
            throw new KeyNotFoundException($"Sales person {request.SalesPersonId} not found.");

        var realm = await keyCloakRealmAccessor.GetRealmBySlugAsync(TenantSlug, ct)
            ?? throw new InvalidOperationException($"Realm '{TenantSlug}' does not exist.");

        var alias = BuildOrganizationAlias(request.Name);
        var organizationId = await CreateOrganizationIdempotentlyAsync(realm.Realm, request.Name, alias, ct);

        var dto = mapper.ToDto(request);
        dto.TenantId = TenantId;
        dto.SalesPersonId = request.SalesPersonId;
        dto.KeycloakOrganizationId = organizationId;
        dto.KeycloakOrganizationAlias = alias;
        dto.LastActivityAt = DateTimeOffset.UtcNow;
        dto.CreateUserId = createUserId;
        dto.ModifyUserId = createUserId;

        var entity = mapper.ToEntity(dto);
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var created = await clientAccessor.CreateAsync(entity, ct);

        await clientAccessor.ReplaceAddressesAsync(created.Id, ToAddressEntities(created.Id, request.Addresses), ct);
        var (contacts, phones) = ToContactEntitiesWithPhones(created.Id, request.Contacts);
        await clientAccessor.ReplaceContactsAsync(created.Id, contacts, phones, ct);

        await PublishAsync(created.Id, createUserId, isDeleted: false, ct);

        return (await GetByIdAsync(created.Id, ct))!;
    }

    public async Task<ClientDto?> UpdateAsync(Guid id, UpdateClientRequest request, Guid modifyUserId, CancellationToken ct = default)
    {
        var existing = await clientAccessor.GetByIdAsync(id, ct);
        if (existing is null) return null;

        if (request.SalesPersonId.HasValue)
        {
            if (await salesOrgApiAccessor.GetSalesPersonByIdAsync(request.SalesPersonId.Value, ct) is null)
                throw new KeyNotFoundException($"Sales person {request.SalesPersonId} not found.");
            existing.SalesPersonId = request.SalesPersonId.Value;
        }
        if (request.Name is not null) existing.Name = request.Name;
        if (request.LegalName is not null) existing.LegalName = request.LegalName;
        if (request.Status is not null) existing.Status = request.Status;
        if (request.Industry is not null) existing.Industry = request.Industry;
        if (request.Website is not null) existing.Website = request.Website;
        if (request.Phone is not null) existing.Phone = request.Phone;
        if (request.AnnualRevenue.HasValue) existing.AnnualRevenue = request.AnnualRevenue;
        if (request.EmployeeCount.HasValue) existing.EmployeeCount = request.EmployeeCount;
        if (request.LeadSource is not null) existing.LeadSource = request.LeadSource;
        if (request.AccountTier is not null) existing.AccountTier = request.AccountTier;
        if (request.FirstContactDate.HasValue) existing.FirstContactDate = request.FirstContactDate;
        if (request.Notes is not null) existing.Notes = request.Notes;
        existing.LastActivityAt = DateTimeOffset.UtcNow;
        existing.ModifyUserId = modifyUserId;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await clientAccessor.UpdateAsync(existing, ct);

        if (request.Addresses is not null)
            await clientAccessor.ReplaceAddressesAsync(id, ToAddressEntities(id, request.Addresses), ct);
        if (request.Contacts is not null)
        {
            var (contacts, phones) = ToContactEntitiesWithPhones(id, request.Contacts);
            await clientAccessor.ReplaceContactsAsync(id, contacts, phones, ct);
        }

        await PublishAsync(updated.Id, modifyUserId, isDeleted: false, ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default)
    {
        var existing = await clientAccessor.GetByIdAsync(id, ct);
        if (existing is null) return false;

        await clientAccessor.SoftDeleteAsync(id, deleteUserId, ct);
        await PublishAsync(id, deleteUserId, isDeleted: true, ct);

        return true;
    }

    private async Task<ClientDto> HydrateAsync(ClientDto dto, CancellationToken ct)
    {
        var salesPerson = await salesOrgApiAccessor.GetSalesPersonByIdAsync(dto.SalesPersonId, ct);
        dto.SalesPersonName = salesPerson is null ? null : $"{salesPerson.FirstName} {salesPerson.LastName}".Trim();

        var addresses = await clientAccessor.GetAddressesAsync(dto.Id, ct);
        dto.Addresses = [.. mapper.ToDtoList(addresses)];

        var contacts = mapper.ToDtoList(await clientAccessor.GetContactsAsync(dto.Id, ct)).ToList();
        var phones = await clientAccessor.GetPhonesForContactsAsync([.. contacts.Select(c => c.Id)], ct);
        var phonesByContact = phones.ToLookup(p => p.ContactId);
        foreach (var contact in contacts)
            contact.Phones = [.. mapper.ToDtoList(phonesByContact[contact.Id])];
        dto.Contacts = contacts;

        return dto;
    }

    private List<ClientAddressEntity> ToAddressEntities(Guid clientId, IEnumerable<ClientAddressRequest> requests)
        => [.. requests.Select(r =>
        {
            var entity = mapper.ToEntity(mapper.ToDto(r));
            entity.Id = Guid.NewGuid();
            entity.TenantId = TenantId;
            entity.ClientId = clientId;
            entity.CreatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            return entity;
        })];

    private (List<ClientContactEntity> Contacts, List<ClientContactPhoneEntity> Phones) ToContactEntitiesWithPhones(
        Guid clientId, IEnumerable<ClientContactRequest> requests)
    {
        var contacts = new List<ClientContactEntity>();
        var phones = new List<ClientContactPhoneEntity>();
        var now = DateTimeOffset.UtcNow;

        foreach (var request in requests)
        {
            var contact = mapper.ToEntity(mapper.ToDto(request));
            contact.Id = Guid.NewGuid();
            contact.TenantId = TenantId;
            contact.ClientId = clientId;
            contact.CreatedAt = now;
            contact.UpdatedAt = now;
            contacts.Add(contact);

            foreach (var phoneRequest in request.Phones)
            {
                var phone = mapper.ToEntity(mapper.ToDto(phoneRequest));
                phone.Id = Guid.NewGuid();
                phone.TenantId = TenantId;
                phone.ContactId = contact.Id;
                phone.CreatedAt = now;
                phone.UpdatedAt = now;
                phones.Add(phone);
            }
        }

        return (contacts, phones);
    }

    private async Task PublishAsync(Guid clientId, Guid userId, bool isDeleted, CancellationToken ct)
        => await messagePublisher.PublishAsync(new ClientChangedEvent
        {
            ClientId = clientId,
            TenantId = TenantId,
            UserId = userId,
            RealmSlug = TenantSlug,
            IsDeleted = isDeleted,
        }, cancellationToken: ct);

    private static string BuildOrganizationAlias(string name)
    {
        var slug = new string([.. name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')])
            .Trim('-');
        return $"{slug}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    /// <summary>
    /// Creates the Keycloak organization, tolerating a 409 two different ways this can happen:
    ///
    /// 1. Alias collision — the alias carries a fresh random suffix each call, so a genuine
    ///    collision with some other, pre-existing organization is astronomically unlikely. In
    ///    practice this means our own POST already succeeded once and got retried by the
    ///    platform's standard HTTP resilience handler after the original response was lost
    ///    (e.g. a slow Keycloak reply tripping the per-attempt timeout).
    /// 2. Name collision — Keycloak organizations also enforce a unique *name* within a realm,
    ///    independent of alias. An orphaned organization (created successfully, but whose client
    ///    row never got written — e.g. a later step in an earlier CreateAsync failed after this
    ///    point) blocks every future attempt to (re-)create a client with that same name, each
    ///    time under a different, never-colliding alias.
    ///
    /// Either way, failing the whole client creation over a 409 we can resolve is unnecessary —
    /// look up what already exists and carry on.
    /// </summary>
    private async Task<string> CreateOrganizationIdempotentlyAsync(
        string realm, string name, string alias, CancellationToken ct)
    {
        try
        {
            return await keyCloakOrganizationAccessor.CreateOrganizationAsync(realm, new CreateKeyCloakOrganizationRequest
            {
                Name = name,
                Alias = alias,
                Enabled = true,
                Domains = []
            }, ct);
        }
        catch (KeyCloakAdminConflictException)
        {
            var existing = (await keyCloakOrganizationAccessor.ListOrganizationsAsync(realm, ct))
                .FirstOrDefault(o => o.Alias == alias || string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
                throw; // neither alias nor name matches anything we can find — surface the real conflict

            return existing.Id;
        }
    }
}
