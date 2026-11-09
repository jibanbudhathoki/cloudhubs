using CloudHubs.Domain.Entities;

namespace CloudHubs.Application.Modules.Docs.Interfaces;

public interface IOrganizationDocRepository
{
    Task<IReadOnlyCollection<OrganizationDoc>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<OrganizationDoc?> GetByIdAsync(Guid organizationId, Guid docId, CancellationToken cancellationToken = default);
    Task<OrganizationDoc?> GetBySlugAsync(Guid organizationId, string slug, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(Guid organizationId, string slug, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<OrganizationDoc> AddAsync(OrganizationDoc doc, CancellationToken cancellationToken = default);
    Task<OrganizationDoc> UpdateAsync(OrganizationDoc doc, CancellationToken cancellationToken = default);
    Task SetAllowedMembersAsync(Guid docId, IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(OrganizationDoc doc, CancellationToken cancellationToken = default);
}

