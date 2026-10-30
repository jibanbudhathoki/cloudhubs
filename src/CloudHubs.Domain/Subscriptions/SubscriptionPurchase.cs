namespace CloudHubs.Domain.Subscriptions;

public enum SubscriptionPurchaseStatus
{
    PendingPayment = 0,
    PaymentApproved = 1,
    Completed = 2,
    Rejected = 3
}

public class SubscriptionPurchase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;
    public string PlanName { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public decimal Price { get; set; }
    public SubscriptionBillingInterval BillingInterval { get; set; } = SubscriptionBillingInterval.Monthly;
    public SubscriptionPurchaseStatus Status { get; set; } = SubscriptionPurchaseStatus.PendingPayment;
    public string? CompletionTokenHash { get; set; }
    public DateTime? CompletionTokenExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
