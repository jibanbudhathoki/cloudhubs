namespace MyDevPlatform.Domain.Organizations;

public class PlatformSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string PlatformName { get; set; } = "CloudHubs";
    public string SupportEmail { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public int SessionTimeoutMinutes { get; set; } = 480;
    public int JwtLifetimeMinutes { get; set; } = 15;
    public int MinimumPasswordLength { get; set; } = 12;
    public bool RequirePasswordDigit { get; set; } = true;
    public bool AllowOrganizationCreation { get; set; } = true;
    public OrganizationStatus DefaultOrganizationStatus { get; set; } = OrganizationStatus.Active;
    public int DefaultTrialDays { get; set; } = 30;
    public string DefaultTemplateId { get; set; } = "default";
    public string DefaultPlanName { get; set; } = "Standard";
    public string BillingCurrency { get; set; } = "USD";
    public decimal MonthlyPrice { get; set; }
    public bool NotifyOnOrganizationCreated { get; set; }
    public string NotificationEmail { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
