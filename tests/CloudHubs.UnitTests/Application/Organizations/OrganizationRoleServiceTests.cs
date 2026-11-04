using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.UnitTests.Fakes;

namespace CloudHubs.UnitTests.Application.Organizations;

public class OrganizationRoleServiceTests
{
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
}
