namespace CloudHubs.Domain.Subscriptions;

public class SubscriptionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public decimal Price { get; set; }
    public SubscriptionBillingInterval BillingInterval { get; set; } = SubscriptionBillingInterval.Monthly;
    public int TrialDays { get; set; }
    public int MaxOrganizations { get; set; }
    public int MaxProjects { get; set; }
    public int MaxMembers { get; set; }
    public int StorageLimitGb { get; set; }
    public bool HasApiAccess { get; set; }
    public bool HasCustomDomains { get; set; }
    public bool HasAdvancedAnalytics { get; set; }
    public bool HasPrioritySupport { get; set; }
    public bool HasSingleSignOn { get; set; }
    public bool HasAuditLogs { get; set; }
    public bool HasAutomatedBackups { get; set; }
    public bool HasIntegrations { get; set; }
    public bool HasCustomRoles { get; set; }
    public int SupportResponseHours { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
