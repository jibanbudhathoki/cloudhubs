using System.ComponentModel.DataAnnotations;
using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Projects;
using CloudHubs.Domain.Subscriptions;
using CloudHubs.Domain.Users;
using CloudHubs.Contracts.Organizations;

namespace CloudHubs.Api.Features.Superuser;

public class OrganizationFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(120)]
    [RegularExpression("^[a-zA-Z0-9_-]+$", ErrorMessage = "Use letters, numbers, hyphens, or underscores.")]
    public string Slug { get; set; } = string.Empty;

    public Guid? SubscriptionPlanId { get; set; }

    [Required]
    public string Status { get; set; } = "Active";

    public string AdminMode { get; set; } = "existing";

    public Guid? AdminUserId { get; set; }

    [StringLength(200)]
    public string? AdminName { get; set; }

    [EmailAddress, StringLength(200)]
    public string? AdminEmail { get; set; }

    [DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
    public string? AdminPassword { get; set; }

    public List<OrganizationMemberFormViewModel> Members { get; set; } = [];
}

public class OrganizationSubscriptionPlanViewModel
{
    public Guid OrganizationId { get; set; }

    public string OrganizationName { get; set; } = string.Empty;

    public string? CurrentPlanName { get; set; }

    public string? SubscriptionStatus { get; set; }

    [Required(ErrorMessage = "Select a subscription plan.")]
    public Guid? PlanId { get; set; }
}

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

public class OrganizationMemberFormViewModel
{
    public string Mode { get; set; } = "existing";

    public Guid? UserId { get; set; }

    [StringLength(200)]
    public string? Name { get; set; }

    [EmailAddress, StringLength(200)]
    public string? Email { get; set; }

    [DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
    public string? Password { get; set; }

    public OrganizationMemberRole Role { get; set; } = OrganizationMemberRole.Developer;
}

public class UserFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = nameof(UserRole.Viewer);

    public bool IsActive { get; set; } = true;
}

