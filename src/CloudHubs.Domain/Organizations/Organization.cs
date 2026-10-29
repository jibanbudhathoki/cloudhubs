using CloudHubs.Domain.Subscriptions;

namespace CloudHubs.Domain.Organizations;

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;
    public string? ApiKey { get; set; }
    public string? BaseApiUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Subscription? Subscription { get; set; }
    public ICollection<OrganizationMembership> Members { get; set; } = new List<OrganizationMembership>();

    public bool IsAvailableForUse()
    {
        if (Status != OrganizationStatus.Active)
        {
            return false;
        }

        return Subscription is null || Subscription.IsAvailable();
    }
}
