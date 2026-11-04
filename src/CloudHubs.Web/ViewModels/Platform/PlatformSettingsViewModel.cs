using System.ComponentModel.DataAnnotations;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Web.ViewModels.Platform;

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
