using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.UnitTests.Fakes;

namespace CloudHubs.UnitTests.Application.Organizations;

public class OrganizationTenantServiceTests
{
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
    public async Task CreateProjectAsync_ScaffoldsProjectFiles_WhenScaffolderIsProvided()
    {
        var organizationId = Guid.NewGuid();
        var user = new User { Name = "Developer", Email = "dev@example.com" };
        var organization = new Organization
        {
            Id = organizationId,
            Slug = "acme-corp",
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.Active,
                Plan = new SubscriptionPlan { HasApiAccess = true, MaxProjects = 10 }
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
        var scaffolder = new ProjectScaffolderStub();
        var tenantService = new OrganizationTenantService(
            new OrganizationRepositoryStub { ExistingOrganization = organization },
            roleRepository,
            projectRepository,
            new UserRepositoryStub { Users = [user] },
            new OrganizationPermissionService(roleRepository),
            scaffolder);

        var project = await tenantService.CreateProjectAsync(
            organizationId,
            user.Id,
            "My Node Service",
            "my-node-service",
            "Express MVC project",
            "",
            "nodejs-mvc");

        Assert.Equal("nodejs-mvc", project.TemplateId);
        Assert.Equal("projects/acme-corp/my-node-service", project.RootPath);
        Assert.True(scaffolder.ScaffoldCalled);
        Assert.Equal("projects/acme-corp/my-node-service", scaffolder.LastRootPath);
        Assert.Equal("nodejs-mvc", scaffolder.LastTemplateId);
        Assert.Equal("acme-corp", scaffolder.LastOrgSlug);
        Assert.Equal("my-node-service", scaffolder.LastProjectSlug);
    }

    [Theory]
    [InlineData("nodejs-modules")]
    [InlineData("go")]
    [InlineData("java")]
    public async Task CreateProjectAsync_SupportsVariousArchitecturalTemplates(string templateId)
    {
        var organizationId = Guid.NewGuid();
        var user = new User { Name = "Developer", Email = "dev@example.com" };
        var organization = new Organization
        {
            Id = organizationId,
            Slug = "tech-lab",
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.Active,
                Plan = new SubscriptionPlan { HasApiAccess = true, MaxProjects = 10 }
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
        var scaffolder = new ProjectScaffolderStub();
        var tenantService = new OrganizationTenantService(
            new OrganizationRepositoryStub { ExistingOrganization = organization },
            roleRepository,
            projectRepository,
            new UserRepositoryStub { Users = [user] },
            new OrganizationPermissionService(roleRepository),
            scaffolder);

        var project = await tenantService.CreateProjectAsync(
            organizationId,
            user.Id,
            "Sample " + templateId,
            "sample-" + templateId,
            "Description for " + templateId,
            "",
            templateId);

        Assert.Equal(templateId, project.TemplateId);
        Assert.Equal($"projects/tech-lab/sample-{templateId}", project.RootPath);
        Assert.True(scaffolder.ScaffoldCalled);
        Assert.Equal(templateId, scaffolder.LastTemplateId);
    }
}
