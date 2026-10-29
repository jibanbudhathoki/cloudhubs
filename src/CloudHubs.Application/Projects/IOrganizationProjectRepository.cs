using CloudHubs.Domain.Projects;

namespace CloudHubs.Application.Projects;

public interface IOrganizationProjectRepository
{
    Task<IReadOnlyCollection<Project>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<Project?> GetByIdAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(Guid organizationId, string slug, CancellationToken cancellationToken = default);
    Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default);
    Task<Project> AddIfBelowLimitAsync(Project project, int maximumProjects, CancellationToken cancellationToken = default);
    Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Project project, CancellationToken cancellationToken = default);
}
