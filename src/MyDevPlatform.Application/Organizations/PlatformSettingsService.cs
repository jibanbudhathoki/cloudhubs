using MyDevPlatform.Domain.Organizations;

namespace MyDevPlatform.Application.Organizations;

public class PlatformSettingsService(IPlatformSettingsRepository repository)
{
    public Task<PlatformSettings> GetAsync(CancellationToken cancellationToken = default) =>
        repository.GetAsync(cancellationToken);

    public async Task<PlatformSettings> UpdateAsync(PlatformSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.PlatformName))
            throw new ArgumentException("Platform name is required.", nameof(settings));
        if (settings.PlatformName.Length > 100)
            throw new ArgumentException("Platform name must be 100 characters or fewer.", nameof(settings));
        if (settings.SupportEmail.Length > 200)
            throw new ArgumentException("Support email must be 200 characters or fewer.", nameof(settings));
        if (settings.PublicUrl.Length > 500)
            throw new ArgumentException("Public URL must be 500 characters or fewer.", nameof(settings));
        if (settings.SessionTimeoutMinutes is < 15 or > 720)
            throw new ArgumentException("Session timeout must be between 15 and 720 minutes.", nameof(settings));
        if (settings.JwtLifetimeMinutes is < 5 or > 60)
            throw new ArgumentException("JWT lifetime must be between 5 and 60 minutes.", nameof(settings));
        if (settings.MinimumPasswordLength is < 8 or > 128)
            throw new ArgumentException("Minimum password length must be between 8 and 128 characters.", nameof(settings));
        if (settings.DefaultTrialDays is < 0 or > 365)
            throw new ArgumentException("Trial duration must be between 0 and 365 days.", nameof(settings));
        if (settings.DefaultTemplateId.Length is < 1 or > 150)
            throw new ArgumentException("Default template ID must be between 1 and 150 characters.", nameof(settings));
        if (string.IsNullOrWhiteSpace(settings.DefaultPlanName) || settings.DefaultPlanName.Length > 100)
            throw new ArgumentException("Plan name is required and must be 100 characters or fewer.", nameof(settings));
        if (settings.BillingCurrency.Length != 3 || !settings.BillingCurrency.All(char.IsAsciiLetter))
            throw new ArgumentException("Billing currency must be a three-letter ISO currency code.", nameof(settings));
        if (settings.MonthlyPrice < 0)
            throw new ArgumentException("Monthly price cannot be negative.", nameof(settings));
        if (settings.NotificationEmail.Length > 200)
            throw new ArgumentException("Notification email must be 200 characters or fewer.", nameof(settings));
        if (settings.NotifyOnOrganizationCreated && string.IsNullOrWhiteSpace(settings.NotificationEmail))
            throw new ArgumentException("Set a notification recipient when organization-created alerts are enabled.", nameof(settings));
        if (!Enum.IsDefined(settings.DefaultOrganizationStatus))
            throw new ArgumentException("Choose a valid default organization status.", nameof(settings));
        if (!string.IsNullOrWhiteSpace(settings.PublicUrl)
            && (!Uri.TryCreate(settings.PublicUrl, UriKind.Absolute, out var publicUri)
                || publicUri.Scheme is not ("http" or "https")))
            throw new ArgumentException("Public URL must be an absolute HTTP or HTTPS URL.", nameof(settings));

        settings.PlatformName = settings.PlatformName.Trim();
        settings.SupportEmail = settings.SupportEmail.Trim();
        settings.PublicUrl = settings.PublicUrl.Trim();
        settings.DefaultTemplateId = settings.DefaultTemplateId.Trim();
        settings.DefaultPlanName = settings.DefaultPlanName.Trim();
        settings.BillingCurrency = settings.BillingCurrency.ToUpperInvariant();
        settings.NotificationEmail = settings.NotificationEmail.Trim();
        settings.UpdatedAt = DateTime.UtcNow;
        return await repository.UpdateAsync(settings, cancellationToken);
    }
}
