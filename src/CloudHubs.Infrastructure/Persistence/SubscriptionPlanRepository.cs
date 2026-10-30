using Microsoft.EntityFrameworkCore;
using CloudHubs.Application.Organizations;
using CloudHubs.Domain.Subscriptions;

namespace CloudHubs.Infrastructure.Persistence;

public class SubscriptionPlanRepository(CloudHubsDbContext dbContext) : ISubscriptionPlanRepository
{
    public async Task<IReadOnlyCollection<SubscriptionPlan>> GetAllAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SubscriptionPlans.AsNoTracking();
        if (activeOnly) query = query.Where(plan => plan.IsActive);
        return await query.OrderBy(plan => plan.Name).ToListAsync(cancellationToken);
    }

    public Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.SubscriptionPlans.FirstOrDefaultAsync(plan => plan.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? exceptId = null, CancellationToken cancellationToken = default) =>
        dbContext.SubscriptionPlans.AnyAsync(
            plan => plan.Name.ToLower() == name.ToLower()
                && (!exceptId.HasValue || plan.Id != exceptId.Value),
            cancellationToken);

    public async Task<SubscriptionPlan> AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        dbContext.SubscriptionPlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken);
        return plan;
    }

    public async Task<SubscriptionPlan> UpdateAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        return plan;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetOrganizationCountsAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.Subscriptions
            .Where(subscription => subscription.PlanId.HasValue)
            .GroupBy(subscription => subscription.PlanId!.Value)
            .Select(group => new { PlanId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.PlanId, item => item.Count, cancellationToken);

    public async Task<bool> IsAssignedAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Subscriptions.AnyAsync(subscription => subscription.PlanId == id, cancellationToken)
        || await dbContext.AccountSubscriptions.AnyAsync(subscription => subscription.PlanId == id, cancellationToken)
        || await dbContext.SubscriptionPurchases.AnyAsync(
            purchase => purchase.PlanId == id
                && purchase.Status != SubscriptionPurchaseStatus.Rejected,
            cancellationToken);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.SubscriptionPlans.FindAsync([id], cancellationToken);
        if (plan is null) return false;
        dbContext.SubscriptionPlans.Remove(plan);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
