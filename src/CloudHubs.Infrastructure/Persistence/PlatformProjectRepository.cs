using Microsoft.EntityFrameworkCore;
using CloudHubs.Application.Projects;
using CloudHubs.Domain.Projects;

namespace CloudHubs.Infrastructure.Persistence;

public class PlatformProjectRepository : IPlatformProjectRepository
{
    private readonly CloudHubsDbContext _dbContext;

    public PlatformProjectRepository(CloudHubsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<PlatformProject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.PlatformProjects.AsNoTracking().OrderBy(project => project.Name).ToListAsync(cancellationToken);
    }

    public Task<PlatformProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.PlatformProjects.FirstOrDefaultAsync(project => project.Id == id, cancellationToken);
    }

    public Task<bool> SlugExistsAsync(string slug, Guid? exceptId = null, CancellationToken cancellationToken = default)
    {
        return _dbContext.PlatformProjects.AnyAsync(
            project => project.Slug == slug && (!exceptId.HasValue || project.Id != exceptId.Value),
            cancellationToken);
    }

    public async Task<PlatformProject> AddAsync(PlatformProject project, CancellationToken cancellationToken = default)
    {
        _dbContext.PlatformProjects.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task<PlatformProject> UpdateAsync(PlatformProject project, CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.PlatformProjects.FindAsync([id], cancellationToken);
        if (project is null) return false;
        _dbContext.PlatformProjects.Remove(project);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}