using CloudHubs.Application.Modules.Subscriptions.Services;
using CloudHubs.Domain.Entities;
using CloudHubs.UnitTests.Fakes;

namespace CloudHubs.UnitTests.Application.Subscriptions;

public class SubscriptionPlanServiceTests
{
    [Fact]
    public async Task SubscriptionPlansValidateBillingAndFeatureLimits()
    {
        var repository = new SubscriptionPlanRepositoryStub();
        var service = new SubscriptionPlanService(repository);

        var created = await service.CreatePlanAsync(new SubscriptionPlan
        {
            Name = "  Growth  ",
            Currency = "eur",
            Price = 25,
            MaxOrganizations = 4,
            MaxProjects = 10,
            MaxMembers = 20,
            StorageLimitGb = 50,
            HasSingleSignOn = true,
            HasAuditLogs = true,
            HasAutomatedBackups = true,
            HasIntegrations = true,
            HasCustomRoles = true,
            SupportResponseHours = 4
        });

        Assert.Equal("Growth", created.Name);
        Assert.Equal("EUR", created.Currency);
        Assert.Equal(4, created.MaxOrganizations);
        Assert.True(created.HasSingleSignOn);
        Assert.True(created.HasAuditLogs);
        Assert.True(created.HasAutomatedBackups);
        Assert.True(created.HasIntegrations);
        Assert.True(created.HasCustomRoles);
        Assert.Equal(4, created.SupportResponseHours);
        Assert.True(await service.DeletePlanAsync(created.Id));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePlanAsync(new SubscriptionPlan { Name = "Bad", Currency = "US", MaxMembers = -1 }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePlanAsync(new SubscriptionPlan { Name = "Bad organization limit", Currency = "USD", MaxOrganizations = -1 }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePlanAsync(new SubscriptionPlan { Name = "Bad SLA", Currency = "USD", SupportResponseHours = 721 }));
    }

    [Fact]
    public async Task AssignedSubscriptionPlansCannotBeDeleted()
    {
        var service = new SubscriptionPlanService(new SubscriptionPlanRepositoryStub { IsAssigned = true });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeletePlanAsync(Guid.NewGuid()));
    }
}
