using CloudHubs.Application.Modules.Projects.Interfaces;
using CloudHubs.Domain.Entities;

namespace CloudHubs.UnitTests.Fakes;

internal sealed class PlatformProjectRepositoryStub : IPlatformProjectRepository
{
    public PlatformProject? AddedProject { get; private set; }

    public Task<IReadOnlyCollection<PlatformProject>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<PlatformProject>>([]);
    public Task<PlatformProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PlatformProject?>(null);
    public Task<bool> SlugExistsAsync(string slug, Guid? exceptId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<PlatformProject> AddAsync(PlatformProject project, CancellationToken cancellationToken = default)
    {
        AddedProject = project;
        return Task.FromResult(project);
    }

    public Task<PlatformProject> UpdateAsync(PlatformProject project, CancellationToken cancellationToken = default) => Task.FromResult(project);
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
}
