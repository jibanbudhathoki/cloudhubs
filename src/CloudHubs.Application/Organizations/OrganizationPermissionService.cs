using CloudHubs.Domain.Organizations;

namespace CloudHubs.Application.Organizations;

public class OrganizationPermissionService(IOrganizationRoleRepository repository)
{
    public async Task<bool> HasPermissionAsync(
        Guid organizationId,
        Guid userId,
        OrganizationPermission permission,
        CancellationToken cancellationToken = default)
    {
        if (permission == OrganizationPermission.None || (permission & ~OrganizationPermission.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(permission));
        }

        var membership = await repository.GetMembershipAsync(organizationId, userId, cancellationToken);
        if (membership is null) return false;

        var granted = membership.CustomRole?.Permissions ?? membership.Role switch
        {
            OrganizationMemberRole.Owner => OrganizationPermission.All,
            OrganizationMemberRole.Admin => OrganizationPermission.All & ~OrganizationPermission.ManageBilling,
            OrganizationMemberRole.Developer => OrganizationPermission.ViewProjects
                | OrganizationPermission.ManageProjects
                | OrganizationPermission.ViewMembers,
            OrganizationMemberRole.Viewer => OrganizationPermission.ViewProjects,
            _ => OrganizationPermission.None
        };

        return (granted & permission) == permission;
    }
}
