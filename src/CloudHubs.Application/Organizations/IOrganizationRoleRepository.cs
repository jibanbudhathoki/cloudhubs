using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Users;

namespace CloudHubs.Application.Organizations;

public interface IOrganizationRoleRepository
{
    Task<IReadOnlyCollection<OrganizationRole>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<OrganizationRole?> GetByIdAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? exceptRoleId = null, CancellationToken cancellationToken = default);
    Task<bool> IsAssignedAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken = default);
    Task<OrganizationRole> AddAsync(OrganizationRole role, CancellationToken cancellationToken = default);
    Task<OrganizationRole> UpdateAsync(OrganizationRole role, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(OrganizationRole role, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<OrganizationMembership>> GetMembersAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<OrganizationMembership?> GetMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<OrganizationMembership>> GetMembershipsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OrganizationMembership> AddMembershipAsync(OrganizationMembership membership, CancellationToken cancellationToken = default);
    Task<OrganizationMembership> AddMembershipIfBelowLimitAsync(OrganizationMembership membership, int maximumMembers, CancellationToken cancellationToken = default);
    Task<OrganizationMembership> AddNewMemberIfBelowLimitAsync(
        User user,
        Guid organizationId,
        OrganizationMemberRole role,
        int maximumMembers,
        CancellationToken cancellationToken = default);
    Task<bool> SaveMembershipAsync(OrganizationMembership membership, CancellationToken cancellationToken = default);
    Task<bool> RemoveMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
}
