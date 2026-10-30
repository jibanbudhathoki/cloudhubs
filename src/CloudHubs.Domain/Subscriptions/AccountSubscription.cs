using CloudHubs.Domain.Users;

namespace CloudHubs.Domain.Subscriptions;

public class AccountSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = null!;
    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;
    public string PlanName { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public decimal Price { get; set; }
    public SubscriptionBillingInterval BillingInterval { get; set; } = SubscriptionBillingInterval.Monthly;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTime StartsAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public bool IsAvailable() =>
        Status is SubscriptionStatus.Active or SubscriptionStatus.Trial
        && (ExpiresAt is null || ExpiresAt.Value > DateTime.UtcNow);
}
