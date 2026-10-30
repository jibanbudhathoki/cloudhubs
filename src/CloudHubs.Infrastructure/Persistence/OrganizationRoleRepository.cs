using Microsoft.EntityFrameworkCore;
using CloudHubs.Application.Organizations;
using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Users;

namespace CloudHubs.Infrastructure.Persistence;

public class OrganizationRoleRepository(CloudHubsDbContext dbContext) : IOrganizationRoleRepository
{
    public async Task<IReadOnlyCollection<OrganizationRole>> GetAllAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default) =>
        await dbContext.OrganizationRoles
            .AsNoTracking()
            .Include(role => role.Members)
            .Where(role => role.OrganizationId == organizationId)
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

    public Task<OrganizationRole?> GetByIdAsync(
        Guid organizationId,
        Guid roleId,
        CancellationToken cancellationToken = default) =>
        dbContext.OrganizationRoles.FirstOrDefaultAsync(
            role => role.OrganizationId == organizationId && role.Id == roleId,
            cancellationToken);

    public Task<bool> NameExistsAsync(
        Guid organizationId,
        string name,
        Guid? exceptRoleId = null,
        CancellationToken cancellationToken = default) =>
        dbContext.OrganizationRoles.AnyAsync(
            role => role.OrganizationId == organizationId
                && role.Name.ToLower() == name.ToLower()
                && (!exceptRoleId.HasValue || role.Id != exceptRoleId.Value),
            cancellationToken);

    public Task<bool> IsAssignedAsync(
        Guid organizationId,
        Guid roleId,
        CancellationToken cancellationToken = default) =>
        dbContext.OrganizationMemberships.AnyAsync(
            membership => membership.OrganizationId == organizationId && membership.CustomRoleId == roleId,
            cancellationToken);

    public async Task<OrganizationRole> AddAsync(OrganizationRole role, CancellationToken cancellationToken = default)
    {
        dbContext.OrganizationRoles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return role;
    }

    public async Task<OrganizationRole> UpdateAsync(OrganizationRole role, CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        return role;
    }

    public async Task<bool> DeleteAsync(OrganizationRole role, CancellationToken cancellationToken = default)
    {
        dbContext.OrganizationRoles.Remove(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<OrganizationMembership>> GetMembersAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default) =>
        await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId)
            .Include(membership => membership.User)
            .Include(membership => membership.CustomRole)
            .OrderBy(membership => membership.User.Name)
            .ToListAsync(cancellationToken);

    public Task<OrganizationMembership?> GetMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        dbContext.OrganizationMemberships
            .Include(membership => membership.CustomRole)
            .FirstOrDefaultAsync(
                membership => membership.OrganizationId == organizationId && membership.UserId == userId,
                cancellationToken);

    public async Task<IReadOnlyCollection<OrganizationMembership>> GetMembershipsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Include(membership => membership.Organization)
                .ThenInclude(organization => organization.Subscription)
                    .ThenInclude(subscription => subscription!.Plan)
            .Include(membership => membership.Organization)
                .ThenInclude(organization => organization.AccountSubscription)
                    .ThenInclude(subscription => subscription!.Plan)
            .OrderBy(membership => membership.Organization.Name)
            .ToListAsync(cancellationToken);

    public async Task<bool> SaveMembershipAsync(
        OrganizationMembership membership,
        CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<OrganizationMembership> AddMembershipAsync(
        OrganizationMembership membership,
        CancellationToken cancellationToken = default)
    {
        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync(cancellationToken);
        return membership;
    }

    public async Task<OrganizationMembership> AddMembershipIfBelowLimitAsync(
        OrganizationMembership membership,
        int maximumMembers,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var lockedOrganization = await dbContext.Organizations
            .FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {membership.OrganizationId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        if (lockedOrganization.Count == 0)
        {
            throw new KeyNotFoundException("Organization was not found.");
        }

        var currentCount = await dbContext.OrganizationMemberships.CountAsync(
            existing => existing.OrganizationId == membership.OrganizationId,
            cancellationToken);
        if (maximumMembers > 0 && currentCount >= maximumMembers)
        {
            throw new InvalidOperationException($"The subscription plan allows up to {maximumMembers} members.");
        }
        if (await dbContext.OrganizationMemberships.AnyAsync(
                existing => existing.OrganizationId == membership.OrganizationId && existing.UserId == membership.UserId,
                cancellationToken))
        {
            throw new InvalidOperationException("This user is already a member of the organization.");
        }

        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return membership;
    }

    public async Task<OrganizationMembership> AddNewMemberIfBelowLimitAsync(
        User user,
        Guid organizationId,
        OrganizationMemberRole role,
        int maximumMembers,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var lockedOrganization = await dbContext.Organizations
            .FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {organizationId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        if (lockedOrganization.Count == 0)
        {
            throw new KeyNotFoundException("Organization was not found.");
        }

        if (await dbContext.Users.AnyAsync(
                existing => existing.Email != null && existing.Email.ToLower() == user.Email!.ToLower(),
                cancellationToken))
        {
            throw new InvalidOperationException("An account already uses this email. Add the existing account instead.");
        }
        if (await dbContext.Users.AnyAsync(
                existing => existing.LoginId == user.LoginId,
                cancellationToken))
        {
            throw new InvalidOperationException("The generated member ID is already in use. Please try again.");
        }

        var currentCount = await dbContext.OrganizationMemberships.CountAsync(
            existing => existing.OrganizationId == organizationId,
            cancellationToken);
        if (maximumMembers > 0 && currentCount >= maximumMembers)
        {
            throw new InvalidOperationException($"The subscription plan allows up to {maximumMembers} members.");
        }

        user.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            User = user,
            Role = role
        });
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return user.OrganizationMemberships.Single();
    }

    public async Task<bool> RemoveMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var membership = await dbContext.OrganizationMemberships.FirstOrDefaultAsync(
            item => item.OrganizationId == organizationId && item.UserId == userId,
            cancellationToken);
        if (membership is null) return false;
        if (membership.Role == OrganizationMemberRole.Owner)
        {
            throw new InvalidOperationException("The organization owner cannot be removed.");
        }

        dbContext.OrganizationMemberships.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
