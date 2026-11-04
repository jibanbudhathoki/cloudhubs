using CloudHubs.Application.Modules.Projects.Interfaces;
using CloudHubs.Domain.Entities;

namespace CloudHubs.UnitTests.Fakes;

internal sealed class OrganizationProjectRepositoryStub : IOrganizationProjectRepository
{
    public int CurrentCount { get; init; }
    public Project? AddedProject { get; private set; }
    public Task<IReadOnlyCollection<Project>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<Project>>([]);
    public Task<Project?> GetByIdAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Project?>(null);
    public Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken = default) => Task.FromResult(CurrentCount);
    public Task<bool> SlugExistsAsync(Guid organizationId, string slug, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        AddedProject = project;
        return Task.FromResult(project);
    }
    public Task<Project> AddIfBelowLimitAsync(Project project, int maximumProjects, CancellationToken cancellationToken = default)
    {
        if (maximumProjects > 0 && CurrentCount >= maximumProjects)
            throw new InvalidOperationException($"The subscription plan allows up to {maximumProjects} projects.");
        AddedProject = project;
        return Task.FromResult(project);
    }
    public Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default) => Task.FromResult(project);
    public Task<bool> DeleteAsync(Project project, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
