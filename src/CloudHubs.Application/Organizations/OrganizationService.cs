using CloudHubs.Contracts.Organizations;
using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Subscriptions;
using CloudHubs.Domain.Users;

namespace CloudHubs.Application.Organizations;

public class OrganizationService
{
    private readonly IOrganizationRepository _repository;
    private readonly IPlatformSettingsRepository _settingsRepository;
    private readonly ISubscriptionPlanRepository? _planRepository;

    public OrganizationService(
        IOrganizationRepository repository,
        IPlatformSettingsRepository settingsRepository,
        ISubscriptionPlanRepository? planRepository = null)
    {
        _repository = repository;
        _settingsRepository = settingsRepository;
        _planRepository = planRepository;
    }

    public async Task<IReadOnlyCollection<OrganizationSummaryDto>> GetOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }

    public async Task<OrganizationDetailsDto> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<OrganizationMembership>? memberships = null,
        Guid? planId = null)
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

        if (planId.HasValue)
        {
            if (_planRepository is null)
            {
                throw new InvalidOperationException("Subscription plan catalog is unavailable.");
            }

            var plan = await _planRepository.GetByIdAsync(planId.Value, cancellationToken);
            if (plan is null || !plan.IsActive)
            {
                throw new ArgumentException("Choose an active subscription plan.", nameof(planId));
            }
            if (plan.MaxMembers > 0 && memberships is not null && memberships.Count > plan.MaxMembers)
            {
                throw new InvalidOperationException($"The subscription plan allows up to {plan.MaxMembers} members.");
            }

            organization.Subscription!.PlanId = plan.Id;
            organization.Subscription.Plan = plan;
            organization.Subscription.PlanName = plan.Name;
            organization.Subscription.Currency = plan.Currency;
            organization.Subscription.MonthlyPrice = plan.Price;
            organization.Subscription.BillingInterval = plan.BillingInterval;
            organization.Subscription.Status = plan.TrialDays > 0
                ? SubscriptionStatus.Trial
                : SubscriptionStatus.Active;
            organization.Subscription.ExpiresAt = plan.TrialDays > 0
                ? DateTime.UtcNow.AddDays(plan.TrialDays)
                : null;
        }

        if (memberships is not null)
        {
            if (memberships.Count(item => item.Role == OrganizationMemberRole.Owner) != 1)
            {
                throw new ArgumentException("Assign exactly one organization owner.", nameof(memberships));
            }
            if (memberships.Any(item =>
                    item.User is null
                    || item.User.Role == UserRole.Superuser
                    || !Enum.IsDefined(item.Role))
                || memberships.Select(item => item.User.Id).Distinct().Count() != memberships.Count)
            {
                throw new ArgumentException("Choose valid, unique non-Superuser organization members.", nameof(memberships));
            }

            foreach (var membership in memberships)
            {
                membership.Organization = organization;
                membership.OrganizationId = organization.Id;
                membership.UserId = membership.User.Id;
                organization.Members.Add(membership);
            }
        }

        var created = await _repository.AddAsync(organization, cancellationToken);

        return new OrganizationDetailsDto(
            created.Id,
            created.Name,
            created.Slug,
            created.Email,
            created.Status.ToString(),
            created.Subscription?.Status.ToString() ?? SubscriptionStatus.Trial.ToString(),
            created.CreatedAt,
            created.BaseApiUrl,
            created.Subscription?.PlanName,
            ToSubscriptionFeatures(created.Subscription?.Plan));
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

    public async Task<bool> ChangeSubscriptionPlanAsync(
        Guid organizationId,
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        if (_planRepository is null)
        {
            throw new InvalidOperationException("Subscription plan catalog is unavailable.");
        }

        var organization = await _repository.GetByIdAsync(organizationId, cancellationToken);
        if (organization is null)
        {
            return false;
        }

        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
        if (plan is null || !plan.IsActive)
        {
            throw new ArgumentException("Choose an active subscription plan.", nameof(planId));
        }

        var now = DateTime.UtcNow;
        var subscription = organization.Subscription;
        if (subscription is null)
        {
            subscription = new Subscription
            {
                OrganizationId = organization.Id,
                StartsAt = now,
                Status = plan.TrialDays > 0 ? SubscriptionStatus.Trial : SubscriptionStatus.Active,
                ExpiresAt = plan.TrialDays > 0 ? now.AddDays(plan.TrialDays) : null
            };
            organization.Subscription = subscription;
        }

        subscription.PlanId = plan.Id;
        subscription.Plan = plan;
        subscription.PlanName = plan.Name;
        subscription.Currency = plan.Currency;
        subscription.MonthlyPrice = plan.Price;
        subscription.BillingInterval = plan.BillingInterval;
        subscription.UpdatedAt = now;

        await _repository.UpdateAsync(organization, cancellationToken);
        return true;
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
            organization.BaseApiUrl,
            organization.Subscription?.PlanName,
            ToSubscriptionFeatures(organization.Subscription?.Plan));
    }

    private static OrganizationSubscriptionFeaturesDto? ToSubscriptionFeatures(SubscriptionPlan? plan) =>
        plan is null
            ? null
            : new OrganizationSubscriptionFeaturesDto(
                plan.Name,
                plan.Currency,
                plan.Price,
                plan.BillingInterval.ToString(),
                plan.MaxProjects,
                plan.MaxMembers,
                plan.StorageLimitGb,
                plan.HasApiAccess,
                plan.HasCustomDomains,
                plan.HasAdvancedAnalytics,
                plan.HasPrioritySupport,
                plan.HasSingleSignOn,
                plan.HasAuditLogs,
                plan.HasAutomatedBackups,
                plan.HasIntegrations,
                plan.HasCustomRoles,
                plan.SupportResponseHours);

    private static string CreateSlug(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var slug = string.Concat(normalized.Select(character => char.IsLetterOrDigit(character) ? character : '-'));
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "organization" : slug;
    }
}
