using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;

namespace CloudHubs.UnitTests.Fakes;

internal sealed class OrganizationRoleRepositoryStub : IOrganizationRoleRepository
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
