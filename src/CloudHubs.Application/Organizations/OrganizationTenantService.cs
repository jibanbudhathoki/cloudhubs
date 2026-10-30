using CloudHubs.Application.Projects;
using CloudHubs.Application.Users;
using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Projects;
using CloudHubs.Domain.Subscriptions;
using CloudHubs.Domain.Users;

namespace CloudHubs.Application.Organizations;

public class OrganizationTenantService(
    IOrganizationRepository organizationRepository,
    IOrganizationRoleRepository roleRepository,
    IOrganizationProjectRepository projectRepository,
    IUserRepository userRepository,
    OrganizationPermissionService permissionService)
{
    public async Task<Organization> RequireAccessAsync(
        Guid organizationId,
        Guid userId,
        OrganizationPermission permission,
        bool requireApiAccess,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || user.Role == UserRole.Superuser)
        {
            throw new UnauthorizedAccessException("This account is not permitted to access organization resources.");
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId, cancellationToken)
            ?? throw new KeyNotFoundException("Organization was not found.");
        if (!organization.IsAvailableForUse())
        {
            throw new UnauthorizedAccessException("The organization or its subscription is not active.");
        }
        var activePlan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        if (activePlan is not { } plan)
        {
            throw new UnauthorizedAccessException("The organization has no subscription plan with configured entitlements.");
        }
        if (requireApiAccess && !plan.HasApiAccess)
        {
            throw new UnauthorizedAccessException("API access is not included in the organization’s subscription plan.");
        }
        if (!await permissionService.HasPermissionAsync(organizationId, userId, permission, cancellationToken))
        {
            throw new UnauthorizedAccessException("Your organization role does not allow this action.");
        }

        return organization;
    }

    public async Task<IReadOnlyCollection<Project>> GetProjectsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ViewProjects, requireApiAccess, cancellationToken);
        return await projectRepository.GetAllAsync(organizationId, cancellationToken);
    }

    public async Task<Project> CreateProjectAsync(
        Guid organizationId,
        Guid userId,
        string name,
        string slug,
        string? description,
        string? rootPath,
        string? templateId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        var organization = await RequireAccessAsync(
            organizationId, userId, OrganizationPermission.ManageProjects, requireApiAccess, cancellationToken);
        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan
            ?? throw new UnauthorizedAccessException("The organization has no subscription plan with configured entitlements.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Project name is required and must be 200 characters or fewer.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug) || slug.Trim().Length > 120 || !IsSlug(slug))
            throw new ArgumentException("Project slug must contain only letters, numbers, hyphens, or underscores and be 120 characters or fewer.", nameof(slug));
        if (description?.Length > 2000)
            throw new ArgumentException("Project description must be 2,000 characters or fewer.", nameof(description));
        if (rootPath?.Length > 500)
            throw new ArgumentException("Project root path must be 500 characters or fewer.", nameof(rootPath));
        if (templateId?.Length > 150)
            throw new ArgumentException("Template ID must be 150 characters or fewer.", nameof(templateId));

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (await projectRepository.SlugExistsAsync(organizationId, normalizedSlug, cancellationToken))
        {
            throw new InvalidOperationException("A project with this slug already exists in the organization.");
        }

        return await projectRepository.AddIfBelowLimitAsync(new Project
        {
            OrganizationId = organizationId,
            Name = name.Trim(),
            Slug = normalizedSlug,
            Description = description?.Trim() ?? string.Empty,
            RootPath = rootPath?.Trim() ?? string.Empty,
            TemplateId = string.IsNullOrWhiteSpace(templateId) ? "default" : templateId.Trim()
        }, plan.MaxProjects, cancellationToken);
    }

    public async Task<bool> DeleteProjectAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ManageProjects, requireApiAccess, cancellationToken);
        var project = await projectRepository.GetByIdAsync(organizationId, projectId, cancellationToken);
        return project is not null && await projectRepository.DeleteAsync(project, cancellationToken);
    }

    public async Task<Project?> UpdateProjectAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        string name,
        string slug,
        string? description,
        string? rootPath,
        string? templateId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ManageProjects, requireApiAccess, cancellationToken);
        var project = await projectRepository.GetByIdAsync(organizationId, projectId, cancellationToken);
        if (project is null) return null;

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Project name is required and must be 200 characters or fewer.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug) || slug.Trim().Length > 120 || !IsSlug(slug))
            throw new ArgumentException("Project slug must contain only letters, numbers, hyphens, or underscores and be 120 characters or fewer.", nameof(slug));
        if (description?.Length > 2000)
            throw new ArgumentException("Project description must be 2,000 characters or fewer.", nameof(description));
        if (rootPath?.Length > 500)
            throw new ArgumentException("Project root path must be 500 characters or fewer.", nameof(rootPath));
        if (templateId?.Length > 150)
            throw new ArgumentException("Template ID must be 150 characters or fewer.", nameof(templateId));

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (!string.Equals(project.Slug, normalizedSlug, StringComparison.Ordinal)
            && await projectRepository.SlugExistsAsync(organizationId, normalizedSlug, cancellationToken))
        {
            throw new InvalidOperationException("A project with this slug already exists in the organization.");
        }

        project.Name = name.Trim();
        project.Slug = normalizedSlug;
        project.Description = description?.Trim() ?? string.Empty;
        project.RootPath = rootPath?.Trim() ?? string.Empty;
        project.TemplateId = string.IsNullOrWhiteSpace(templateId) ? "default" : templateId.Trim();
        return await projectRepository.UpdateAsync(project, cancellationToken);
    }

    public async Task<IReadOnlyCollection<OrganizationMembership>> GetMembersAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ViewMembers, requireApiAccess, cancellationToken);
        return await roleRepository.GetMembersAsync(organizationId, cancellationToken);
    }

    public async Task<OrganizationMembership> AddMemberAsync(
        Guid organizationId,
        Guid actorId,
        string email,
        OrganizationMemberRole role,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        var organization = await RequireAccessAsync(
            organizationId, actorId, OrganizationPermission.ManageMembers, requireApiAccess, cancellationToken);
        if (role is OrganizationMemberRole.Owner || !Enum.IsDefined(role))
            throw new ArgumentException("Choose a valid non-owner organization role.", nameof(role));

        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan
            ?? throw new UnauthorizedAccessException("The organization has no subscription plan with configured entitlements.");
        var user = await userRepository.GetByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.PasswordHash))
            throw new ArgumentException("No active user with sign-in credentials was found for this email.", nameof(email));
        if (user.Role == UserRole.Superuser)
            throw new ArgumentException("Superusers cannot be added as organization members.", nameof(email));
        return await roleRepository.AddMembershipIfBelowLimitAsync(new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            User = user,
            Role = role
        }, plan.MaxMembers, cancellationToken);
    }

    public async Task<OrganizationMembership> CreateMemberAsync(
        Guid organizationId,
        Guid actorId,
        User member,
        OrganizationMemberRole role,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        var organization = await RequireAccessAsync(
            organizationId, actorId, OrganizationPermission.ManageMembers, requireApiAccess, cancellationToken);
        if (role is OrganizationMemberRole.Owner || !Enum.IsDefined(role))
            throw new ArgumentException("Choose a valid non-owner organization role.", nameof(role));
        if (member.Role == UserRole.Superuser)
            throw new ArgumentException("Superusers cannot be added as organization members.", nameof(member));

        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan
            ?? throw new UnauthorizedAccessException("The organization has no subscription plan with configured entitlements.");
        return await roleRepository.AddNewMemberIfBelowLimitAsync(
            member, organizationId, role, plan.MaxMembers, cancellationToken);
    }

    public async Task<bool> RemoveMemberAsync(
        Guid organizationId,
        Guid actorId,
        Guid memberId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        await RequireAccessAsync(organizationId, actorId, OrganizationPermission.ManageMembers, requireApiAccess, cancellationToken);
        if (actorId == memberId)
            throw new InvalidOperationException("You cannot remove your own organization membership.");
        return await roleRepository.RemoveMembershipAsync(organizationId, memberId, cancellationToken);
    }

    public async Task<OrganizationMembership?> UpdateMemberRoleAsync(
        Guid organizationId,
        Guid actorId,
        Guid memberId,
        OrganizationMemberRole role,
        Guid? customRoleId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        await RequireAccessAsync(organizationId, actorId, OrganizationPermission.ManageMembers, requireApiAccess, cancellationToken);
        if (role == OrganizationMemberRole.Owner || !Enum.IsDefined(role))
            throw new ArgumentException("Choose a valid non-owner organization role.", nameof(role));

        var membership = await roleRepository.GetMembershipAsync(organizationId, memberId, cancellationToken);
        if (membership is null) return null;
        if (membership.Role == OrganizationMemberRole.Owner)
            throw new InvalidOperationException("The organization owner’s role cannot be changed.");
        if (customRoleId.HasValue
            && await roleRepository.GetByIdAsync(organizationId, customRoleId.Value, cancellationToken) is null)
            throw new ArgumentException("Choose a custom role belonging to this organization.", nameof(customRoleId));

        membership.Role = role;
        membership.CustomRoleId = customRoleId;
        return await roleRepository.SaveMembershipAsync(membership, cancellationToken) ? membership : null;
    }

    private static bool IsSlug(string slug) =>
        slug.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
