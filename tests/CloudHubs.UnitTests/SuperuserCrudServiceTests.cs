using CloudHubs.Application.Organizations;
using CloudHubs.Application.Projects;
using CloudHubs.Application.Users;
using CloudHubs.Contracts.Organizations;
using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Projects;
using CloudHubs.Domain.Subscriptions;
using CloudHubs.Domain.Users;

namespace CloudHubs.UnitTests;

public class SuperuserCrudServiceTests
{
    [Fact]
    public async Task PlatformProjectIsIndependentFromOrganizations()
    {
        var repository = new PlatformProjectRepositoryStub();
        var service = new PlatformProjectService(repository);

        await service.CreateProjectAsync("Platform Owner Project", "", "Internal tooling", "platform/owner-project", "dotnet", true);

        Assert.Equal("platform-owner-project", repository.AddedProject?.Slug);
        Assert.Null(typeof(PlatformProject).GetProperty(nameof(Project.OrganizationId)));
    }

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

    [Fact]
    public async Task CustomOrganizationRolesValidatePermissionsAndAssignToMembers()
    {
        var organizationId = Guid.NewGuid();
        var user = new User { Name = "Developer", Email = "dev@example.com" };
        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            User = user,
            Role = OrganizationMemberRole.Developer
        };
        var repository = new OrganizationRoleRepositoryStub { Membership = membership };
        var service = new OrganizationRoleService(repository);

        var role = await service.CreateRoleAsync(
            organizationId,
            "  Analyst ",
            " Read access ",
            OrganizationPermission.ViewProjects | OrganizationPermission.ViewAuditLog);

