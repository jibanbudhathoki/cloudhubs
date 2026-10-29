using Microsoft.EntityFrameworkCore;
using Npgsql;
using CloudHubs.Application.Projects;
using CloudHubs.Domain.Projects;

namespace CloudHubs.Infrastructure.Persistence;

public class OrganizationProjectRepository(CloudHubsDbContext dbContext) : IOrganizationProjectRepository
{
    public async Task<IReadOnlyCollection<Project>> GetAllAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.OrganizationId == organizationId)
            .OrderBy(project => project.Name)
            .ToListAsync(cancellationToken);

    public Task<Project?> GetByIdAsync(
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        dbContext.Projects.FirstOrDefaultAsync(
            project => project.OrganizationId == organizationId && project.Id == projectId,
            cancellationToken);

    public Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        dbContext.Projects.CountAsync(project => project.OrganizationId == organizationId, cancellationToken);

    public Task<bool> SlugExistsAsync(
        Guid organizationId,
        string slug,
        CancellationToken cancellationToken = default) =>
        dbContext.Projects.AnyAsync(
            project => project.OrganizationId == organizationId && project.Slug == slug,
            cancellationToken);

    public async Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task<Project> AddIfBelowLimitAsync(
        Project project,
        int maximumProjects,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var lockedOrganization = await dbContext.Organizations
            .FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {project.OrganizationId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        if (lockedOrganization.Count == 0)
        {
            throw new KeyNotFoundException("Organization was not found.");
        }

        var currentCount = await dbContext.Projects.CountAsync(
            existing => existing.OrganizationId == project.OrganizationId,
            cancellationToken);
        if (maximumProjects > 0 && currentCount >= maximumProjects)
        {
            throw new InvalidOperationException($"The subscription plan allows up to {maximumProjects} projects.");
        }

        dbContext.Projects.Add(project);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new InvalidOperationException("A project with this slug already exists in the organization.", exception);
        }
        await transaction.CommitAsync(cancellationToken);
        return project;
    }

    public async Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        project.UpdatedAt = DateTime.UtcNow;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new InvalidOperationException("A project with this slug already exists in the organization.", exception);
        }

        return project;
    }

    public async Task<bool> DeleteAsync(Project project, CancellationToken cancellationToken = default)
    {
        dbContext.Projects.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
