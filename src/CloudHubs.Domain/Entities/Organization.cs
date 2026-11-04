using CloudHubs.Domain.Common;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Domain.Entities;

public class Organization : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;
    public string? ApiKey { get; set; }
    public string? BaseApiUrl { get; set; }
    public Subscription? Subscription { get; set; }
    public Guid? AccountSubscriptionId { get; set; }
    public AccountSubscription? AccountSubscription { get; set; }
    public ICollection<OrganizationMembership> Members { get; set; } = new List<OrganizationMembership>();

    public bool IsAvailableForUse()
    {
        if (Status != OrganizationStatus.Active)
        {
            return false;
        }

        return AccountSubscriptionId is not null
            ? AccountSubscription?.IsAvailable() == true
            : Subscription is null || Subscription.IsAvailable();
    }
}
