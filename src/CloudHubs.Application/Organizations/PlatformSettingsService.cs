using System.ComponentModel.DataAnnotations;
using CloudHubs.Domain.Organizations;

namespace CloudHubs.Application.Organizations;

public class PlatformSettingsService(IPlatformSettingsRepository repository)
{
    public Task<PlatformSettings> GetAsync(CancellationToken cancellationToken = default) =>
        repository.GetAsync(cancellationToken);

    public async Task<PlatformSettings> UpdateAsync(PlatformSettings settings, CancellationToken cancellationToken = default)
    {
        foreach (var section in SettingSections)
        {
            ValidateSection(settings, section);
            NormalizeSection(settings, section);
        }

        settings.UpdatedAt = DateTime.UtcNow;
        return await repository.UpdateAsync(settings, cancellationToken);
    }

    public async Task<PlatformSettings> UpdateSectionAsync(
        PlatformSettings settings,
        string section,
        CancellationToken cancellationToken = default)
    {
        ValidateSection(settings, section);
        NormalizeSection(settings, section);
        settings.UpdatedAt = DateTime.UtcNow;
        return await repository.UpdateAsync(settings, cancellationToken);
    }

    private static readonly string[] SettingSections =
        ["profile", "security", "organizations", "catalog", "billing", "notifications"];

    private static void ValidateSection(PlatformSettings settings, string section)
    {
        switch (section)
        {
            case "profile":
                ValidatePlatformName(settings.PlatformName);
                ValidateOptionalEmail(settings.SupportEmail, "Support email");
                ValidatePublicUrl(settings.PublicUrl);
                break;
            case "security":
                if (settings.SessionTimeoutMinutes is < 15 or > 720)
                    throw new ArgumentException("Session timeout must be between 15 and 720 minutes.", nameof(settings));
                if (settings.JwtLifetimeMinutes is < 5 or > 60)
                    throw new ArgumentException("JWT lifetime must be between 5 and 60 minutes.", nameof(settings));
                if (settings.MinimumPasswordLength is < 8 or > 128)
                    throw new ArgumentException("Minimum password length must be between 8 and 128 characters.", nameof(settings));
                break;
            case "organizations":
                if (settings.DefaultTrialDays is < 0 or > 365)
                    throw new ArgumentException("Trial duration must be between 0 and 365 days.", nameof(settings));
                if (!Enum.IsDefined(settings.DefaultOrganizationStatus))
                    throw new ArgumentException("Choose a valid default organization status.", nameof(settings));
                break;
            case "catalog":
                if (string.IsNullOrWhiteSpace(settings.DefaultTemplateId) || settings.DefaultTemplateId.Length > 150)
                    throw new ArgumentException("Default template ID must be between 1 and 150 characters.", nameof(settings));
                break;
            case "billing":
                if (string.IsNullOrWhiteSpace(settings.DefaultPlanName) || settings.DefaultPlanName.Length > 100)
                    throw new ArgumentException("Plan name is required and must be 100 characters or fewer.", nameof(settings));
                if (settings.BillingCurrency is null
                    || settings.BillingCurrency.Length != 3
                    || !settings.BillingCurrency.All(char.IsAsciiLetter))
                    throw new ArgumentException("Billing currency must be a three-letter ISO currency code.", nameof(settings));
                if (settings.MonthlyPrice is < 0 or > 9999999999m)
                    throw new ArgumentException("Monthly price must be between 0 and 9,999,999,999.", nameof(settings));
                break;
            case "notifications":
                ValidateOptionalEmail(settings.NotificationEmail, "Notification email");
                if (settings.NotifyOnOrganizationCreated && string.IsNullOrWhiteSpace(settings.NotificationEmail))
                    throw new ArgumentException("Set a notification recipient when organization-created alerts are enabled.", nameof(settings));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown settings section.");
        }
    }

    private static void NormalizeSection(PlatformSettings settings, string section)
    {
        switch (section)
        {
            case "profile":
                settings.PlatformName = settings.PlatformName.Trim();
                settings.SupportEmail = settings.SupportEmail?.Trim() ?? string.Empty;
                settings.PublicUrl = settings.PublicUrl?.Trim() ?? string.Empty;
                break;
            case "catalog":
                settings.DefaultTemplateId = settings.DefaultTemplateId.Trim();
                break;
            case "billing":
                settings.DefaultPlanName = settings.DefaultPlanName.Trim();
                settings.BillingCurrency = settings.BillingCurrency.ToUpperInvariant();
                break;
            case "notifications":
                settings.NotificationEmail = settings.NotificationEmail?.Trim() ?? string.Empty;
                break;
        }
    }

    private static void ValidatePlatformName(string? platformName)
    {
        if (string.IsNullOrWhiteSpace(platformName))
            throw new ArgumentException("Platform name is required.", nameof(platformName));
        if (platformName.Length > 100)
            throw new ArgumentException("Platform name must be 100 characters or fewer.", nameof(platformName));
    }

    private static void ValidateOptionalEmail(string? email, string fieldName)
    {
        if (email?.Length > 200)
            throw new ArgumentException($"{fieldName} must be 200 characters or fewer.", nameof(email));
        if (!string.IsNullOrWhiteSpace(email) && !new EmailAddressAttribute().IsValid(email))
            throw new ArgumentException($"{fieldName} must be a valid email address.", nameof(email));
    }

    private static void ValidatePublicUrl(string? publicUrl)
    {
        if (publicUrl is { Length: > 500 })
            throw new ArgumentException("Public URL must be 500 characters or fewer.", nameof(publicUrl));
        if (!string.IsNullOrWhiteSpace(publicUrl)
            && (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var publicUri)
                || publicUri.Scheme is not ("http" or "https")))
            throw new ArgumentException("Public URL must be an absolute HTTP or HTTPS URL.", nameof(publicUrl));
    }
}
