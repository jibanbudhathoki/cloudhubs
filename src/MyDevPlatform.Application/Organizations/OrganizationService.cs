using MyDevPlatform.Contracts.Organizations;
using MyDevPlatform.Domain.Organizations;
using MyDevPlatform.Domain.Subscriptions;

namespace MyDevPlatform.Application.Organizations;

public class OrganizationService
{
    private readonly IOrganizationRepository _repository;
    private readonly IPlatformSettingsRepository _settingsRepository;

    public OrganizationService(IOrganizationRepository repository, IPlatformSettingsRepository settingsRepository)
    {
        _repository = repository;
        _settingsRepository = settingsRepository;
    }

    public async Task<IReadOnlyCollection<OrganizationSummaryDto>> GetOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }

    public async Task<OrganizationDetailsDto> CreateOrganizationAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.GetAsync(cancellationToken);
        if (!settings.AllowOrganizationCreation)
        {
            throw new InvalidOperationException("Organization creation is disabled in platform settings.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Organization name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            throw new ArgumentException("A valid organization email is required.", nameof(request));
        }

        var slug = CreateSlug(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);
        if (await _repository.SlugExistsAsync(slug, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException("That organization slug is already in use.");
        }

        var organization = new Organization
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Email = request.Email.Trim(),
            Status = settings.DefaultOrganizationStatus,
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.Trial,
                PlanName = settings.DefaultPlanName,
                Currency = settings.BillingCurrency,
                MonthlyPrice = settings.MonthlyPrice,
                StartsAt = DateTime.UtcNow,
                ExpiresAt = settings.DefaultTrialDays > 0
                    ? DateTime.UtcNow.AddDays(settings.DefaultTrialDays)
                    : DateTime.UtcNow
            }
        };

        var created = await _repository.AddAsync(organization, cancellationToken);

        return new OrganizationDetailsDto(
            created.Id,
            created.Name,
            created.Slug,
            created.Email,
            created.Status.ToString(),
            created.Subscription?.Status.ToString() ?? SubscriptionStatus.Trial.ToString(),
            created.CreatedAt,
            created.BaseApiUrl);
    }

    public async Task<OrganizationDetailsDto?> GetOrganizationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var organization = await _repository.GetByIdAsync(id, cancellationToken);
        return organization is null ? null : ToDetails(organization);
    }

    public async Task<OrganizationDetailsDto?> UpdateOrganizationAsync(Guid id, string name, string email, string slug, string status, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Organization name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new ArgumentException("A valid organization email is required.", nameof(email));
        if (!Enum.TryParse<OrganizationStatus>(status, true, out var parsedStatus)) throw new ArgumentException("Choose a valid organization status.", nameof(status));

        var organization = await _repository.GetByIdAsync(id, cancellationToken);
        if (organization is null) return null;

        var normalizedSlug = CreateSlug(slug);
        if (await _repository.SlugExistsAsync(normalizedSlug, id, cancellationToken))
        {
            throw new InvalidOperationException("That organization slug is already in use.");
        }

        organization.Name = name.Trim();
        organization.Email = email.Trim();
        organization.Slug = normalizedSlug;
        organization.Status = parsedStatus;
        return ToDetails(await _repository.UpdateAsync(organization, cancellationToken));
    }

    public async Task<bool> DeleteOrganizationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await _repository.HasProjectsAsync(id, cancellationToken))
        {
            throw new InvalidOperationException("Move or delete this organization's projects before deleting the organization.");
        }

        return await _repository.DeleteAsync(id, cancellationToken);
    }

    private static OrganizationDetailsDto ToDetails(Organization organization)
    {
        return new OrganizationDetailsDto(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.Email,
            organization.Status.ToString(),
            organization.Subscription?.Status.ToString() ?? SubscriptionStatus.Trial.ToString(),
            organization.CreatedAt,
            organization.BaseApiUrl);
    }

    private static string CreateSlug(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var slug = string.Concat(normalized.Select(character => char.IsLetterOrDigit(character) ? character : '-'));
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "organization" : slug;
    }
}
