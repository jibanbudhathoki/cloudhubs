using Microsoft.EntityFrameworkCore;
using CloudHubs.Application.Organizations;
using CloudHubs.Contracts.Organizations;
using CloudHubs.Domain.Organizations;

namespace CloudHubs.Infrastructure.Persistence;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly CloudHubsDbContext _dbContext;

    public OrganizationRepository(CloudHubsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<OrganizationSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Organizations
            .AsNoTracking()
            .Include(x => x.Subscription)
            .Select(x => new OrganizationSummaryDto(
                x.Id,
                x.Name,
                x.Slug,
                x.Email,
                x.Status.ToString(),
                x.AccountSubscription != null
                    ? x.AccountSubscription.Status.ToString()
                    : x.Subscription != null ? x.Subscription.Status.ToString() : null,
                x.CreatedAt,
                x.Members.Where(member => member.Role == OrganizationMemberRole.Owner)
                    .Select(member => member.User.Name)
                    .FirstOrDefault(),
                x.Members.Where(member => member.Role == OrganizationMemberRole.Owner)
                    .Select(member => member.User.Email)
                    .FirstOrDefault(),
                x.Members.Count(),
                x.AccountSubscription != null
                    ? x.AccountSubscription.Plan.Name
                    : x.Subscription != null ? x.Subscription.PlanName : null))
            .ToListAsync(cancellationToken);
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Organizations
            .Include(x => x.Subscription)
                .ThenInclude(subscription => subscription!.Plan)
            .Include(x => x.AccountSubscription)
                .ThenInclude(subscription => subscription!.Plan)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> SlugExistsAsync(string slug, Guid? exceptId = null, CancellationToken cancellationToken = default)
    {
        return _dbContext.Organizations.AnyAsync(
            organization => organization.Slug == slug && (!exceptId.HasValue || organization.Id != exceptId.Value),
            cancellationToken);
    }

    public Task<bool> HasProjectsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Projects.AnyAsync(project => project.OrganizationId == id, cancellationToken);
    }

    public async Task<Organization> AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        if (organization.AccountSubscriptionId is Guid accountSubscriptionId)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var lockedSubscriptions = await _dbContext.AccountSubscriptions
                .FromSqlInterpolated($"SELECT * FROM \"AccountSubscriptions\" WHERE \"Id\" = {accountSubscriptionId} FOR UPDATE")
                .ToListAsync(cancellationToken);
            if (lockedSubscriptions.Count == 0)
            {
                throw new InvalidOperationException("The account subscription for this organization is unavailable.");
            }

            var maxOrganizations = await _dbContext.SubscriptionPlans
                .Where(plan => plan.Id == lockedSubscriptions[0].PlanId)
                .Select(plan => plan.MaxOrganizations)
                .SingleAsync(cancellationToken);
            var existingOrganizationCount = await _dbContext.Organizations.CountAsync(
                existing => existing.AccountSubscriptionId == accountSubscriptionId,
                cancellationToken);
            if (maxOrganizations > 0 && existingOrganizationCount >= maxOrganizations)
            {
                throw new InvalidOperationException(
                    $"The {lockedSubscriptions[0].PlanName} plan allows up to {maxOrganizations} organizations. Upgrade your plan to create more.");
            }

            _dbContext.Organizations.Add(organization);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return organization;
        }

        _dbContext.Organizations.Add(organization);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return organization;
    }

    public async Task<Organization> UpdateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        organization.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return organization;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var organization = await _dbContext.Organizations.FindAsync([id], cancellationToken);
        if (organization is null)
        {
            return false;
        }

        _dbContext.Organizations.Remove(organization);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
