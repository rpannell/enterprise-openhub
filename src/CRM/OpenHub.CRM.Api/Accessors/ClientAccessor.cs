using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenHub.CRM.Api.Accessors.Data;
using OpenHub.CRM.Api.Accessors.Interfaces;
using OpenHub.CRM.Api.Accessors.Models;

namespace OpenHub.CRM.Api.Accessors;

// Owns the client aggregate (client + its addresses + its contacts). No .Include() calls per
// iDesign's "no joins in Accessors" rule — addresses/contacts are fetched with separate queries
// and composed by the Manager.
public class ClientAccessor(CrmDbContext db, IHttpContextAccessor http) : IClientAccessor
{
    private Guid TenantId =>
        http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    private async Task SetTenantContextAsync(Guid tenantId, CancellationToken ct)
        => await db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_tenant_id', {0}, true)", [tenantId.ToString()], ct);

    public async Task<ClientEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var result = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
            await tx.CommitAsync(ct);
            return result;
        });
    }

    public async Task<IReadOnlyList<ClientEntity>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var results = (IReadOnlyList<ClientEntity>)await db.Clients.AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(ct);
            await tx.CommitAsync(ct);
            return results;
        });
    }

    public async Task<ClientEntity> CreateAsync(ClientEntity entity, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            db.Clients.Add(entity);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return entity;
    }

    public async Task<ClientEntity> UpdateAsync(ClientEntity entity, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            db.Clients.Update(entity);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return entity;
    }

    public async Task SoftDeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var entity = await db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct)
                ?? throw new KeyNotFoundException($"Client {id} not found.");
            entity.DeletedAt = DateTimeOffset.UtcNow;
            entity.DeleteUserId = deleteUserId;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    public async Task<IReadOnlyList<ClientAddressEntity>> GetAddressesAsync(Guid clientId, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var results = (IReadOnlyList<ClientAddressEntity>)await db.ClientAddresses.AsNoTracking()
                .Where(a => a.ClientId == clientId)
                .ToListAsync(ct);
            await tx.CommitAsync(ct);
            return results;
        });
    }

    public async Task<IReadOnlyList<ClientContactEntity>> GetContactsAsync(Guid clientId, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var results = (IReadOnlyList<ClientContactEntity>)await db.ClientContacts.AsNoTracking()
                .Where(c => c.ClientId == clientId)
                .ToListAsync(ct);
            await tx.CommitAsync(ct);
            return results;
        });
    }

    public async Task<IReadOnlyList<ClientContactPhoneEntity>> GetPhonesForContactsAsync(IReadOnlyList<Guid> contactIds, CancellationToken ct = default)
    {
        if (contactIds.Count == 0) return [];

        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var results = (IReadOnlyList<ClientContactPhoneEntity>)await db.ClientContactPhones.AsNoTracking()
                .Where(p => contactIds.Contains(p.ContactId))
                .ToListAsync(ct);
            await tx.CommitAsync(ct);
            return results;
        });
    }

    // Full-replace semantics: simplest correct way to reconcile a submitted address/contact list
    // against what's stored, since the client aggregate is always edited as a whole from the UI.
    public async Task ReplaceAddressesAsync(Guid clientId, IReadOnlyList<ClientAddressEntity> addresses, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var existing = await db.ClientAddresses.Where(a => a.ClientId == clientId).ToListAsync(ct);
            db.ClientAddresses.RemoveRange(existing);
            db.ClientAddresses.AddRange(addresses);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    public async Task ReplaceContactsAsync(
        Guid clientId,
        IReadOnlyList<ClientContactEntity> contacts,
        IReadOnlyList<ClientContactPhoneEntity> phones,
        CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var existing = await db.ClientContacts.Where(c => c.ClientId == clientId).ToListAsync(ct);
            // Deleting the parent contact rows cascades to their phones at the DB level
            // (client_contact_phones.contact_id ON DELETE CASCADE) — no need to load/remove
            // the old phones here.
            db.ClientContacts.RemoveRange(existing);
            db.ClientContacts.AddRange(contacts);
            db.ClientContactPhones.AddRange(phones);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}
