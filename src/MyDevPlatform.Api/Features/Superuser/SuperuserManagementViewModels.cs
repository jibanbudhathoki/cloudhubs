using System.ComponentModel.DataAnnotations;
using MyDevPlatform.Domain.Organizations;
using MyDevPlatform.Domain.Projects;
using MyDevPlatform.Domain.Users;

namespace MyDevPlatform.Api.Features.Superuser;

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

    [Required]
    public string Status { get; set; } = "Active";
}

public class UserFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

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

public class SuperuserLoginViewModel
{
    [Required, StringLength(200)]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? ReturnUrl { get; set; }
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
}