        Assert.Equal("Analyst", role.Name);
        Assert.Equal("Read access", role.Description);
        Assert.True(await service.AssignRoleAsync(organizationId, user.Id, role.Id));
        Assert.Equal(role.Id, membership.CustomRoleId);
        var permissionService = new OrganizationPermissionService(repository);
        Assert.True(await permissionService.HasPermissionAsync(
            organizationId, user.Id, OrganizationPermission.ViewAuditLog));
        Assert.False(await permissionService.HasPermissionAsync(
            organizationId, user.Id, OrganizationPermission.ManageBilling));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateRoleAsync(organizationId, "No access", "", OrganizationPermission.None));
    }

    [Fact]
    public async Task OrganizationOwnerCannotBeAssignedACustomRole()
    {
        var organizationId = Guid.NewGuid();
        var owner = new User { Name = "Owner", Email = "owner@example.com" };
        var role = new OrganizationRole
        {
            OrganizationId = organizationId,
            Organization = new Organization { Id = organizationId },
            Name = "Limited",
            Permissions = OrganizationPermission.ViewProjects
        };
        var repository = new OrganizationRoleRepositoryStub
        {
            Roles = [role],
            Membership = new OrganizationMembership
            {
                OrganizationId = organizationId,
                UserId = owner.Id,
                User = owner,
                Role = OrganizationMemberRole.Owner
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new OrganizationRoleService(repository).AssignRoleAsync(organizationId, owner.Id, role.Id));
        Assert.Null(repository.Membership?.CustomRoleId);
    }

    [Fact]
    public async Task OrganizationProjectCreationEnforcesApiPermissionAndProjectLimit()
    {
        var organizationId = Guid.NewGuid();
        var user = new User { Name = "Developer", Email = "dev@example.com" };
        var plan = new SubscriptionPlan { HasApiAccess = true, MaxProjects = 1 };
        var organization = new Organization
        {
            Id = organizationId,
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.Active,
                Plan = plan
            }
        };
        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            User = user,
            Role = OrganizationMemberRole.Developer
        };
        var roleRepository = new OrganizationRoleRepositoryStub { Membership = membership };
        var projectRepository = new OrganizationProjectRepositoryStub { CurrentCount = 1 };
        var tenantService = new OrganizationTenantService(
            new OrganizationRepositoryStub { ExistingOrganization = organization },
            roleRepository,
            projectRepository,
            new UserRepositoryStub { Users = [user] },
            new OrganizationPermissionService(roleRepository));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenantService.CreateProjectAsync(organizationId, user.Id, "One", "one", "", "", "default"));
        Assert.Null(projectRepository.AddedProject);
    }

    [Fact]
    public async Task OrganizationProjectCreationRejectsPlansWithoutApiAccess()
    {
        var organizationId = Guid.NewGuid();
        var user = new User { Name = "Developer", Email = "dev@example.com" };
        var organization = new Organization
        {
            Id = organizationId,
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.Active,
                Plan = new SubscriptionPlan { HasApiAccess = false }
            }
        };
        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            User = user,
            Role = OrganizationMemberRole.Developer
        };
        var roleRepository = new OrganizationRoleRepositoryStub { Membership = membership };
        var tenantService = new OrganizationTenantService(
            new OrganizationRepositoryStub { ExistingOrganization = organization },
            roleRepository,
            new OrganizationProjectRepositoryStub(),
            new UserRepositoryStub { Users = [user] },
            new OrganizationPermissionService(roleRepository));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tenantService.GetProjectsAsync(organizationId, user.Id));
    }

    [Fact]
    public async Task OrganizationPortalCanCreateProjectsUsingAnAccountPlanWithoutApiAccess()
    {
        var organizationId = Guid.NewGuid();
        var user = new User { Name = "Developer", Email = "dev@example.com" };
        var accountSubscription = new AccountSubscription
        {
            OwnerUserId = user.Id,
            Plan = new SubscriptionPlan { HasApiAccess = false, MaxProjects = 2 }
        };
        var organization = new Organization
        {
            Id = organizationId,
            AccountSubscriptionId = accountSubscription.Id,
            AccountSubscription = accountSubscription
        };
        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            User = user,
            Role = OrganizationMemberRole.Developer
        };
        var roleRepository = new OrganizationRoleRepositoryStub { Membership = membership };
        var projectRepository = new OrganizationProjectRepositoryStub();
        var tenantService = new OrganizationTenantService(
            new OrganizationRepositoryStub { ExistingOrganization = organization },
            roleRepository,
            projectRepository,
            new UserRepositoryStub { Users = [user] },
            new OrganizationPermissionService(roleRepository));

        var project = await tenantService.CreateProjectAsync(
            organizationId,
            user.Id,
            "Portal project",
            "portal-project",
            "",
            "",
            "default",
            requireApiAccess: false);

        Assert.Equal("portal-project", project.Slug);
        Assert.Same(project, projectRepository.AddedProject);
    }

    [Fact]
    public async Task OrganizationProjectCreationUsesUnlimitedPlanAndNormalizesSlug()
    {
        var organizationId = Guid.NewGuid();
        var user = new User { Name = "Developer", Email = "dev@example.com" };
        var organization = new Organization
        {
            Id = organizationId,
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.Active,
                Plan = new SubscriptionPlan { HasApiAccess = true, MaxProjects = 0 }
            }
        };
        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            User = user,
            Role = OrganizationMemberRole.Developer
        };
        var roleRepository = new OrganizationRoleRepositoryStub { Membership = membership };
        var projectRepository = new OrganizationProjectRepositoryStub();
        var tenantService = new OrganizationTenantService(
            new OrganizationRepositoryStub { ExistingOrganization = organization },
            roleRepository,
            projectRepository,
            new UserRepositoryStub { Users = [user] },
            new OrganizationPermissionService(roleRepository));

        var project = await tenantService.CreateProjectAsync(
            organizationId, user.Id, "Sample project", "Sample_Project", "Description", "/safe", "default");

        Assert.Equal(organizationId, project.OrganizationId);
        Assert.Equal("sample_project", project.Slug);
        Assert.Equal(project.Id, projectRepository.AddedProject?.Id);
    }

    [Fact]
    public async Task OrganizationMemberCreationRejectsExceedingPlanLimit()
    {
        var organizationId = Guid.NewGuid();
        var user = new User { Name = "Owner", Email = "owner@example.com" };
        var organization = new Organization
        {
            Id = organizationId,
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.Active,
                Plan = new SubscriptionPlan { HasApiAccess = true, MaxMembers = 1 }
            }
        };
        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            User = user,
            Role = OrganizationMemberRole.Owner
        };
        var roleRepository = new OrganizationRoleRepositoryStub { Membership = membership };
        var newMember = new User
        {
            Name = "New member",
            Email = "new@example.com",
            PasswordHash = "hashed-password",
            IsActive = true
        };
        var users = new UserRepositoryStub { Users = [user, newMember] };
        var tenantService = new OrganizationTenantService(
            new OrganizationRepositoryStub { ExistingOrganization = organization },
            roleRepository,
            new OrganizationProjectRepositoryStub(),
            users,
            new OrganizationPermissionService(roleRepository));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenantService.AddMemberAsync(organizationId, user.Id, "new@example.com", OrganizationMemberRole.Viewer));
    }

    [Fact]
    public async Task OrganizationMemberCreationUsesSelectedOrganizationAndRole()
    {
        var organizationId = Guid.NewGuid();
        var owner = new User { Name = "Owner", Email = "owner@example.com" };
        var organization = new Organization
        {
            Id = organizationId,
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.Active,
                Plan = new SubscriptionPlan { HasApiAccess = true, MaxMembers = 8 }
            }
        };
        var roleRepository = new OrganizationRoleRepositoryStub
        {
            Membership = new OrganizationMembership
            {
                OrganizationId = organizationId,
                UserId = owner.Id,
                User = owner,
                Role = OrganizationMemberRole.Owner
            }
        };
        var member = new User
        {
            Name = "New member",
            Email = "new@example.com",
            LoginId = "IDF15B4C",
            PasswordHash = "hashed-password",
            Role = UserRole.Developer
        };
        var tenantService = new OrganizationTenantService(
            new OrganizationRepositoryStub { ExistingOrganization = organization },
            roleRepository,
            new OrganizationProjectRepositoryStub(),
            new UserRepositoryStub { Users = [owner] },
            new OrganizationPermissionService(roleRepository));

        var membership = await tenantService.CreateMemberAsync(
            organizationId, owner.Id, member, OrganizationMemberRole.Developer, requireApiAccess: false);

        Assert.Equal(organizationId, membership.OrganizationId);
        Assert.Equal(member.Id, membership.UserId);
        Assert.Equal(OrganizationMemberRole.Developer, membership.Role);
        Assert.Same(member, roleRepository.CreatedMember);
    }

    [Fact]
    public async Task DisabledOrganizationUserCannotUseAnExistingMembership()
    {
        var organizationId = Guid.NewGuid();
        var user = new User { Name = "Disabled", Email = "disabled@example.com", IsActive = false };
        var organization = new Organization
        {
            Id = organizationId,
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.Active,
                Plan = new SubscriptionPlan { HasApiAccess = true }
            }
        };
        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            User = user,
            Role = OrganizationMemberRole.Owner
        };
        var roleRepository = new OrganizationRoleRepositoryStub { Membership = membership };
        var tenantService = new OrganizationTenantService(
            new OrganizationRepositoryStub { ExistingOrganization = organization },
            roleRepository,
            new OrganizationProjectRepositoryStub(),
            new UserRepositoryStub { Users = [user] },
            new OrganizationPermissionService(roleRepository));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tenantService.GetProjectsAsync(organizationId, user.Id));
    }

    [Fact]
    public async Task OrganizationMemberRoleCanBeUpdatedWithoutChangingMembership()
    {
        var organizationId = Guid.NewGuid();
        var admin = new User { Name = "Admin", Email = "admin@example.com" };
        var member = new User { Name = "Member", Email = "member@example.com" };
        var organization = new Organization
        {
            Id = organizationId,
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.Active,
                Plan = new SubscriptionPlan { HasApiAccess = true }
            }
        };
        var actorMembership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = admin.Id,
            User = admin,
            Role = OrganizationMemberRole.Admin
        };
        var memberMembership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = member.Id,
            User = member,
            Role = OrganizationMemberRole.Viewer
        };
        var roleRepository = new OrganizationRoleRepositoryStub
        {
            Memberships = [actorMembership, memberMembership]
        };
        var tenantService = new OrganizationTenantService(
            new OrganizationRepositoryStub { ExistingOrganization = organization },
            roleRepository,
            new OrganizationProjectRepositoryStub(),
            new UserRepositoryStub { Users = [admin, member] },
            new OrganizationPermissionService(roleRepository));

        var updated = await tenantService.UpdateMemberRoleAsync(
            organizationId, admin.Id, member.Id, OrganizationMemberRole.Developer, null);

        Assert.NotNull(updated);
        Assert.Equal(OrganizationMemberRole.Developer, memberMembership.Role);
        Assert.Equal(organizationId, memberMembership.OrganizationId);
    }

    [Fact]
    public async Task AuditLogServicePersistsEventData()
    {
        var repository = new AuditLogRepositoryStub();
        var service = new AuditLogService(repository);

        await service.RecordAsync("actor-id", "Superuser", "POST /su/users/create", "users", "", null, "Response status: 302.");

        Assert.Equal("Superuser", repository.AddedEntry?.ActorName);
        Assert.Equal("POST /su/users/create", repository.AddedEntry?.Action);
        Assert.Equal("Response status: 302.", repository.AddedEntry?.Details);
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

    [Fact]
    public async Task PlatformSettingsRejectUnsafeOrIncompleteValues()
    {
        var service = new PlatformSettingsService(new PlatformSettingsRepositoryStub());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(new PlatformSettings { SessionTimeoutMinutes = 5 }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(new PlatformSettings { NotifyOnOrganizationCreated = true }));

        var saved = await service.UpdateAsync(new PlatformSettings { BillingCurrency = "eur" });
        Assert.Equal("EUR", saved.BillingCurrency);
    }

    [Fact]
    public async Task PlatformSettingsSectionUpdateDoesNotValidateOrOverwriteOtherSections()
    {
        var existingSettings = new PlatformSettings
        {
            PlatformName = "Old name",
            NotificationEmail = "invalid-address",
            NotifyOnOrganizationCreated = true,
            DefaultPlanName = "Existing plan"
        };
        var repository = new PlatformSettingsRepositoryStub { Settings = existingSettings };
        var service = new PlatformSettingsService(repository);

        existingSettings.PlatformName = "  CloudHubs  ";
        var saved = await service.UpdateSectionAsync(existingSettings, "profile");

        Assert.Equal("CloudHubs", saved.PlatformName);
        Assert.Equal("invalid-address", saved.NotificationEmail);
        Assert.True(saved.NotifyOnOrganizationCreated);
        Assert.Equal("Existing plan", saved.DefaultPlanName);
    }

    [Fact]
    public async Task PlatformSettingsSectionUpdateValidatesOnlyTheSelectedSection()
    {
        var service = new PlatformSettingsService(new PlatformSettingsRepositoryStub
        {
            Settings = new PlatformSettings { NotificationEmail = "not-an-email" }
        });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateSectionAsync(new PlatformSettings { SupportEmail = "not-an-email" }, "profile"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateSectionAsync(new PlatformSettings
            {
                NotifyOnOrganizationCreated = true,
                NotificationEmail = ""
            }, "notifications"));
    }

    [Fact]
    public async Task UserCreationNormalizesEmailAndValidatesRole()
    {
        var repository = new UserRepositoryStub();
        var service = new UserService(repository);

        await service.CreateUserAsync(
            "Platform User",
            "USER@EXAMPLE.COM",
            "hashed-password",
            "Developer",
            true);

        Assert.Equal("user@example.com", repository.AddedUser?.Email);
        Assert.Equal(repository.AddedUser?.Id.ToString(), repository.AddedUser?.LoginId);
        Assert.Equal("hashed-password", repository.AddedUser?.PasswordHash);
        Assert.Equal(UserRole.Developer, repository.AddedUser?.Role);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateUserAsync(
                "Invalid Role",
                "invalid@example.com",
                "hashed-password",
                "Superuser",
                true));
    }

    [Fact]
    public async Task SuperuserAccountsCannotBeManagedThroughRegularUserCrud()
    {
        var superuser = new User
        {
            Name = "Platform Superuser",
            Email = "admin@example.com",
            Role = UserRole.Superuser
        };
        var repository = new UserRepositoryStub
        {
            ExistingUser = superuser
        };
        var service = new UserService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateUserAsync(
                "Another Superuser",
                "other@example.com",
                "hashed-password",
                "Superuser",
                true));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateUserAsync(superuser.Id, "Platform Superuser", "admin@example.com", "Viewer", true));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteUserAsync(superuser.Id));
    }

    private sealed class OrganizationRepositoryStub : IOrganizationRepository
    {
        public bool ProjectsExist { get; init; }
        public bool DeleteCalled { get; private set; }
        public Organization? AddedOrganization { get; private set; }
        public Organization? ExistingOrganization { get; set; }

        public Task<IReadOnlyCollection<OrganizationSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<OrganizationSummaryDto>>([]);
        public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistingOrganization?.Id == id ? ExistingOrganization : null);
        public Task<bool> SlugExistsAsync(string slug, Guid? exceptId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasProjectsAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(ProjectsExist);
        public Task<Organization> AddAsync(Organization organization, CancellationToken cancellationToken = default)
        {
            AddedOrganization = organization;
            return Task.FromResult(organization);
        }
        public Task<Organization> UpdateAsync(Organization organization, CancellationToken cancellationToken = default)
        {
            ExistingOrganization ??= organization;
            return Task.FromResult(organization);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeleteCalled = true;
            return Task.FromResult(true);
        }
    }

    private sealed class PlatformSettingsRepositoryStub : IPlatformSettingsRepository
    {
        public PlatformSettings Settings { get; init; } = new();

        public Task<PlatformSettings> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Settings);

        public Task<PlatformSettings> UpdateAsync(PlatformSettings settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);
    }

    private sealed class SubscriptionPlanRepositoryStub : ISubscriptionPlanRepository
    {
        public SubscriptionPlan? Plan { get; init; }
        public bool IsAssigned { get; init; }

        public Task<IReadOnlyCollection<SubscriptionPlan>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<SubscriptionPlan>>(Plan is null ? [] : [Plan]);
        public Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Plan?.Id == id ? Plan : null);
        public Task<bool> NameExistsAsync(string name, Guid? exceptId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Plan is not null && Plan.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && Plan.Id != exceptId);
        public Task<SubscriptionPlan> AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default) => Task.FromResult(plan);
        public Task<SubscriptionPlan> UpdateAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default) => Task.FromResult(plan);
        public Task<IReadOnlyDictionary<Guid, int>> GetOrganizationCountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());
        public Task<bool> IsAssignedAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(IsAssigned);
        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class OrganizationRoleRepositoryStub : IOrganizationRoleRepository
    {
        public List<OrganizationRole> Roles { get; init; } = [];
        public OrganizationMembership? Membership { get; init; }
        public IReadOnlyCollection<OrganizationMembership> Memberships { get; init; } = [];
        public User? CreatedMember { get; private set; }

        private IEnumerable<OrganizationMembership> AllMemberships =>
            Membership is null ? Memberships : Memberships.Append(Membership);

        public Task<IReadOnlyCollection<OrganizationRole>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<OrganizationRole>>(Roles.Where(role => role.OrganizationId == organizationId).ToArray());
        public Task<OrganizationRole?> GetByIdAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Roles.FirstOrDefault(role => role.OrganizationId == organizationId && role.Id == roleId));
        public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? exceptRoleId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Roles.Any(role => role.OrganizationId == organizationId
                && role.Id != exceptRoleId
                && role.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        public Task<bool> IsAssignedAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(AllMemberships.Any(membership =>
                membership.OrganizationId == organizationId && membership.CustomRoleId == roleId));
        public Task<OrganizationRole> AddAsync(OrganizationRole role, CancellationToken cancellationToken = default)
        {
            Roles.Add(role);
            return Task.FromResult(role);
        }
        public Task<OrganizationRole> UpdateAsync(OrganizationRole role, CancellationToken cancellationToken = default) => Task.FromResult(role);
        public Task<bool> DeleteAsync(OrganizationRole role, CancellationToken cancellationToken = default) =>
            Task.FromResult(Roles.Remove(role));
        public Task<IReadOnlyCollection<OrganizationMembership>> GetMembersAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<OrganizationMembership>>(
                AllMemberships.Where(membership => membership.OrganizationId == organizationId).ToArray());
        public Task<OrganizationMembership?> GetMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(AllMemberships.FirstOrDefault(membership =>
                membership.OrganizationId == organizationId && membership.UserId == userId));
        public Task<IReadOnlyCollection<OrganizationMembership>> GetMembershipsForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<OrganizationMembership>>(
                AllMemberships.Where(membership => membership.UserId == userId).ToArray());
        public Task<OrganizationMembership> AddMembershipAsync(OrganizationMembership membership, CancellationToken cancellationToken = default) =>
            Task.FromResult(membership);
        public Task<OrganizationMembership> AddMembershipIfBelowLimitAsync(OrganizationMembership membership, int maximumMembers, CancellationToken cancellationToken = default)
        {
            if (maximumMembers > 0 && Membership is not null)
                throw new InvalidOperationException($"The subscription plan allows up to {maximumMembers} members.");
            return Task.FromResult(membership);
        }
        public Task<OrganizationMembership> AddNewMemberIfBelowLimitAsync(
            User user,
            Guid organizationId,
            OrganizationMemberRole role,
            int maximumMembers,
            CancellationToken cancellationToken = default)
        {
            CreatedMember = user;
            return Task.FromResult(new OrganizationMembership
            {
                OrganizationId = organizationId,
                UserId = user.Id,
                User = user,
                Role = role
            });
        }
        public Task<bool> SaveMembershipAsync(OrganizationMembership membership, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> RemoveMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class OrganizationProjectRepositoryStub : IOrganizationProjectRepository
    {
        public int CurrentCount { get; init; }
        public Project? AddedProject { get; private set; }
        public Task<IReadOnlyCollection<Project>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Project>>([]);
        public Task<Project?> GetByIdAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Project?>(null);
        public Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken = default) => Task.FromResult(CurrentCount);
        public Task<bool> SlugExistsAsync(Guid organizationId, string slug, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default)
        {
            AddedProject = project;
            return Task.FromResult(project);
        }
        public Task<Project> AddIfBelowLimitAsync(Project project, int maximumProjects, CancellationToken cancellationToken = default)
        {
            if (maximumProjects > 0 && CurrentCount >= maximumProjects)
                throw new InvalidOperationException($"The subscription plan allows up to {maximumProjects} projects.");
            AddedProject = project;
            return Task.FromResult(project);
        }
        public Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default) => Task.FromResult(project);
        public Task<bool> DeleteAsync(Project project, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class AuditLogRepositoryStub : IAuditLogRepository
    {
        public AuditLogEntry? AddedEntry { get; private set; }
        public Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
        {
            AddedEntry = entry;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyCollection<AuditLogEntry>> SearchAsync(
            string? query,
            Guid? organizationId,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditLogEntry>>([]);
        public Task<int> CountAsync(
            string? query,
            Guid? organizationId,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class UserRepositoryStub : IUserRepository
    {
        public User? AddedUser { get; private set; }
        public User? ExistingUser { get; init; }
        public IReadOnlyCollection<User> Users { get; init; } = [];

        public Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Users);
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult((User?)new[] { AddedUser, ExistingUser }.Concat(Users).FirstOrDefault(user => user?.Id == id));
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult((User?)new[] { AddedUser, ExistingUser }.Concat(Users)
                .FirstOrDefault(user => string.Equals(user?.Email, email, StringComparison.OrdinalIgnoreCase)));
        public Task<User?> GetByLoginIdAsync(string loginId, CancellationToken cancellationToken = default) =>
            Task.FromResult((User?)new[] { AddedUser, ExistingUser }.Concat(Users)
                .FirstOrDefault(user => string.Equals(user?.LoginId, loginId, StringComparison.OrdinalIgnoreCase)));
        public Task<bool> EmailExistsAsync(string email, Guid? exceptId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
        {
            AddedUser = user;
            return Task.FromResult(user);
        }

        public Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.FromResult(user);
        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class PlatformProjectRepositoryStub : IPlatformProjectRepository
    {
        public PlatformProject? AddedProject { get; private set; }

        public Task<IReadOnlyCollection<PlatformProject>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<PlatformProject>>([]);
        public Task<PlatformProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PlatformProject?>(null);
        public Task<bool> SlugExistsAsync(string slug, Guid? exceptId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<PlatformProject> AddAsync(PlatformProject project, CancellationToken cancellationToken = default)
        {
            AddedProject = project;
            return Task.FromResult(project);
        }

        public Task<PlatformProject> UpdateAsync(PlatformProject project, CancellationToken cancellationToken = default) => Task.FromResult(project);
        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}