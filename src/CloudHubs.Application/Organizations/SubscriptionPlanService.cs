using CloudHubs.Domain.Subscriptions;

namespace CloudHubs.Application.Organizations;

public class SubscriptionPlanService(ISubscriptionPlanRepository repository)
{
    public Task<IReadOnlyCollection<SubscriptionPlan>> GetPlansAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(activeOnly, cancellationToken);

    public Task<SubscriptionPlan?> GetPlanAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, int>> GetOrganizationCountsAsync(CancellationToken cancellationToken = default) =>
        repository.GetOrganizationCountsAsync(cancellationToken);

    public async Task<SubscriptionPlan> CreatePlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        NormalizeAndValidate(plan);
        if (await repository.NameExistsAsync(plan.Name, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException("A subscription plan with this name already exists.");
        }

        return await repository.AddAsync(plan, cancellationToken);
    }

    public async Task<SubscriptionPlan?> UpdatePlanAsync(
        Guid id,
        SubscriptionPlan plan,
        CancellationToken cancellationToken = default)
    {
        NormalizeAndValidate(plan);
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return null;
        if (await repository.NameExistsAsync(plan.Name, id, cancellationToken))
        {
            throw new InvalidOperationException("A subscription plan with this name already exists.");
        }

        existing.Name = plan.Name;
        existing.Description = plan.Description;
        existing.Currency = plan.Currency;
        existing.Price = plan.Price;
        existing.BillingInterval = plan.BillingInterval;
        existing.TrialDays = plan.TrialDays;
        existing.MaxOrganizations = plan.MaxOrganizations;
        existing.MaxProjects = plan.MaxProjects;
        existing.MaxMembers = plan.MaxMembers;
        existing.StorageLimitGb = plan.StorageLimitGb;
        existing.HasApiAccess = plan.HasApiAccess;
        existing.HasCustomDomains = plan.HasCustomDomains;
        existing.HasAdvancedAnalytics = plan.HasAdvancedAnalytics;
        existing.HasPrioritySupport = plan.HasPrioritySupport;
        existing.HasSingleSignOn = plan.HasSingleSignOn;
        existing.HasAuditLogs = plan.HasAuditLogs;
        existing.HasAutomatedBackups = plan.HasAutomatedBackups;
        existing.HasIntegrations = plan.HasIntegrations;
        existing.HasCustomRoles = plan.HasCustomRoles;
        existing.SupportResponseHours = plan.SupportResponseHours;
        existing.IsActive = plan.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        return await repository.UpdateAsync(existing, cancellationToken);
    }

    public async Task<bool> DeletePlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await repository.IsAssignedAsync(id, cancellationToken))
        {
            throw new InvalidOperationException("A plan assigned to an organization or account cannot be deleted. Deactivate it instead.");
        }

        return await repository.DeleteAsync(id, cancellationToken);
    }

    private static void NormalizeAndValidate(SubscriptionPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.Name) || plan.Name.Trim().Length > 100)
            throw new ArgumentException("Plan name is required and must be 100 characters or fewer.", nameof(plan));
        if (plan.Description?.Length > 1000)
            throw new ArgumentException("Plan description must be 1,000 characters or fewer.", nameof(plan));
        if (string.IsNullOrWhiteSpace(plan.Currency)
            || plan.Currency.Length != 3
            || !plan.Currency.All(char.IsAsciiLetter))
            throw new ArgumentException("Currency must be a three-letter ISO currency code.", nameof(plan));
        if (plan.Price is < 0 or > 9999999999m)
            throw new ArgumentException("Plan price must be between 0 and 9,999,999,999.", nameof(plan));
        if (!Enum.IsDefined(plan.BillingInterval))
            throw new ArgumentException("Choose a valid billing interval.", nameof(plan));
        if (plan.TrialDays is < 0 or > 365)
            throw new ArgumentException("Trial duration must be between 0 and 365 days.", nameof(plan));
        if (plan.MaxOrganizations < 0 || plan.MaxProjects < 0 || plan.MaxMembers < 0 || plan.StorageLimitGb < 0)
            throw new ArgumentException("Plan limits cannot be negative. Use 0 for unlimited.", nameof(plan));
        if (plan.SupportResponseHours is < 0 or > 720)
            throw new ArgumentException("Support response target must be between 0 and 720 hours. Use 0 for no SLA.", nameof(plan));

        plan.Name = plan.Name.Trim();
        plan.Description = plan.Description?.Trim() ?? string.Empty;
        plan.Currency = plan.Currency.ToUpperInvariant();
    }
}
