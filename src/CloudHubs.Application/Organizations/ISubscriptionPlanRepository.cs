using CloudHubs.Domain.Subscriptions;

namespace CloudHubs.Application.Organizations;

public interface ISubscriptionPlanRepository
{
    Task<IReadOnlyCollection<SubscriptionPlan>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, Guid? exceptId = null, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan> AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan> UpdateAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> GetOrganizationCountsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsAssignedAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
