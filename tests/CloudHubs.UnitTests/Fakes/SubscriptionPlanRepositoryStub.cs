using CloudHubs.Application.Modules.Subscriptions.Interfaces;
using CloudHubs.Domain.Entities;

namespace CloudHubs.UnitTests.Fakes;

internal sealed class SubscriptionPlanRepositoryStub : ISubscriptionPlanRepository
{
    public SubscriptionPlan? Plan { get; init; }
    public bool IsAssigned { get; init; }

    public Task<IReadOnlyCollection<SubscriptionPlan>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<SubscriptionPlan>>(Plan is null ? [] : [Plan]);
    public Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Plan?.Id == id ? Plan : null);
    public Task<bool> NameExistsAsync(string name, Guid? exceptId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Plan is not null && Plan.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && Plan.Id != exceptId);
    public Task<SubscriptionPlan> AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default) => Task.FromResult(plan);
    public Task<SubscriptionPlan> UpdateAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default) => Task.FromResult(plan);
    public Task<IReadOnlyDictionary<Guid, int>> GetOrganizationCountsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());
    public Task<bool> IsAssignedAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(IsAssigned);
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
