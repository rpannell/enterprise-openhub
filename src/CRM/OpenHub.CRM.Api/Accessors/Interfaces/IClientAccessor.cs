using OpenHub.CRM.Api.Accessors.Models;

namespace OpenHub.CRM.Api.Accessors.Interfaces;

public interface IClientAccessor
{
    Task<ClientEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ClientEntity>> GetAllAsync(CancellationToken ct = default);
    Task<ClientEntity> CreateAsync(ClientEntity entity, CancellationToken ct = default);
    Task<ClientEntity> UpdateAsync(ClientEntity entity, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default);

    Task<IReadOnlyList<ClientAddressEntity>> GetAddressesAsync(Guid clientId, CancellationToken ct = default);
    Task<IReadOnlyList<ClientContactEntity>> GetContactsAsync(Guid clientId, CancellationToken ct = default);

    /// <summary>All phones for the given contacts in one query (filtered by contact_id IN (...), not a join).</summary>
    Task<IReadOnlyList<ClientContactPhoneEntity>> GetPhonesForContactsAsync(IReadOnlyList<Guid> contactIds, CancellationToken ct = default);

    Task ReplaceAddressesAsync(Guid clientId, IReadOnlyList<ClientAddressEntity> addresses, CancellationToken ct = default);

    /// <summary>
    /// Replaces the client's full contact list, and each contact's full phone list, in one
    /// transaction. <paramref name="phones"/> must already carry the new ContactId each phone
    /// belongs to (contacts are always replaced wholesale, so a "phone for an existing contact"
    /// never applies — see the ON DELETE CASCADE on client_contact_phones.contact_id).
    /// </summary>
    Task ReplaceContactsAsync(
        Guid clientId,
        IReadOnlyList<ClientContactEntity> contacts,
        IReadOnlyList<ClientContactPhoneEntity> phones,
        CancellationToken ct = default);
}
