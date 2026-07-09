using OpenHub.CRM.Api.Accessors.Models;

namespace OpenHub.CRM.Api.Accessors.Interfaces;

public interface ISalesOrgApiAccessor
{
    Task<SalesPersonApiResponse?> GetSalesPersonByIdAsync(Guid salesPersonId, CancellationToken ct = default);

    /// <summary>
    /// Every sales person id reporting (transitively) to the given sales person, per
    /// SalesOrg.Api's derived hierarchy (org units + role hierarchy level).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetReportSalesPersonIdsAsync(Guid salesPersonId, CancellationToken ct = default);
}
