using CloudHubs.Domain.Entities;

namespace CloudHubs.Web.ViewModels.Subscriptions;

public class SubscriptionPlanIndexViewModel
{
    public IReadOnlyCollection<SubscriptionPlan> Plans { get; init; } = [];
    public IReadOnlyDictionary<Guid, int> OrganizationCounts { get; init; } = new Dictionary<Guid, int>();
    public bool IsDataAvailable { get; init; } = true;
    public string? ErrorMessage { get; init; }
}
