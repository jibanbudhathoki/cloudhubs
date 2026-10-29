using CloudHubs.Domain.Organizations;

namespace CloudHubs.Application.Organizations;

public class OrganizationRoleService(IOrganizationRoleRepository repository)
{
    public Task<IReadOnlyCollection<OrganizationRole>> GetRolesAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(organizationId, cancellationToken);

    public Task<OrganizationRole?> GetRoleAsync(
        Guid organizationId,
        Guid roleId,
        CancellationToken cancellationToken = default) =>
        repository.GetByIdAsync(organizationId, roleId, cancellationToken);

    public Task<IReadOnlyCollection<OrganizationMembership>> GetMembersAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default) =>
        repository.GetMembersAsync(organizationId, cancellationToken);

    public async Task<OrganizationRole> CreateRoleAsync(
        Guid organizationId,
        string name,
        string? description,
        OrganizationPermission permissions,
        CancellationToken cancellationToken = default)
    {
        Validate(name, description, permissions);
        var normalizedName = name.Trim();
        if (await repository.NameExistsAsync(organizationId, normalizedName, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException("A role with this name already exists in the organization.");
        }

        return await repository.AddAsync(new OrganizationRole
        {
            OrganizationId = organizationId,
            Name = normalizedName,
            Description = description?.Trim() ?? string.Empty,
            Permissions = permissions
        }, cancellationToken);
    }

    public async Task<OrganizationRole?> UpdateRoleAsync(
        Guid organizationId,
        Guid roleId,
        string name,
        string? description,
        OrganizationPermission permissions,
        CancellationToken cancellationToken = default)
    {
        Validate(name, description, permissions);
        var role = await repository.GetByIdAsync(organizationId, roleId, cancellationToken);
        if (role is null) return null;
        var normalizedName = name.Trim();
        if (await repository.NameExistsAsync(organizationId, normalizedName, roleId, cancellationToken))
        {
            throw new InvalidOperationException("A role with this name already exists in the organization.");
        }

        role.Name = normalizedName;
        role.Description = description?.Trim() ?? string.Empty;
        role.Permissions = permissions;
        role.UpdatedAt = DateTime.UtcNow;
        return await repository.UpdateAsync(role, cancellationToken);
    }

    public async Task<bool> DeleteRoleAsync(
        Guid organizationId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await repository.GetByIdAsync(organizationId, roleId, cancellationToken);
        if (role is null) return false;
        if (await repository.IsAssignedAsync(organizationId, roleId, cancellationToken))
        {
            throw new InvalidOperationException("Remove this role from organization members before deleting it.");
        }

        return await repository.DeleteAsync(role, cancellationToken);
    }

    public async Task<bool> AssignRoleAsync(
        Guid organizationId,
        Guid userId,
        Guid? roleId,
        CancellationToken cancellationToken = default)
    {
        var membership = await repository.GetMembershipAsync(organizationId, userId, cancellationToken);
        if (membership is null) return false;
        if (membership.Role == OrganizationMemberRole.Owner && roleId.HasValue)
        {
            throw new InvalidOperationException("The organization owner cannot be assigned a custom role.");
        }
        var role = roleId.HasValue
            ? await repository.GetByIdAsync(organizationId, roleId.Value, cancellationToken)
            : null;
        if (roleId.HasValue && role is null)
        {
            throw new ArgumentException("Choose a role belonging to this organization.", nameof(roleId));
        }

        membership.CustomRoleId = roleId;
        membership.CustomRole = role;
        return await repository.SaveMembershipAsync(membership, cancellationToken);
    }

    private static void Validate(string name, string? description, OrganizationPermission permissions)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
        {
            throw new ArgumentException("Role name is required and must be 100 characters or fewer.", nameof(name));
        }
        if (description?.Length > 500)
        {
            throw new ArgumentException("Role description must be 500 characters or fewer.", nameof(description));
        }
        if (permissions == OrganizationPermission.None || (permissions & ~OrganizationPermission.All) != 0)
        {
            throw new ArgumentException("Select at least one valid permission.", nameof(permissions));
        }
    }
}