public class PlatformProjectFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(120)]
    [RegularExpression("^[a-zA-Z0-9_-]+$", ErrorMessage = "Use letters, numbers, hyphens, or underscores.")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string RootPath { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string TemplateId { get; set; } = "default";

    public bool IsActive { get; set; } = true;
}

public class UserIndexViewModel
{
    public IReadOnlyCollection<User> Users { get; init; } = [];
    public string? Query { get; init; }
    public bool IsDataAvailable { get; init; } = true;
    public string? ErrorMessage { get; init; }
}

public class PlatformProjectIndexViewModel
{
    public IReadOnlyCollection<PlatformProject> Projects { get; init; } = [];
    public string? Query { get; init; }
    public bool IsDataAvailable { get; init; } = true;
    public string? ErrorMessage { get; init; }
}

public class SubscriptionPlanIndexViewModel
{
    public IReadOnlyCollection<SubscriptionPlan> Plans { get; init; } = [];
    public IReadOnlyDictionary<Guid, int> OrganizationCounts { get; init; } = new Dictionary<Guid, int>();
    public bool IsDataAvailable { get; init; } = true;
    public string? ErrorMessage { get; init; }
}

public class OrganizationRolesIndexViewModel
{
    public Guid OrganizationId { get; init; }
    public string OrganizationName { get; init; } = string.Empty;
    public IReadOnlyCollection<OrganizationRole> Roles { get; init; } = [];
    public IReadOnlyCollection<OrganizationMembership> Members { get; init; } = [];
}

public class OrganizationRoleFormViewModel
{
    public Guid OrganizationId { get; set; }
    public Guid? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public bool ViewProjects { get; set; }
    public bool ManageProjects { get; set; }
    public bool ViewMembers { get; set; }
    public bool ManageMembers { get; set; }
    public bool ManageSettings { get; set; }
    public bool ManageIntegrations { get; set; }
    public bool ViewAuditLog { get; set; }
    public bool ManageBilling { get; set; }

    public OrganizationPermission ToPermissions()
    {
        var permissions = OrganizationPermission.None;
        if (ViewProjects) permissions |= OrganizationPermission.ViewProjects;
        if (ManageProjects) permissions |= OrganizationPermission.ManageProjects;
        if (ViewMembers) permissions |= OrganizationPermission.ViewMembers;
        if (ManageMembers) permissions |= OrganizationPermission.ManageMembers;
        if (ManageSettings) permissions |= OrganizationPermission.ManageSettings;
        if (ManageIntegrations) permissions |= OrganizationPermission.ManageIntegrations;
        if (ViewAuditLog) permissions |= OrganizationPermission.ViewAuditLog;
        if (ManageBilling) permissions |= OrganizationPermission.ManageBilling;
        return permissions;
    }

    public static OrganizationRoleFormViewModel From(OrganizationRole role) => new()
    {
        OrganizationId = role.OrganizationId,
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        ViewProjects = role.Permissions.HasFlag(OrganizationPermission.ViewProjects),
        ManageProjects = role.Permissions.HasFlag(OrganizationPermission.ManageProjects),
        ViewMembers = role.Permissions.HasFlag(OrganizationPermission.ViewMembers),
        ManageMembers = role.Permissions.HasFlag(OrganizationPermission.ManageMembers),
        ManageSettings = role.Permissions.HasFlag(OrganizationPermission.ManageSettings),
        ManageIntegrations = role.Permissions.HasFlag(OrganizationPermission.ManageIntegrations),
        ViewAuditLog = role.Permissions.HasFlag(OrganizationPermission.ViewAuditLog),
        ManageBilling = role.Permissions.HasFlag(OrganizationPermission.ManageBilling)
    };
}

public class AuditLogIndexViewModel
{
    public IReadOnlyCollection<AuditLogEntry> Entries { get; init; } = [];
    public IReadOnlyCollection<OrganizationSummaryDto> Organizations { get; init; } = [];
    public string? Query { get; init; }
    public Guid? OrganizationId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int TotalCount { get; init; }
    public string? ErrorMessage { get; init; }
}

public class SuperuserLoginViewModel
{
    [Required, StringLength(200)]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? ReturnUrl { get; set; }
}

public class SuperuserAccountChangeViewModel
{
    [Required]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [EmailAddress, StringLength(200)]
    public string? NewEmail { get; set; }

    [DataType(DataType.Password)]
    [StringLength(128, MinimumLength = 8)]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation do not match.")]
    public string? ConfirmNewPassword { get; set; }
}

public class PlatformSettingsViewModel
{
    [Required, StringLength(100)]
    public string PlatformName { get; set; } = "CloudHubs";

    [EmailAddress, StringLength(200)]
    public string SupportEmail { get; set; } = string.Empty;

    [Url, StringLength(500)]
    public string PublicUrl { get; set; } = string.Empty;

    [Range(15, 720)]
    public int SessionTimeoutMinutes { get; set; } = 480;

    [Range(5, 60)]
    public int JwtLifetimeMinutes { get; set; } = 15;

    [Range(8, 128)]
    public int MinimumPasswordLength { get; set; } = 12;

    public bool RequirePasswordDigit { get; set; } = true;

    public bool AllowOrganizationCreation { get; set; } = true;

    [Required]
    public OrganizationStatus DefaultOrganizationStatus { get; set; } = OrganizationStatus.Active;

    [Range(0, 365)]
    public int DefaultTrialDays { get; set; } = 30;

    [Required, StringLength(150, MinimumLength = 1)]
    public string DefaultTemplateId { get; set; } = "default";

    [Required, StringLength(100)]
    public string DefaultPlanName { get; set; } = "Standard";

    [Required, StringLength(3, MinimumLength = 3)]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Enter a three-letter currency code.")]
    public string BillingCurrency { get; set; } = "USD";

    [Range(typeof(decimal), "0", "9999999999")]
    public decimal MonthlyPrice { get; set; }

    public bool NotifyOnOrganizationCreated { get; set; }

    [EmailAddress, StringLength(200)]
    public string NotificationEmail { get; set; } = string.Empty;

    public static PlatformSettingsViewModel From(PlatformSettings settings) => new()
    {
        PlatformName = settings.PlatformName,
        SupportEmail = settings.SupportEmail,
        PublicUrl = settings.PublicUrl,
        SessionTimeoutMinutes = settings.SessionTimeoutMinutes,
        JwtLifetimeMinutes = settings.JwtLifetimeMinutes,
        MinimumPasswordLength = settings.MinimumPasswordLength,
        RequirePasswordDigit = settings.RequirePasswordDigit,
        AllowOrganizationCreation = settings.AllowOrganizationCreation,
        DefaultOrganizationStatus = settings.DefaultOrganizationStatus,
        DefaultTrialDays = settings.DefaultTrialDays,
        DefaultTemplateId = settings.DefaultTemplateId,
        DefaultPlanName = settings.DefaultPlanName,
        BillingCurrency = settings.BillingCurrency,
        MonthlyPrice = settings.MonthlyPrice,
        NotifyOnOrganizationCreated = settings.NotifyOnOrganizationCreated,
        NotificationEmail = settings.NotificationEmail
    };

    public PlatformSettings ToSettings() => new()
    {
        Id = PlatformSettings.SingletonId,
        PlatformName = PlatformName,
        SupportEmail = SupportEmail,
        PublicUrl = PublicUrl,
        SessionTimeoutMinutes = SessionTimeoutMinutes,
        JwtLifetimeMinutes = JwtLifetimeMinutes,
        MinimumPasswordLength = MinimumPasswordLength,
        RequirePasswordDigit = RequirePasswordDigit,
        AllowOrganizationCreation = AllowOrganizationCreation,
        DefaultOrganizationStatus = DefaultOrganizationStatus,
        DefaultTrialDays = DefaultTrialDays,
        DefaultTemplateId = DefaultTemplateId,
        DefaultPlanName = DefaultPlanName,
        BillingCurrency = BillingCurrency,
        MonthlyPrice = MonthlyPrice,
        NotifyOnOrganizationCreated = NotifyOnOrganizationCreated,
        NotificationEmail = NotificationEmail
    };

    public void ApplySection(PlatformSettings settings, string section)
    {
        switch (section)
        {
            case "profile":
                settings.PlatformName = PlatformName;
                settings.SupportEmail = SupportEmail;
                settings.PublicUrl = PublicUrl;
                break;
            case "security":
                settings.SessionTimeoutMinutes = SessionTimeoutMinutes;
                settings.JwtLifetimeMinutes = JwtLifetimeMinutes;
                settings.MinimumPasswordLength = MinimumPasswordLength;
                settings.RequirePasswordDigit = RequirePasswordDigit;
                break;
            case "organizations":
                settings.AllowOrganizationCreation = AllowOrganizationCreation;
                settings.DefaultOrganizationStatus = DefaultOrganizationStatus;
                settings.DefaultTrialDays = DefaultTrialDays;
                break;
            case "catalog":
                settings.DefaultTemplateId = DefaultTemplateId;
                break;
            case "billing":
                settings.DefaultPlanName = DefaultPlanName;
                settings.BillingCurrency = BillingCurrency;
                settings.MonthlyPrice = MonthlyPrice;
                break;
            case "notifications":
                settings.NotifyOnOrganizationCreated = NotifyOnOrganizationCreated;
                settings.NotificationEmail = NotificationEmail;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown settings section.");
        }
    }
}