using CloudHubs.Application.Modules.Organizations.DTOs;
using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.UnitTests.Fakes;

namespace CloudHubs.UnitTests.Application.Organizations;

public class OrganizationServiceTests
{
    [Fact]
    public async Task OrganizationWithTenantProjectsCannotBeDeleted()
    {
        var repository = new OrganizationRepositoryStub { ProjectsExist = true };
        var service = new OrganizationService(repository, new PlatformSettingsRepositoryStub());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteOrganizationAsync(Guid.NewGuid()));

        Assert.False(repository.DeleteCalled);
    }

    [Fact]
    public async Task OrganizationCreationUsesPlatformDefaults()
    {
        var organizationRepository = new OrganizationRepositoryStub();
        var settingsRepository = new PlatformSettingsRepositoryStub
        {
            Settings = new PlatformSettings
            {
                DefaultOrganizationStatus = OrganizationStatus.Draft,
                DefaultTrialDays = 14,
                DefaultPlanName = "Launch",
                BillingCurrency = "EUR",
                MonthlyPrice = 19.99m
            }
        };
        var service = new OrganizationService(organizationRepository, settingsRepository);

        await service.CreateOrganizationAsync(
            new CreateOrganizationRequest("Acme", "owner@example.com", "acme", "", ""));

        Assert.Equal(OrganizationStatus.Draft, organizationRepository.AddedOrganization?.Status);
        Assert.Equal("Launch", organizationRepository.AddedOrganization?.Subscription?.PlanName);
        Assert.Equal("EUR", organizationRepository.AddedOrganization?.Subscription?.Currency);
        Assert.Equal(19.99m, organizationRepository.AddedOrganization?.Subscription?.MonthlyPrice);
        Assert.Equal("acme", organizationRepository.AddedOrganization?.Slug);
        Assert.InRange(
            (organizationRepository.AddedOrganization!.Subscription!.ExpiresAt!.Value - DateTime.UtcNow).TotalDays,
            13.99,
            14.01);
    }

    [Fact]
    public async Task OrganizationCreationGeneratesSlugFromNameWhenSlugIsNotProvided()
    {
        var repository = new OrganizationRepositoryStub();
        var service = new OrganizationService(repository, new PlatformSettingsRepositoryStub());

        await service.CreateOrganizationAsync(
            new CreateOrganizationRequest("Acme Cloud Hubs", "contact@example.com", "", "", ""));

        Assert.Equal("acme-cloud-hubs", repository.AddedOrganization?.Slug);
    }

    [Fact]
    public async Task OrganizationCreationAssignsSelectedPlanAndItsBillingTerms()
    {
        var plan = new SubscriptionPlan
        {
            Name = "Growth",
            Currency = "EUR",
            Price = 49.50m,
            BillingInterval = SubscriptionBillingInterval.Yearly,
            TrialDays = 14,
            MaxProjects = 20,
            MaxMembers = 50,
            StorageLimitGb = 100,
            HasApiAccess = true
        };
        var planRepository = new SubscriptionPlanRepositoryStub { Plan = plan };
        var organizationRepository = new OrganizationRepositoryStub();
        var service = new OrganizationService(
            organizationRepository,
            new PlatformSettingsRepositoryStub(),
            planRepository);

        await service.CreateOrganizationAsync(
            new CreateOrganizationRequest("Acme", "contact@example.com", "", "", ""),
            planId: plan.Id);

        var subscription = organizationRepository.AddedOrganization?.Subscription;
        Assert.Equal(plan.Id, subscription?.PlanId);
        Assert.Equal("Growth", subscription?.PlanName);
        Assert.Equal("EUR", subscription?.Currency);
        Assert.Equal(49.50m, subscription?.MonthlyPrice);
        Assert.Equal(SubscriptionBillingInterval.Yearly, subscription?.BillingInterval);
        Assert.Equal(SubscriptionStatus.Trial, subscription?.Status);
        Assert.InRange((subscription!.ExpiresAt!.Value - DateTime.UtcNow).TotalDays, 13.99, 14.01);
    }

    [Fact]
    public async Task OrganizationCreationLinksPaidAccountSubscriptionAndUsesItsBillingPeriod()
    {
        var plan = new SubscriptionPlan
        {
            Name = "Account Growth",
            Currency = "EUR",
            Price = 49.50m,
            BillingInterval = SubscriptionBillingInterval.Yearly,
            MaxMembers = 10
        };
        var owner = new User { Name = "Account Owner", Email = "owner@example.com" };
        var expiresAt = DateTime.UtcNow.AddMonths(11);
        var accountSubscription = new AccountSubscription
        {
            Id = Guid.NewGuid(),
            OwnerUserId = owner.Id,
            PlanId = plan.Id,
            Plan = plan,
            PlanName = plan.Name,
            Currency = plan.Currency,
            Price = plan.Price,
            BillingInterval = plan.BillingInterval,
            Status = SubscriptionStatus.Active,
            StartsAt = DateTime.UtcNow.AddDays(-20),
            ExpiresAt = expiresAt
        };
        var repository = new OrganizationRepositoryStub();
        var service = new OrganizationService(
            repository,
            new PlatformSettingsRepositoryStub(),
            new SubscriptionPlanRepositoryStub { Plan = plan });

        await service.CreateOrganizationAsync(
            new CreateOrganizationRequest("Account Child", "contact@example.com", "", "", ""),
            memberships: [new OrganizationMembership { User = owner, Role = OrganizationMemberRole.Owner }],
            planId: plan.Id,
            accountSubscription: accountSubscription);

        var created = repository.AddedOrganization!;
        Assert.Equal(accountSubscription.Id, created.AccountSubscriptionId);
        Assert.Equal(SubscriptionStatus.Active, created.Subscription?.Status);
        Assert.Equal(expiresAt, created.Subscription?.ExpiresAt);
        Assert.Equal(49.50m, created.Subscription?.MonthlyPrice);
        Assert.Equal(SubscriptionBillingInterval.Yearly, created.Subscription?.BillingInterval);
    }

    [Fact]
    public async Task OrganizationCreationRejectsInactiveSubscriptionPlans()
    {
        var plan = new SubscriptionPlan { IsActive = false };
        var service = new OrganizationService(
            new OrganizationRepositoryStub(),
            new PlatformSettingsRepositoryStub(),
            new SubscriptionPlanRepositoryStub { Plan = plan });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrganizationAsync(
                new CreateOrganizationRequest("Acme", "contact@example.com", "", "", ""),
                planId: plan.Id));
    }

    [Fact]
    public async Task OrganizationCreationRejectsInitialMembersAboveSelectedPlanLimit()
    {
        var plan = new SubscriptionPlan { MaxMembers = 1 };
        var owner = new User { Name = "Owner", Email = "owner@example.com" };
        var member = new User { Name = "Member", Email = "member@example.com" };
        var service = new OrganizationService(
            new OrganizationRepositoryStub(),
            new PlatformSettingsRepositoryStub(),
            new SubscriptionPlanRepositoryStub { Plan = plan });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrganizationAsync(
                new CreateOrganizationRequest("Acme", "contact@example.com", "", "", ""),
                memberships:
                [
                    new OrganizationMembership { User = owner, Role = OrganizationMemberRole.Owner },
                    new OrganizationMembership { User = member, Role = OrganizationMemberRole.Developer }
                ],
                planId: plan.Id));
    }

    [Fact]
    public async Task ChangingOrganizationPlanUpdatesBillingTermsAndPreservesSubscriptionLifecycle()
    {
        var expiry = DateTime.UtcNow.AddDays(20);
        var organization = new Organization
        {
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.PastDue,
                PlanName = "Old",
                ExpiresAt = expiry
            }
        };
        var plan = new SubscriptionPlan
        {
            Name = "Growth",
            Currency = "EUR",
            Price = 49.50m,
            BillingInterval = SubscriptionBillingInterval.Yearly,
            TrialDays = 30
        };
        var repository = new OrganizationRepositoryStub { ExistingOrganization = organization };
        var service = new OrganizationService(
            repository,
            new PlatformSettingsRepositoryStub(),
            new SubscriptionPlanRepositoryStub { Plan = plan });

        var changed = await service.ChangeSubscriptionPlanAsync(organization.Id, plan.Id);

        Assert.True(changed);
        Assert.Equal(plan.Id, organization.Subscription?.PlanId);
        Assert.Equal("Growth", organization.Subscription?.PlanName);
        Assert.Equal("EUR", organization.Subscription?.Currency);
        Assert.Equal(49.50m, organization.Subscription?.MonthlyPrice);
        Assert.Equal(SubscriptionBillingInterval.Yearly, organization.Subscription?.BillingInterval);
        Assert.Equal(SubscriptionStatus.PastDue, organization.Subscription?.Status);
        Assert.Equal(expiry, organization.Subscription?.ExpiresAt);
    }

    [Fact]
    public async Task ChangingOrganizationPlanRejectsInactivePlans()
    {
        var plan = new SubscriptionPlan { IsActive = false };
        var organization = new Organization { Subscription = new Subscription { PlanName = "Current" } };
        var service = new OrganizationService(
            new OrganizationRepositoryStub { ExistingOrganization = organization },
            new PlatformSettingsRepositoryStub(),
            new SubscriptionPlanRepositoryStub { Plan = plan });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ChangeSubscriptionPlanAsync(organization.Id, plan.Id));
        Assert.Equal("Current", organization.Subscription?.PlanName);
    }

    [Fact]
    public async Task OrganizationCreationAssignsOwnerAndScopedMembers()
    {
        var repository = new OrganizationRepositoryStub();
        var service = new OrganizationService(repository, new PlatformSettingsRepositoryStub());
        var owner = new User { Name = "Org Owner", Email = "owner@example.com", LoginId = "owner-id", PasswordHash = "hash" };
        var developer = new User { Name = "Developer", Email = "developer@example.com", LoginId = "developer-id", PasswordHash = "hash" };

        await service.CreateOrganizationAsync(
            new CreateOrganizationRequest("Acme", "contact@example.com", "acme", "", ""),
            memberships:
            [
                new OrganizationMembership { User = owner, Role = OrganizationMemberRole.Owner },
                new OrganizationMembership { User = developer, Role = OrganizationMemberRole.Developer }
            ]);

        var organization = repository.AddedOrganization;
        Assert.NotNull(organization);
        Assert.Equal(2, organization.Members.Count);
        Assert.Contains(organization.Members, membership =>
            membership.UserId == owner.Id
            && membership.OrganizationId == organization.Id
            && membership.Role == OrganizationMemberRole.Owner);
        Assert.Contains(organization.Members, membership =>
            membership.UserId == developer.Id
            && membership.OrganizationId == organization.Id
            && membership.Role == OrganizationMemberRole.Developer);
    }

    [Fact]
    public async Task OrganizationCreationRequiresOneUniqueNonSuperuserOwner()
    {
        var service = new OrganizationService(new OrganizationRepositoryStub(), new PlatformSettingsRepositoryStub());
        var user = new User { Name = "Org Admin", Email = "admin@example.com", Role = UserRole.OrganizationAdmin };
        var request = new CreateOrganizationRequest("Acme", "contact@example.com", "acme", "", "");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrganizationAsync(request, memberships: []));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrganizationAsync(
                request,
                memberships:
                [
                    new OrganizationMembership { User = user, Role = OrganizationMemberRole.Owner },
                    new OrganizationMembership { User = user, Role = OrganizationMemberRole.Admin }
                ]));

        user.Role = UserRole.Superuser;
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrganizationAsync(
                request,
                memberships: [new OrganizationMembership { User = user, Role = OrganizationMemberRole.Owner }]));
    }

    [Fact]
    public async Task OrganizationCreationCanBeDisabledInPlatformSettings()
    {
        var settingsRepository = new PlatformSettingsRepositoryStub
        {
            Settings = new PlatformSettings { AllowOrganizationCreation = false }
        };
        var service = new OrganizationService(new OrganizationRepositoryStub(), settingsRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrganizationAsync(
                new CreateOrganizationRequest("Acme", "owner@example.com", "acme", "", "")));
    }
}
