using System.ComponentModel.DataAnnotations;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Web.ViewModels.Subscriptions;

public class SubscriptionPlanFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(3, MinimumLength = 3)]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Enter a three-letter currency code.")]
    public string Currency { get; set; } = "USD";

    [Range(typeof(decimal), "0", "9999999999")]
    public decimal Price { get; set; }

    [Required]
    public SubscriptionBillingInterval BillingInterval { get; set; } = SubscriptionBillingInterval.Monthly;

    [Range(0, 365)]
    public int TrialDays { get; set; }

    [Range(0, 1000000)]
    public int MaxOrganizations { get; set; }

    [Range(0, 1000000)]
    public int MaxProjects { get; set; }

    [Range(0, 1000000)]
    public int MaxMembers { get; set; }

    [Range(0, 100000000)]
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
    [Range(0, 720)]
    public int SupportResponseHours { get; set; }
    public bool IsActive { get; set; } = true;

    public SubscriptionPlan ToPlan() => new()
    {
        Name = Name,
        Description = Description,
        Currency = Currency,
        Price = Price,
        BillingInterval = BillingInterval,
        TrialDays = TrialDays,
        MaxOrganizations = MaxOrganizations,
        MaxProjects = MaxProjects,
        MaxMembers = MaxMembers,
        StorageLimitGb = StorageLimitGb,
        HasApiAccess = HasApiAccess,
        HasCustomDomains = HasCustomDomains,
        HasAdvancedAnalytics = HasAdvancedAnalytics,
        HasPrioritySupport = HasPrioritySupport,
        HasSingleSignOn = HasSingleSignOn,
        HasAuditLogs = HasAuditLogs,
        HasAutomatedBackups = HasAutomatedBackups,
        HasIntegrations = HasIntegrations,
        HasCustomRoles = HasCustomRoles,
        SupportResponseHours = SupportResponseHours,
        IsActive = IsActive
    };

    public static SubscriptionPlanFormViewModel From(SubscriptionPlan plan) => new()
    {
        Id = plan.Id,
        Name = plan.Name,
        Description = plan.Description,
        Currency = plan.Currency,
        Price = plan.Price,
        BillingInterval = plan.BillingInterval,
        TrialDays = plan.TrialDays,
        MaxOrganizations = plan.MaxOrganizations,
        MaxProjects = plan.MaxProjects,
        MaxMembers = plan.MaxMembers,
        StorageLimitGb = plan.StorageLimitGb,
        HasApiAccess = plan.HasApiAccess,
        HasCustomDomains = plan.HasCustomDomains,
        HasAdvancedAnalytics = plan.HasAdvancedAnalytics,
        HasPrioritySupport = plan.HasPrioritySupport,
        HasSingleSignOn = plan.HasSingleSignOn,
        HasAuditLogs = plan.HasAuditLogs,
        HasAutomatedBackups = plan.HasAutomatedBackups,
        HasIntegrations = plan.HasIntegrations,
        HasCustomRoles = plan.HasCustomRoles,
        SupportResponseHours = plan.SupportResponseHours,
        IsActive = plan.IsActive
    };
}
