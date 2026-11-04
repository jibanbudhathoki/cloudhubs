using CloudHubs.Application.Modules.Organizations.DTOs;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Application.Modules.Organizations.Interfaces;

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
