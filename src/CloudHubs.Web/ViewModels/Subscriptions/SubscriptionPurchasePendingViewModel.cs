namespace CloudHubs.Web.ViewModels.Subscriptions;

public sealed class SubscriptionPurchasePendingViewModel
{
    public Guid PurchaseId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PlanName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string BillingInterval { get; init; } = string.Empty;
    public string SupportEmail { get; init; } = string.Empty;
}
