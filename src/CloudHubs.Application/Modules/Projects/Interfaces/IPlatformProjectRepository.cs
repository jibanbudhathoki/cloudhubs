using CloudHubs.Domain.Entities;

namespace CloudHubs.Application.Modules.Projects.Interfaces;

public interface IPlatformProjectRepository
{
    Task<IReadOnlyCollection<PlatformProject>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PlatformProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(string slug, Guid? exceptId = null, CancellationToken cancellationToken = default);
    Task<PlatformProject> AddAsync(PlatformProject project, CancellationToken cancellationToken = default);
    Task<PlatformProject> UpdateAsync(PlatformProject project, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}