using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Subscriptions;

namespace CloudHubs.UnitTests;

public class Phase1FoundationTests
{
    [Fact]
    public void OrganizationStartsActiveWhenSubscriptionIsActive()
    {
        var organization = new Organization
        {
            Name = "Acme",
            Status = OrganizationStatus.Active,
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.Active,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            }
        };

        Assert.True(organization.IsAvailableForUse());
    }

    [Fact]
    public void OrganizationIsBlockedWhenSubscriptionIsExpired()
    {
        var organization = new Organization
        {
            Name = "Acme",
            Status = OrganizationStatus.Active,
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.Expired,
                ExpiresAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        Assert.False(organization.IsAvailableForUse());
    }
}
