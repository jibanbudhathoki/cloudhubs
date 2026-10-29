using CloudHubs.Contracts.Organizations;
using CloudHubs.Domain.Organizations;

namespace CloudHubs.Application.Organizations;

public interface IOrganizationRepository
{
    Task<IReadOnlyCollection<OrganizationSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(string slug, Guid? exceptId = null, CancellationToken cancellationToken = default);
    Task<bool> HasProjectsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Organization> AddAsync(Organization organization, CancellationToken cancellationToken = default);
    Task<Organization> UpdateAsync(Organization organization, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
